using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RemnantSaveGuardian.Saves
{
    /// <summary>A single tagged Unreal property.</summary>
    public sealed class Property
    {
        public Property(string name, string type, int arrayIndex, object? value)
        {
            Name = name;
            Type = type;
            ArrayIndex = arrayIndex;
            Value = value;
        }

        public string Name { get; }
        public string Type { get; }
        public int ArrayIndex { get; }
        /// <summary>
        /// Primitive (int, float, bool, string, ...), <see cref="ObjectReference"/>, <see cref="StructValue"/>,
        /// <see cref="ArrayValue"/>, <see cref="MapValue"/>, <see cref="TextValue"/> or <see cref="RawValue"/>.
        /// </summary>
        public object? Value { get; }

        public override string ToString() => $"{Name} ({Type}) = {Value}";
    }

    /// <summary>An ordered list of tagged properties, terminated in the file by a "None" name.</summary>
    public sealed class PropertyBag : IReadOnlyList<Property>
    {
        private readonly List<Property> _properties = new();

        internal void Add(Property property) => _properties.Add(property);

        public Property this[int index] => _properties[index];
        public int Count => _properties.Count;
        public IEnumerator<Property> GetEnumerator() => _properties.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public Property? Get(string name) => _properties.Find(p => p.Name == name);
        public bool Contains(string name) => Get(name) != null;

        public T? GetValue<T>(string name)
        {
            var value = Get(name)?.Value;
            return value is T typed ? typed : default;
        }

        /// <summary>Returns the property bag inside a struct property, if any.</summary>
        public PropertyBag? GetStruct(string name) => GetValue<StructValue>(name)?.Value as PropertyBag;

        public IEnumerable<T> GetArray<T>(string name) => GetValue<ArrayValue>(name)?.Items.OfType<T>() ?? Enumerable.Empty<T>();

        public int? GetInt(string name) => Get(name)?.Value is int v ? v : null;
        public string? GetString(string name) => Get(name)?.Value switch
        {
            string s => s,
            TextValue t => t.Value,
            _ => null,
        };

        public override string ToString() => $"PropertyBag[{Count}]";
    }

    /// <summary>Reference to an entry in an archive's object table.</summary>
    public sealed class ObjectReference
    {
        public ObjectReference(int index, string? path)
        {
            Index = index;
            Path = path;
        }

        public int Index { get; }
        /// <summary>Object path from the archive's object table, or null for a null reference.</summary>
        public string? Path { get; }
        public bool IsNull => Index < 0;
        public override string ToString() => Path ?? "null";
    }

    public sealed class StructValue
    {
        public StructValue(string structType, object? value)
        {
            StructType = structType;
            Value = value;
        }

        public string StructType { get; }
        /// <summary><see cref="PropertyBag"/> for property-serialized structs, otherwise the native value.</summary>
        public object? Value { get; }
        public override string ToString() => $"{StructType}: {Value}";
    }

    public sealed class ArrayValue
    {
        public ArrayValue(string elementType, string? structType, List<object?> items)
        {
            ElementType = elementType;
            StructType = structType;
            Items = items;
        }

        public string ElementType { get; }
        public string? StructType { get; }
        public List<object?> Items { get; }
        public override string ToString() => $"{ElementType}[{Items.Count}]";
    }

    public sealed class MapValue
    {
        public MapValue(string keyType, string valueType, List<KeyValuePair<object?, object?>> entries)
        {
            KeyType = keyType;
            ValueType = valueType;
            Entries = entries;
        }

        public string KeyType { get; }
        public string ValueType { get; }
        public List<KeyValuePair<object?, object?>> Entries { get; }
        public override string ToString() => $"Map<{KeyType},{ValueType}>[{Entries.Count}]";
    }

    /// <summary>FText; only the displayable string is kept.</summary>
    public sealed class TextValue
    {
        public TextValue(sbyte historyType, string? key, string? value)
        {
            HistoryType = historyType;
            Key = key;
            Value = value;
        }

        public sbyte HistoryType { get; }
        public string? Key { get; }
        public string? Value { get; }
        public override string ToString() => Value ?? "";
    }

    /// <summary>Bytes of a value whose layout is not understood; kept so nothing is silently lost.</summary>
    public sealed class RawValue
    {
        public RawValue(byte[] bytes) => Bytes = bytes;
        public byte[] Bytes { get; }
        public override string ToString() => $"<{Bytes.Length} bytes>";
    }

    public readonly record struct SaveVector(double X, double Y, double Z);
    public readonly record struct SaveQuat(double X, double Y, double Z, double W);
    public readonly record struct SaveTransform(SaveQuat Rotation, SaveVector Translation, SaveVector Scale)
    {
        internal static SaveTransform Read(SaveReader r) => new(
            new SaveQuat(r.ReadDouble(), r.ReadDouble(), r.ReadDouble(), r.ReadDouble()),
            new SaveVector(r.ReadDouble(), r.ReadDouble(), r.ReadDouble()),
            new SaveVector(r.ReadDouble(), r.ReadDouble(), r.ReadDouble()));
    }

    /// <summary>
    /// Reads Unreal tagged-property streams. Every property carries its own size, so anything that
    /// cannot be decoded is preserved as <see cref="RawValue"/> and parsing continues.
    /// </summary>
    internal sealed class PropertyReader
    {
        private readonly SaveReader _r;
        private readonly Func<int, string?> _resolveObject;

        public PropertyReader(SaveReader reader, Func<int, string?> resolveObject)
        {
            _r = reader;
            _resolveObject = resolveObject;
        }

        public PropertyBag ReadProperties()
        {
            var bag = new PropertyBag();
            while (true)
            {
                string name = _r.ReadFName();
                if (name == "None")
                {
                    return bag;
                }
                string type = _r.ReadFName();
                int size = _r.ReadInt32();
                int arrayIndex = _r.ReadInt32();
                bag.Add(new Property(name, type, arrayIndex, ReadPropertyValue(type, size)));
            }
        }

        private void SkipPropertyGuid()
        {
            if (_r.ReadByte() != 0)
            {
                _r.Skip(16);
            }
        }

        private object? ReadPropertyValue(string type, int size)
        {
            if (size < 0)
            {
                throw new SaveFormatException($"Negative property size {size} at offset {_r.Position}.");
            }
            switch (type)
            {
                case "BoolProperty":
                    {
                        // Bool stores its value in the tag itself, before the GUID flag.
                        bool value = _r.ReadByte() != 0;
                        SkipPropertyGuid();
                        return value;
                    }
                case "StructProperty":
                    {
                        string structType = _r.ReadFName();
                        _r.Skip(16); // struct GUID
                        SkipPropertyGuid();
                        return ReadSized(size, () => new StructValue(structType, ReadStructValue(structType)));
                    }
                case "ArrayProperty":
                case "SetProperty":
                    {
                        string innerType = _r.ReadFName();
                        SkipPropertyGuid();
                        return ReadSized(size, () => type == "SetProperty" ? ReadSet(innerType) : ReadArray(innerType));
                    }
                case "MapProperty":
                    {
                        string keyType = _r.ReadFName();
                        string valueType = _r.ReadFName();
                        SkipPropertyGuid();
                        return ReadSized(size, () => ReadMap(keyType, valueType));
                    }
                case "ByteProperty":
                    {
                        string enumName = _r.ReadFName();
                        SkipPropertyGuid();
                        // Plain bytes are one byte; enum-backed bytes are stored as an FName.
                        return ReadSized(size, () => size == 1 ? _r.ReadByte() : _r.ReadFName());
                    }
                case "EnumProperty":
                    {
                        _r.ReadFName(); // enum type
                        SkipPropertyGuid();
                        return ReadSized(size, () => _r.ReadFName());
                    }
                default:
                    SkipPropertyGuid();
                    return ReadSized(size, () => ReadScalar(type));
            }
        }

        /// <summary>
        /// Runs <paramref name="read"/> and leaves the reader exactly <paramref name="size"/> bytes later.
        /// If the reader throws, the bytes are kept as a <see cref="RawValue"/>.
        /// </summary>
        private object? ReadSized(int size, Func<object?> read)
        {
            int start = _r.Position;
            int end = checked(start + size);
            if (end > _r.Length)
            {
                throw new SaveFormatException($"Property of size {size} at offset {start} runs past the end of the buffer.");
            }
            object? value;
            try
            {
                value = read();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Unknown layout inside a sized property: fall back to the raw bytes below.
                value = null;
            }
            if (value == null || _r.Position != end)
            {
                _r.Position = start;
                value = new RawValue(_r.ReadBytes(size));
            }
            _r.Position = end;
            return value;
        }

        private object? ReadScalar(string type) => type switch
        {
            "IntProperty" => _r.ReadInt32(),
            "UInt32Property" => _r.ReadUInt32(),
            "Int64Property" => _r.ReadInt64(),
            "UInt64Property" => _r.ReadUInt64(),
            "Int16Property" => _r.ReadInt16(),
            "UInt16Property" => _r.ReadUInt16(),
            "Int8Property" => _r.ReadSByte(),
            "ByteProperty" => _r.ReadByte(),
            "BoolProperty" => _r.ReadByte() != 0,
            "FloatProperty" => _r.ReadSingle(),
            "DoubleProperty" => _r.ReadDouble(),
            "NameProperty" => _r.ReadFName(),
            "EnumProperty" => _r.ReadFName(),
            "StrProperty" => _r.ReadFString(),
            "SoftObjectProperty" => _r.ReadFString(),
            "SoftClassProperty" => _r.ReadFString(),
            "ObjectProperty" => ReadObjectReference(),
            "TextProperty" => ReadText(),
            _ => null, // unknown: caller stores raw bytes
        };

        private ObjectReference ReadObjectReference()
        {
            int index = _r.ReadInt32();
            return new ObjectReference(index, index >= 0 ? _resolveObject(index) : null);
        }

        private TextValue? ReadText()
        {
            _r.ReadUInt32(); // flags
            sbyte historyType = _r.ReadSByte();
            switch (historyType)
            {
                case -1: // None
                    {
                        bool hasCultureInvariantString = _r.ReadInt32() != 0;
                        return new TextValue(historyType, null, hasCultureInvariantString ? _r.ReadFString() : null);
                    }
                case 0: // Base: namespace, key, source string
                    {
                        _r.ReadFString();
                        string key = _r.ReadFString();
                        string source = _r.ReadFString();
                        return new TextValue(historyType, key, source);
                    }
                default:
                    return null; // other history types are kept raw
            }
        }

        private object? ReadStructValue(string structType)
        {
            switch (structType)
            {
                case "Guid":
                    return _r.ReadGuid();
                case "Vector":
                case "Rotator":
                    return new SaveVector(_r.ReadDouble(), _r.ReadDouble(), _r.ReadDouble());
                case "Quat":
                    return new SaveQuat(_r.ReadDouble(), _r.ReadDouble(), _r.ReadDouble(), _r.ReadDouble());
                case "Vector2D":
                    return (_r.ReadDouble(), _r.ReadDouble());
                case "IntPoint":
                    return (_r.ReadInt32(), _r.ReadInt32());
                case "LinearColor":
                    return (_r.ReadSingle(), _r.ReadSingle(), _r.ReadSingle(), _r.ReadSingle());
                case "Color":
                    return _r.ReadUInt32();
                case "DateTime":
                case "Timespan":
                    return _r.ReadInt64();
                case "SoftObjectPath":
                case "SoftClassPath":
                    return _r.ReadFString();
                case "PersistenceBlob":
                    return _r.ReadBytes(_r.ReadInt32());
                default:
                    return ReadProperties();
            }
        }

        private ArrayValue ReadArray(string innerType)
        {
            int count = _r.ReadInt32();
            if (count < 0 || count > _r.Remaining)
            {
                throw new SaveFormatException($"Invalid array count {count} at offset {_r.Position - 4}.");
            }
            var items = new List<object?>(count);
            if (innerType == "StructProperty")
            {
                // Arrays of structs repeat a full property tag describing the element struct.
                _r.ReadFName(); // element name
                _r.ReadFName(); // "StructProperty"
                _r.ReadInt32(); // total element size
                _r.ReadInt32(); // array index
                string structType = _r.ReadFName();
                _r.Skip(16);
                SkipPropertyGuid();
                for (int i = 0; i < count; i++)
                {
                    items.Add(new StructValue(structType, ReadStructValue(structType)));
                }
                return new ArrayValue(innerType, structType, items);
            }
            for (int i = 0; i < count; i++)
            {
                items.Add(ReadElement(innerType));
            }
            return new ArrayValue(innerType, null, items);
        }

        private ArrayValue ReadSet(string innerType)
        {
            _r.ReadInt32(); // number of removed elements
            int count = _r.ReadInt32();
            if (count < 0 || count > _r.Remaining)
            {
                throw new SaveFormatException($"Invalid set count {count} at offset {_r.Position - 4}.");
            }
            var items = new List<object?>(count);
            for (int i = 0; i < count; i++)
            {
                items.Add(ReadElement(innerType));
            }
            return new ArrayValue(innerType, null, items);
        }

        private MapValue ReadMap(string keyType, string valueType)
        {
            _r.ReadInt32(); // number of removed entries
            int count = _r.ReadInt32();
            if (count < 0 || count > _r.Remaining)
            {
                throw new SaveFormatException($"Invalid map count {count} at offset {_r.Position - 4}.");
            }
            var entries = new List<KeyValuePair<object?, object?>>(count);
            for (int i = 0; i < count; i++)
            {
                var key = ReadElement(keyType);
                var value = ReadElement(valueType);
                entries.Add(new KeyValuePair<object?, object?>(key, value));
            }
            return new MapValue(keyType, valueType, entries);
        }

        /// <summary>Reads an untagged container element.</summary>
        private object? ReadElement(string type)
        {
            if (type == "StructProperty")
            {
                // Container elements carry no struct type; property-serialized structs are the common case.
                return new StructValue("", ReadProperties());
            }
            return ReadScalar(type) ?? throw new SaveFormatException($"Unsupported container element type {type}.");
        }
    }
}
