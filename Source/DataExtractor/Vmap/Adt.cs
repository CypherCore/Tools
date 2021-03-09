/*
 * Copyright (C) 2012-2019 CypherCore <http://github.com/CypherCore>
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

using DataExtractor.Framework.Constants;
using DataExtractor.Framework.GameMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace DataExtractor.Vmap
{
    class ADTFile
    {
        public ADTFile(string filename, bool cache)
        {
            _fileStream = Program.CascHandler.OpenFile(filename);
            cacheable = cache;
            dirfileCache = null;
        }

        public ADTFile(uint fileDataId, bool cache)
        {
            _fileStream = Program.CascHandler.OpenFile((int)fileDataId);
            cacheable = cache;
            dirfileCache = null;
        }

        public bool init(uint map_num, uint originalMapId)
        {
            if (dirfileCache != null)
                return initFromCache(map_num, originalMapId);

            if (_fileStream == null)
                return false;

            if (cacheable)
                dirfileCache = new List<ADTOutputCache>();

            string dirname = Program.WmoDirectory + "dir_bin";
            using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(dirname, FileMode.Append, FileAccess.Write)))
            {
                using (BinaryReader binaryReader = new BinaryReader(_fileStream))
                {
                    long fileLength = binaryReader.BaseStream.Length;
                    while (binaryReader.BaseStream.Position < fileLength)
                    {
                        string fourcc = binaryReader.ReadStringFromChars(4, true);
                        uint size = binaryReader.ReadUInt32();

                        long nextpos = binaryReader.BaseStream.Position + size;

                        if (fourcc == "MMDX")
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
                                        Model.Extract(doodadDef, ModelInstanceNames[(int)doodadDef.Id], map_num, originalMapId, binaryWriter, dirfileCache);
                                    }
                                    else
                                    {
                                        string fileName = $"FILE{doodadDef.Id}:X8.xxx";
                                        VmapFile.ExtractSingleModel(fileName);
                                        Model.Extract(doodadDef, fileName, map_num, originalMapId, binaryWriter, dirfileCache);
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
                                        WMORoot.Extract(mapObjDef, WmoInstanceNames[(int)mapObjDef.Id], false, map_num, originalMapId, binaryWriter, dirfileCache);
                                        Model.ExtractSet(VmapFile.WmoDoodads[WmoInstanceNames[(int)mapObjDef.Id]], mapObjDef, false, map_num, originalMapId, binaryWriter, dirfileCache);
                                    }
                                    else
                                    {
                                        string fileName = $"FILE{mapObjDef.Id:8X}.xxx";
                                        VmapFile.ExtractSingleWmo(fileName);
                                        WMORoot.Extract(mapObjDef, fileName, false, map_num, originalMapId, binaryWriter, dirfileCache);
                                        Model.ExtractSet(VmapFile.WmoDoodads[fileName], mapObjDef, false, map_num, originalMapId, binaryWriter, dirfileCache);
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

        bool initFromCache(uint map_num, uint originalMapId)
        {
            if (dirfileCache.Empty())
                return true;

            string dirname = Program.WmoDirectory + "dir_bin";
            using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(dirname, FileMode.Append, FileAccess.Write)))
            {
                foreach (ADTOutputCache cached in dirfileCache)
                {
                    binaryWriter.Write(map_num);
                    uint flags = cached.Flags;
                    if (map_num != originalMapId)
                        flags |= ModelFlags.ParentSpawn;
                    binaryWriter.Write(flags);
                    binaryWriter.Write(cached.Data);
                }
            }

            return true;
        }

        Stream _fileStream;
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
