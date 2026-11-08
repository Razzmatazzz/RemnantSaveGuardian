using System;
using System.Collections.Generic;

namespace RemnantSaveGuardian.Saves
{
    /// <summary>An actor stored in a <see cref="PersistenceContainer"/>.</summary>
    public sealed class PersistenceActor
    {
        private readonly byte[] _blob;
        private readonly int _offset;
        private SaveArchive? _archive;

        internal PersistenceActor(byte[] blob, ulong uniqueId, int offset, int size, string? dynamicClassPath, SaveTransform? spawnTransform)
        {
            _blob = blob;
            _offset = offset;
            UniqueId = uniqueId;
            Size = size;
            DynamicClassPath = dynamicClassPath;
            SpawnTransform = spawnTransform;
        }

        public ulong UniqueId { get; }
        public int Size { get; }
        /// <summary>Class path for actors spawned at runtime (quests, zones, dropped items).</summary>
        public string? DynamicClassPath { get; }
        public SaveTransform? SpawnTransform { get; }

        /// <summary>Parsed on first access; the large quest containers hold thousands of actors.</summary>
        public SaveArchive Archive => _archive ??= ReadArchive();

        public UObject? Root => Archive.RootObject;
        public string ClassPath => DynamicClassPath ?? Root?.ObjectPath ?? "";

        private SaveArchive ReadArchive()
        {
            // int32 hasTransform, optional FTransform, then an archive without package versions.
            // The archive's table offsets are relative to the start of the actor record.
            var r = new SaveReader(_blob, _offset);
            bool hasTransform = r.ReadInt32() != 0;
            if (hasTransform)
            {
                r.Skip(80);
            }
            return SaveArchive.Read(_blob, r.Position, _offset, hasPackageVersion: false, hasClassPath: false);
        }

        public override string ToString() => ClassPath;
    }

    /// <summary>
    /// Contents of a PersistenceContainer blob: uint32 version, uint32 indexOffset, uint32 dynamicOffset,
    /// actor records, dynamic actor table (unique id, transform, class path), actor index (unique id, offset, size).
    /// </summary>
    public sealed class PersistenceContainer
    {
        private PersistenceContainer(uint version, List<PersistenceActor> actors)
        {
            Version = version;
            Actors = actors;
        }

        public uint Version { get; }
        public IReadOnlyList<PersistenceActor> Actors { get; }

        public static PersistenceContainer Read(byte[] blob)
        {
            var r = new SaveReader(blob);
            uint version = r.ReadUInt32();
            int indexOffset = checked((int)r.ReadUInt32());
            int dynamicOffset = checked((int)r.ReadUInt32());

            var dynamicActors = new Dictionary<ulong, (string classPath, SaveTransform transform)>();
            r.Position = dynamicOffset;
            int dynamicCount = r.ReadInt32();
            if (dynamicCount < 0 || dynamicCount > r.Remaining)
            {
                throw new SaveFormatException($"Invalid dynamic actor count {dynamicCount}.");
            }
            for (int i = 0; i < dynamicCount; i++)
            {
                ulong id = r.ReadUInt64();
                var transform = SaveTransform.Read(r);
                string package = r.ReadFString();
                string asset = r.ReadFString();
                dynamicActors[id] = ($"{package}.{asset}", transform);
            }

            r.Position = indexOffset;
            int count = r.ReadInt32();
            if (count < 0 || count > r.Remaining)
            {
                throw new SaveFormatException($"Invalid actor count {count}.");
            }
            var actors = new List<PersistenceActor>(count);
            for (int i = 0; i < count; i++)
            {
                ulong id = r.ReadUInt64();
                int offset = checked((int)r.ReadUInt32());
                int size = checked((int)r.ReadUInt32());
                if (offset < 0 || size < 0 || offset > blob.Length - size)
                {
                    throw new SaveFormatException($"Actor {id} record ({offset}+{size}) lies outside the container.");
                }
                bool isDynamic = dynamicActors.TryGetValue(id, out var dynamic);
                actors.Add(new PersistenceActor(blob, id, offset, size,
                    isDynamic ? dynamic.classPath : null,
                    isDynamic ? dynamic.transform : null));
            }
            return new PersistenceContainer(version, actors);
        }
    }
}
