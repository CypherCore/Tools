/*
 * Copyright (C) 2012-2017 CypherCore <http://github.com/CypherCore>
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using Framework.CASC.Constants;
using Framework.CASC.Structures;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Framework.CASC.Handlers
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

            long fileLength = blteEntry.BaseStream.Length;
            while (blteEntry.BaseStream.Position < fileLength)
            {
                var entries = new RootEntry[blteEntry.ReadInt32()];

                uint contentFlags = blteEntry.ReadUInt32();
                var localeFlags = (LocaleMask)blteEntry.ReadUInt32();

                int fileDataIndex = 0;
                for (var i = 0; i < entries.Length; i++)
                {
                    entries[i].LocaleFlags = localeFlags;
                    entries[i].ContentFlags = contentFlags;

                    entries[i].FileDataId = fileDataIndex + blteEntry.ReadInt32();
                    fileDataIndex = entries[i].FileDataId + 1;
                }

                for (var i = 0; i < entries.Length; i++)
                {
                    entries[i].MD5 = blteEntry.ReadBytes(16);
                    entries[i].Hash = blteEntry.ReadUInt64();
                }

                list.AddRange(entries);
            }

            entries = list.ToLookup(re => re.Hash);
            entriesByFileDataId = list.ToLookup(re => re.FileDataId);
        }
    }
}
