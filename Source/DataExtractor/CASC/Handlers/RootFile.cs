using System.Collections.Generic;
using System.IO;
using System.Linq;
using CASC.Constants;
using CASC.Structures;

namespace CASC.Handlers
{
    public class RootFile
    {
        public ILookup<ulong, RootEntry> Entries => entries;
        public RootEntry[] this[ulong hash] => entries.Contains(hash) ? entries[hash].ToArray() : new RootEntry[0];
        public RootEntry[] this[int fileDataId] => entriesByFileDataId.Contains(fileDataId) ? entriesByFileDataId[fileDataId].ToArray() : new RootEntry[0];

        ILookup<ulong, RootEntry> entries;
        ILookup<int, RootEntry> entriesByFileDataId;

        public void LoadEntries(DataFile file, IndexEntry indexEntry)
        {
            var list = new List<RootEntry>();
            var blteEntry = new BinaryReader(DataFile.LoadBLTEEntry(indexEntry, file.readStream));

            while (blteEntry.BaseStream.Position < blteEntry.BaseStream.Length)
            {
                var entries = new RootEntry[blteEntry.ReadInt32()];

                blteEntry.BaseStream.Position += 4;

                var locales = (LocaleMask)blteEntry.ReadUInt32();

                int fileDataIndex = 0;
                int[] fileDataIds = new int[entries.Length];
                for (var i = 0; i < entries.Length; i++)
                {
                    fileDataIds[i] = fileDataIndex + blteEntry.ReadInt32();
                    fileDataIndex = fileDataIds[i] + 1;
                }

                for (var i = 0; i < entries.Length; i++)
                {
                    list.Add(new RootEntry
                    {
                        MD5 = blteEntry.ReadBytes(16),
                        Hash = blteEntry.ReadUInt64(),
                        FileDataId = fileDataIds[i],
                        Locales = locales
                    });
                }
            }

            entries = list.ToLookup(re => re.Hash);
            entriesByFileDataId = list.ToLookup(re => re.FileDataId);
        }
    }
}
