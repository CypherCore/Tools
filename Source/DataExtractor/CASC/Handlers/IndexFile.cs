using System.Collections.Generic;
using System.IO;
using CASC.Structures;

namespace CASC.Handlers
{
    public class IndexFile
    {
        public IndexEntry this[byte[] hash]
        {
            get
            {
                IndexEntry entry;

                if (entries.TryGetValue(hash, out entry))
                    return entry;

                return default(IndexEntry);
            }
        }

        public Dictionary<byte[], IndexEntry> entries = new Dictionary<byte[], IndexEntry>(new ByteArrayComparer());

        public IndexFile(string idx, bool cdnIndex = false, ushort fileIndex = 0)
        {
            if (cdnIndex)
            {
                var nullHash = new byte[16];

                using (var br = new BinaryReader(File.OpenRead(idx)))
                {
                    br.BaseStream.Position = br.BaseStream.Length - 12;

                    var entries = br.ReadUInt32();

                    br.BaseStream.Position = 0;

                    for (var i = 0; i < entries; i++)
                    {
                        var hash = br.ReadBytes(16);

                        if (hash.Compare(nullHash))
                            hash = br.ReadBytes(16);

                        var entry = new IndexEntry
                        {
                            Index = fileIndex,
                            Size = br.ReadBEInt32(),
                            Offset = br.ReadBEInt32()
                        };

                        if (this.entries.ContainsKey(hash))
                            continue;

                        this.entries.Add(hash, entry);
                    }
                }
            }
            else
            {
                using (var br = new BinaryReader(File.OpenRead(idx)))
                {
                    br.BaseStream.Position = (8 + br.ReadInt32() + 0x0F) & 0xFFFFFFF0;

                    var dataLength = br.ReadUInt32();
                    br.BaseStream.Position += 4;

                    // 18 bytes per entry.
                    for (var i = 0; i < dataLength / 18; i++)
                    {
                        var hash = br.ReadBytes(9);
                        var index = br.ReadByte();
                        var offset = br.ReadBEInt32();

                        var entry = new IndexEntry();
                        entry.Index = index << 2 | (byte)((offset & 0xC0000000) >> 30);
                        entry.Offset = (int)(offset & 0x3FFFFFFF);
                        entry.Size = br.ReadInt32();

                        if (entries.ContainsKey(hash))
                            continue;

                        entries.Add(hash, entry);
                    }
                }
            }
        }
    }
}
