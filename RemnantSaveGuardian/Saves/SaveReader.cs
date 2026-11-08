using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace RemnantSaveGuardian.Saves
{
    /// <summary>
    /// Thrown when save data does not match the expected binary layout.
    /// </summary>
    public class SaveFormatException : Exception
    {
        public SaveFormatException(string message) : base(message) { }
        public SaveFormatException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Little-endian cursor over a byte buffer with the primitive readers used by
    /// Unreal Engine serialization (FString, FName, etc.).
    /// </summary>
    internal sealed class SaveReader
    {
        private readonly byte[] _data;

        public SaveReader(byte[] data, int position = 0)
        {
            _data = data;
            Position = position;
        }

        public byte[] Data => _data;
        public int Position { get; set; }
        public int Length => _data.Length;
        public int Remaining => _data.Length - Position;

        /// <summary>Name table used to resolve FNames; set once an archive's name table has been read.</summary>
        public IReadOnlyList<string>? Names { get; set; }

        private int Advance(int count)
        {
            if (count < 0 || Position < 0 || count > _data.Length - Position)
            {
                throw new SaveFormatException($"Attempted to read {count} bytes at offset {Position} (buffer length {_data.Length}).");
            }
            int start = Position;
            Position += count;
            return start;
        }

        public void Skip(int count) => Advance(count);
        public byte ReadByte() => _data[Advance(1)];
        public sbyte ReadSByte() => (sbyte)_data[Advance(1)];
        public short ReadInt16() => BinaryPrimitives.ReadInt16LittleEndian(_data.AsSpan(Advance(2), 2));
        public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(Advance(2), 2));
        public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(Advance(4), 4));
        public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(Advance(4), 4));
        public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(_data.AsSpan(Advance(8), 8));
        public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(_data.AsSpan(Advance(8), 8));
        public float ReadSingle() => BinaryPrimitives.ReadSingleLittleEndian(_data.AsSpan(Advance(4), 4));
        public double ReadDouble() => BinaryPrimitives.ReadDoubleLittleEndian(_data.AsSpan(Advance(8), 8));
        public Guid ReadGuid() => new(_data.AsSpan(Advance(16), 16));

        public byte[] ReadBytes(int count)
        {
            int start = Advance(count);
            var result = new byte[count];
            Buffer.BlockCopy(_data, start, result, 0, count);
            return result;
        }

        /// <summary>
        /// Reads an Unreal FString: int32 length (negative = UTF-16 char count), including the null terminator.
        /// </summary>
        public string ReadFString()
        {
            int length = ReadInt32();
            if (length == 0)
            {
                return "";
            }
            if (length > 0)
            {
                int start = Advance(length);
                return Encoding.Latin1.GetString(_data, start, length - 1);
            }
            if (length == int.MinValue)
            {
                throw new SaveFormatException($"Invalid FString length at offset {Position - 4}.");
            }
            int byteCount = -length * 2;
            int ustart = Advance(byteCount);
            return Encoding.Unicode.GetString(_data, ustart, byteCount - 2);
        }

        /// <summary>
        /// Reads an FName as stored in Remnant 2 saves: uint16 name-table index whose high bit
        /// indicates that an int32 instance number follows.
        /// </summary>
        public string ReadFName() => ReadFName(out _);

        public string ReadFName(out int number)
        {
            if (Names == null)
            {
                throw new SaveFormatException("Attempted to read an FName before the name table was loaded.");
            }
            ushort raw = ReadUInt16();
            int index = raw & 0x7FFF;
            number = (raw & 0x8000) != 0 ? ReadInt32() : 0;
            if (index >= Names.Count)
            {
                throw new SaveFormatException($"FName index {index} out of range ({Names.Count} names) at offset {Position}.");
            }
            return Names[index];
        }
    }
}
