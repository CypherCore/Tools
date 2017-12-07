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

using System;
using System.Collections.Generic;
using System.IO;

namespace DataExtractor.Vmap
{
    class WDTFile
    {
        public bool init(string fileName, uint mapID)
        {
            MemoryStream stream = Program.cascHandler.ReadFile(fileName);
            if (stream == null)
                return false;

            string dirname = Program.wmoDirectory + "dir_bin";
            using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(dirname, FileMode.Append, FileAccess.Write)))
            {
                using (BinaryReader binaryReader = new BinaryReader(stream))
                {
                    while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
                    {
                        string fourcc = binaryReader.ReadStringFromChars(4, true);
                        uint size = binaryReader.ReadUInt32();

                        long nextpos = binaryReader.BaseStream.Position + size;

                        if (fourcc == "MAIN")
                        {
                        }
                        if (fourcc == "MWMO")
                        {
                            // global map objects
                            if (size != 0)
                            {
                                while (size > 0)
                                {
                                    string path = binaryReader.ReadCString();

                                    gWmoInstansName.Add(path.GetPlainName());
                                    VmapFile.ExtractSingleWmo(path);

                                    size -= (uint)(path.Length + 1);
                                }
                            }
                        }
                        else if (fourcc == "MODF")
                        {
                            // global wmo instance data
                            if (size != 0)
                            {
                                gnWMO = (int)size / 64;

                                for (int i = 0; i < gnWMO; ++i)
                                {
                                    int id = binaryReader.ReadInt32();
                                    WMOInstance inst = new WMOInstance(binaryReader, gWmoInstansName[id], mapID, 65, 65, binaryWriter);
                                }
                            }
                        }

                        binaryReader.BaseStream.Seek(nextpos, SeekOrigin.Begin);
                    }
                }
            }

            return true;
        }

        List<string> gWmoInstansName = new List<string>();
        int gnWMO;
    }
}
