using System;
using System.IO;
using System.IO.Compression;

namespace RemnantSaveGuardian.Saves
{
    /// <summary>
    /// A compressed Remnant 2 .sav file (profile or world).
    /// File layout: uint32 crc, uint32 totalSize, uint32 version, then zlib chunks each preceded by a 49-byte header.
    /// The decompressed payload starts with 8 bytes of header data followed by the top-level <see cref="SaveArchive"/>.
    /// Chunk format credit to https://gist.github.com/crackedmind
    /// </summary>
    public static class SaveFile
    {
        private const ulong ChunkHeaderTag = 0x222222229E2A83C1;
        private const int PayloadHeaderSize = 8;
        // Table offsets in the top-level archive are 8 bytes behind positions in the decompressed payload.
        private static readonly int[] TopLevelOffsetBases = { -8, 0 };

        /// <summary>Decompresses a save file. Throws <see cref="IOException"/> if the file is locked by the game.</summary>
        public static byte[] Decompress(string path)
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(file);
            reader.ReadUInt32(); // crc32
            uint totalSize = reader.ReadUInt32();
            reader.ReadUInt32(); // version

            using var output = new MemoryStream((int)Math.Min(totalSize, int.MaxValue));
            while (file.Position < file.Length)
            {
                ulong tag = reader.ReadUInt64();
                if (tag != ChunkHeaderTag)
                {
                    throw new SaveFormatException($"Unexpected chunk tag 0x{tag:X16} in {path} at offset {file.Position - 8}.");
                }
                reader.ReadUInt64(); // max chunk size
                reader.ReadByte(); // compression method (3 = zlib)
                ulong compressedSize = reader.ReadUInt64();
                reader.ReadUInt64(); // decompressed size
                reader.ReadUInt64(); // compressed size (repeated)
                reader.ReadUInt64(); // decompressed size (repeated)

                byte[] chunk = reader.ReadBytes(checked((int)compressedSize));
                if (chunk.Length != (int)compressedSize)
                {
                    throw new SaveFormatException($"Truncated chunk in {path}.");
                }
                using var chunkStream = new MemoryStream(chunk);
                using var zlib = new ZLibStream(chunkStream, CompressionMode.Decompress);
                zlib.CopyTo(output);
            }
            return output.ToArray();
        }

        /// <summary>Decompresses and parses the top-level archive of a save file.</summary>
        public static SaveArchive Load(string path) => ReadTopLevelArchive(Decompress(path));

        internal static SaveArchive ReadTopLevelArchive(byte[] payload)
        {
            SaveFormatException? lastError = null;
            foreach (int offsetBase in TopLevelOffsetBases)
            {
                try
                {
                    return SaveArchive.Read(payload, PayloadHeaderSize, offsetBase, hasPackageVersion: true, hasClassPath: true);
                }
                catch (SaveFormatException ex)
                {
                    lastError = ex;
                }
            }
            throw new SaveFormatException("Unable to read save archive.", lastError!);
        }
    }
}
