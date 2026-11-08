using System;
using System.Collections.Generic;
using System.Linq;

namespace RemnantSaveGuardian.Saves
{
    /// <summary>A component attached to an actor object.</summary>
    public sealed class UComponent
    {
        public UComponent(string key, PropertyBag properties, byte[] extraData)
        {
            Key = key;
            Properties = properties;
            ExtraData = extraData;
        }

        public string Key { get; }
        public PropertyBag Properties { get; }
        /// <summary>Natively serialized data that follows the properties (e.g. variable stores).</summary>
        public byte[] ExtraData { get; }
        public override string ToString() => Key;
    }

    /// <summary>An entry of an archive's object table together with its serialized data.</summary>
    public sealed class UObject
    {
        private static readonly IReadOnlyList<UComponent> NoComponents = Array.Empty<UComponent>();

        internal UObject(int index, bool wasLoadedFromPackage, string objectPath, string? name, int outerIndex)
        {
            Index = index;
            WasLoadedFromPackage = wasLoadedFromPackage;
            ObjectPath = objectPath;
            Name = name;
            OuterIndex = outerIndex;
        }

        public int Index { get; }
        /// <summary>True for assets referenced from packages (item blueprints, tilesets, ...).</summary>
        public bool WasLoadedFromPackage { get; }
        /// <summary>Asset path for loaded objects, class path for objects created at runtime.</summary>
        public string ObjectPath { get; }
        public string? Name { get; }
        public int OuterIndex { get; }
        public PropertyBag Properties { get; internal set; } = new();
        public byte[] ExtraData { get; internal set; } = Array.Empty<byte>();
        public bool IsActor { get; internal set; }
        public IReadOnlyList<UComponent> Components { get; internal set; } = NoComponents;

        public UComponent? GetComponent(string key) => Components.FirstOrDefault(c => c.Key == key);
        public override string ToString() => ObjectPath;
    }

    /// <summary>
    /// Remnant 2's object archive. Layout:
    /// [package versions] [save class path] uint64 nameTableOffset, int32 version, uint64 objectTableOffset,
    /// object data..., object table, name table. Offsets are relative to <c>offsetBase</c>.
    /// </summary>
    public sealed class SaveArchive
    {
        private SaveArchive() { }

        public int? PackageVersionUE4 { get; private set; }
        public int? PackageVersionUE5 { get; private set; }
        public string? SaveGameClassPath { get; private set; }
        public int Version { get; private set; }
        public IReadOnlyList<string> Names { get; private set; } = Array.Empty<string>();
        public IReadOnlyList<UObject> Objects { get; private set; } = Array.Empty<UObject>();

        public UObject? RootObject => Objects.Count > 0 ? Objects[0] : null;

        internal static SaveArchive Read(byte[] data, int headerPosition, int offsetBase, bool hasPackageVersion, bool hasClassPath)
        {
            var archive = new SaveArchive();
            var r = new SaveReader(data, headerPosition);
            if (hasPackageVersion)
            {
                archive.PackageVersionUE4 = r.ReadInt32();
                archive.PackageVersionUE5 = r.ReadInt32();
            }
            if (hasClassPath)
            {
                string package = r.ReadFString();
                string asset = r.ReadFString();
                archive.SaveGameClassPath = $"{package}.{asset}";
            }
            long nameTableOffset = (long)r.ReadUInt64();
            archive.Version = r.ReadInt32();
            long objectTableOffset = (long)r.ReadUInt64();
            int dataStart = r.Position;

            int nameTablePosition = ToPosition(data, offsetBase, nameTableOffset, "name table");
            int objectTablePosition = ToPosition(data, offsetBase, objectTableOffset, "object table");
            if (objectTablePosition < dataStart)
            {
                throw new SaveFormatException($"Object table offset {objectTablePosition} precedes object data at {dataStart}.");
            }

            archive.Names = ReadNameTable(new SaveReader(data, nameTablePosition));
            r.Names = archive.Names;
            var objects = ReadObjectTable(new SaveReader(data, objectTablePosition) { Names = archive.Names });
            archive.Objects = objects;

            string? Resolve(int index) => index >= 0 && index < objects.Count ? objects[index].ObjectPath : null;
            var properties = new PropertyReader(r, Resolve);

            while (r.Position < objectTablePosition)
            {
                int objectIndex = r.ReadInt32();
                int length = r.ReadInt32();
                if (objectIndex < 0 || objectIndex >= objects.Count)
                {
                    throw new SaveFormatException($"Object index {objectIndex} out of range at offset {r.Position - 8}.");
                }
                var obj = objects[objectIndex];
                if (length > 0)
                {
                    int end = checked(r.Position + length);
                    obj.Properties = properties.ReadProperties();
                    obj.ExtraData = ReadUpTo(r, end);
                }
                obj.IsActor = r.ReadByte() != 0;
                if (obj.IsActor)
                {
                    obj.Components = ReadComponents(r, properties);
                }
            }
            if (r.Position != objectTablePosition)
            {
                throw new SaveFormatException($"Object data ended at {r.Position}, expected {objectTablePosition}.");
            }
            return archive;
        }

        private static int ToPosition(byte[] data, int offsetBase, long offset, string what)
        {
            long position = offsetBase + offset;
            if (position < 0 || position >= data.Length)
            {
                throw new SaveFormatException($"The {what} offset {offset} is outside the buffer (length {data.Length}).");
            }
            return (int)position;
        }

        private static List<string> ReadNameTable(SaveReader r)
        {
            int count = r.ReadInt32();
            if (count < 0 || count > r.Remaining)
            {
                throw new SaveFormatException($"Invalid name table size {count}.");
            }
            var names = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                names.Add(r.ReadFString());
            }
            return names;
        }

        private static List<UObject> ReadObjectTable(SaveReader r)
        {
            int count = r.ReadInt32();
            if (count < 0 || count > r.Remaining)
            {
                throw new SaveFormatException($"Invalid object table size {count}.");
            }
            var objects = new List<UObject>(count);
            for (int i = 0; i < count; i++)
            {
                bool wasLoaded = r.ReadByte() != 0;
                string path = r.ReadFString();
                string? name = null;
                int outer = -1;
                if (!wasLoaded)
                {
                    name = r.ReadFName(out _);
                    outer = r.ReadInt32();
                }
                objects.Add(new UObject(i, wasLoaded, path, name, outer));
            }
            return objects;
        }

        private static List<UComponent> ReadComponents(SaveReader r, PropertyReader properties)
        {
            int count = r.ReadInt32();
            if (count < 0 || count > r.Remaining)
            {
                throw new SaveFormatException($"Invalid component count {count} at offset {r.Position - 4}.");
            }
            var components = new List<UComponent>(count);
            for (int i = 0; i < count; i++)
            {
                string key = r.ReadFString();
                int length = r.ReadInt32();
                int end = checked(r.Position + length);
                var bag = properties.ReadProperties();
                components.Add(new UComponent(key, bag, ReadUpTo(r, end)));
            }
            return components;
        }

        private static byte[] ReadUpTo(SaveReader r, int end)
        {
            if (r.Position > end)
            {
                throw new SaveFormatException($"Properties overran their declared length (at {r.Position}, expected end {end}).");
            }
            return r.ReadBytes(end - r.Position);
        }
    }
}
