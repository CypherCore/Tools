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
using DataExtractor.Vmap.Collision;

namespace DataExtractor.Vmap
{
    class ADTFile
    {
        public bool init(string fileName, uint map_num, uint tileX, uint tileY)
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

                        if (fourcc == "MCIN")
                        {
                        }
                        else if (fourcc == "MTEX")
                        {
                        }
                        else if (fourcc == "MMDX")
                        {
                            if (size != 0)
                            {
                                while (size > 0)
                                {
                                    string path = binaryReader.ReadCString();

                                    ModelInstanceNames.Add(path.GetPlainName());
                                    if (path.GetPlainName() == "Skullcandle02.m2")
                                    {

                                    }
                                    VmapFile.ExtractSingleModel(path);

                                    size -= (uint)(path.Length + 1);
                                }
                            }
                        }
                        else if (fourcc == "MWMO")
                        {
                            if (size != 0)
                            {
                                while (size > 0)
                                {
                                    string path = binaryReader.ReadCString();

                                    WmoInstanceNames.Add(path.GetPlainName());
                                    VmapFile.ExtractSingleWmo(path);

                                    size -= (uint)(path.Length + 1);
                                }
                            }
                        }
                        //======================
                        else if (fourcc == "MDDF")
                        {
                            if (size != 0)
                            {
                                nMDX = (int)size / 36;
                                for (int i = 0; i < nMDX; ++i)
                                {
                                    int id = binaryReader.ReadInt32();
                                    ModelInstance inst = new ModelInstance(binaryReader, ModelInstanceNames[id], map_num, tileX, tileY, binaryWriter);
                                }

                                ModelInstanceNames.Clear();
                            }
                        }
                        else if (fourcc == "MODF")
                        {
                            if (size != 0)
                            {
                                nWMO = (int)size / 64;
                                for (int i = 0; i < nWMO; ++i)
                                {
                                    int id = binaryReader.ReadInt32();
                                    WMOInstance inst = new WMOInstance(binaryReader, WmoInstanceNames[id], map_num, tileX, tileY, binaryWriter);
                                }

                                WmoInstanceNames.Clear();
                            }
                        }

                        //======================
                        binaryReader.BaseStream.Seek(nextpos, SeekOrigin.Begin);
                    }
                }
            }

            return true;
        }

        int nWMO;
        int nMDX;
        List<string> WmoInstanceNames = new List<string>();
        List<string> ModelInstanceNames = new List<string>();
    }
}
