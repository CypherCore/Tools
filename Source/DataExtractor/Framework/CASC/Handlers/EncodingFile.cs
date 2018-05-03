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

using Framework.CASC.Structures;
using System.Collections.Generic;
using System.IO;

namespace Framework.CASC.Handlers
{
    public class EncodingFile
    {
        public EncodingEntry this[byte[] md5]
        {
            get
            {
                EncodingEntry entry;

                if (entries.TryGetValue(md5, out entry))
                    return entry;

                return default(EncodingEntry);
            }
        }

        public byte[] Key { get; }

        Dictionary<byte[], EncodingEntry> entries = new Dictionary<byte[], EncodingEntry>(new ByteArrayComparer());

        public EncodingFile(byte[] encodingKey)
        {
            Key = encodingKey.Slice(0, 9);
        }

        public void LoadEntries(DataFile file, IndexEntry indexEntry)
        {
            var blteEntry = new BinaryReader(DataFile.LoadBLTEEntry(indexEntry, file.readStream));

            blteEntry.BaseStream.Position = 9;

            var entries = blteEntry.ReadBEInt32();

            blteEntry.BaseStream.Position += 5;

            var offsetEntries = blteEntry.ReadBEInt32();

            blteEntry.BaseStream.Position += offsetEntries + (entries << 5);

            for (var i = 0; i < entries; i++)
            {
                var keys = blteEntry.ReadUInt16();

                while (keys != 0)
                {
                    var encodingEntry = new EncodingEntry
                    {
                        Keys = new byte[keys][],
                        Size = (uint)blteEntry.ReadBEInt32()
                    };

                    var md5 = blteEntry.ReadBytes(16);

                    for (var j = 0; j < keys; j++)
                        encodingEntry.Keys[j] = blteEntry.ReadBytes(16);

                    this.entries.Add(md5, encodingEntry);

                    keys = blteEntry.ReadUInt16();
                }

                while (blteEntry.ReadByte() == 0);

                blteEntry.BaseStream.Position -= 1;
            }

        }
    }
}
