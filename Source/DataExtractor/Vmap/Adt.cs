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
using Framework.Constants;
using Framework.GameMath;
using System.Runtime.InteropServices;

namespace DataExtractor.Vmap
{
    class ADTFile
    {
        public ADTFile(string filename, bool cache)
        {
            Adtfilename = filename;
            cacheable = cache;
            dirfileCache = null;
        }

        public bool init(uint map_num, uint tileX, uint tileY, uint originalMapId)
        {
            if (dirfileCache != null)
                return initFromCache(map_num, tileX, tileY, originalMapId);

            MemoryStream stream = Program.cascHandler.ReadFile(Adtfilename);
            if (stream == null)
                return false;

            if (cacheable)
                dirfileCache = new List<ADTOutputCache>();

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
                                uint doodadCount = size / 36; //sizeof(MDDF)
                                for (int i = 0; i < doodadCount; ++i)
                                {
                                    MDDF doodadDef = binaryReader.Read<MDDF>();
                                    if (!Convert.ToBoolean(doodadDef.Flags & 0x40))
                                    {
                                        Model.Extract(doodadDef, ModelInstanceNames[(int)doodadDef.Id], map_num, tileX, tileY, originalMapId, binaryWriter, dirfileCache);
                                    }
                                    else
                                    {
                                        string fileName = $"FILE{doodadDef.Id}:X8.xxx";
                                        VmapFile.ExtractSingleModel(fileName);
                                        Model.Extract(doodadDef, fileName, map_num, tileX, tileY, originalMapId, binaryWriter, dirfileCache);
                                    }
                                }

                                ModelInstanceNames.Clear();
                            }
                        }
                        else if (fourcc == "MODF")
                        {
                            if (size != 0)
                            {
                                uint mapObjectCount = size / 64; // sizeof(ADT::MODF);
                                for (int i = 0; i < mapObjectCount; ++i)
                                {
                                    MODF mapObjDef = binaryReader.Read<MODF>();
                                    if (!Convert.ToBoolean(mapObjDef.Flags & 0x8))
                                    {
                                        WMORoot.Extract(mapObjDef, WmoInstanceNames[(int)mapObjDef.Id], map_num, tileX, tileY, originalMapId, binaryWriter, dirfileCache);
                                    }
                                    else
                                    {
                                        string fileName = $"FILE{mapObjDef.Id}:8X.xxx";
                                        VmapFile.ExtractSingleModel(fileName);
                                        WMORoot.Extract(mapObjDef, fileName, map_num, tileX, tileY, originalMapId, binaryWriter, dirfileCache);
                                    }
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

        bool initFromCache(uint map_num, uint tileX, uint tileY, uint originalMapId)
        {
            if (dirfileCache.Empty())
                return true;

            string dirname = Program.wmoDirectory + "dir_bin";
            using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(dirname, FileMode.Append, FileAccess.Write)))
            {
                foreach (ADTOutputCache cached in dirfileCache)
                {
                    binaryWriter.Write(map_num);
                    binaryWriter.Write(tileX);
                    binaryWriter.Write(tileY);
                    uint flags = cached.Flags;
                    if (map_num != originalMapId)
                        flags |= ModelFlags.ParentSpawn;
                    binaryWriter.Write(flags);
                    binaryWriter.Write(cached.Data);
                }
            }

            return true;
        }

        string Adtfilename;
        bool cacheable;
        List<ADTOutputCache> dirfileCache = new List<ADTOutputCache>();
        List<string> WmoInstanceNames = new List<string>();
        List<string> ModelInstanceNames = new List<string>();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct MDDF
    {
        public uint Id { get; set; }
        public uint UniqueId { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public ushort Scale { get; set; }
        public ushort Flags { get; set; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct MODF
    {
        public uint Id;
        public uint UniqueId;
        public Vector3 Position;
        public Vector3 Rotation;
        public AxisAlignedBox Bounds;
        public ushort Flags;
        public ushort DoodadSet;
        public ushort NameSet;
        public ushort Scale;
    }

    public struct ADTOutputCache
    {
        public uint Flags { get; set; }
        public byte[] Data { get; set; }
    }
}
