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

using System;
using System.Collections.Generic;
using System.IO;

namespace DataExtractor.Vmap
{
    class WDTFile
    {
        public WDTFile(uint fileDataId, string mapName, bool cache)
        {
            _fileStream = Program.CascHandler.OpenFile((int)fileDataId);
            _mapName = mapName;

            if (cache)
            {
                _adtCache = new ADTFile[64][];
                for (var i = 0; i < 64; ++i)
                    _adtCache[i] = new ADTFile[64];
            }
        }

        public bool init(uint mapId)
        {
            if (_fileStream == null)
                return false;

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

                        if (fourcc == "MPHD")
                        {
                            _header = binaryReader.Read<MPHD>();
                        }
                        else if (fourcc == "MAIN")
                        {
                            _adtInfo = new MAIN();
                            _adtInfo.Read(binaryReader);
                        }
                        else if (fourcc == "MAID")
                        {
                            _adtFileDataIds = new MAID();
                            _adtFileDataIds.Read(binaryReader);
                        }
                        else if (fourcc == "MWMO")
                        {
                            // global map objects
                            if (size != 0)
                            {
                                while (size > 0)
                                {
                                    string path = binaryReader.ReadCString();

                                    _wmoNames.Add(path.GetPlainName());
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
                                uint mapObjectCount = size / 64; //sizeof(ADT::MODF);
                                for (int i = 0; i < mapObjectCount; ++i)
                                {
                                    MODF mapObjDef = binaryReader.Read<MODF>();
                                    if (!Convert.ToBoolean(mapObjDef.Flags & 0x8))
                                    {
                                        WMORoot.Extract(mapObjDef, _wmoNames[(int)mapObjDef.Id], true, mapId, mapId, binaryWriter, null);
                                        Model.ExtractSet(VmapFile.WmoDoodads[_wmoNames[(int)mapObjDef.Id]], mapObjDef, true, mapId, mapId, binaryWriter, null);
                                    }
                                    else
                                    {
                                        string fileName = $"FILE{mapObjDef.Id}:8X.xxx";
                                        VmapFile.ExtractSingleWmo(fileName);
                                        WMORoot.Extract(mapObjDef, fileName, true, mapId, mapId, binaryWriter, null);
                                        Model.ExtractSet(VmapFile.WmoDoodads[fileName], mapObjDef, true, mapId, mapId, binaryWriter, null);
                                    }
                                }
                            }
                        }

                        binaryReader.BaseStream.Seek(nextpos, SeekOrigin.Begin);
                    }
                }
            }

            return true;
        }

        public ADTFile GetMap(uint x, uint y)
        {
            if (!(x >= 0 && y >= 0 && x < 64 && y < 64))
                return null;

            if (_adtCache != null && _adtCache[x][y] != null)
                return _adtCache[x][y];

            if (!Convert.ToBoolean(_adtInfo.Data[y][x].Flag & 1))
                return null;

            ADTFile adt;
            string name = $"World\\Maps\\{_mapName}\\{_mapName}_{x}_{y}_obj0.adt";
            if ((_header.Flags & 0x200) != 0)
                adt = new ADTFile(_adtFileDataIds.Data[y][x].Obj0ADT, _adtCache != null);
            else
                adt = new ADTFile(name, _adtCache != null);

            if (_adtCache != null)
                _adtCache[x][y] = adt;

            return adt;
        }

        Stream _fileStream;
        MPHD _header;
        MAIN _adtInfo;
        MAID _adtFileDataIds;
        string _mapName;
        List<string> _wmoNames = new List<string>();
        ADTFile[][] _adtCache;
    }

    struct MPHD
    {
        public uint Flags;
        public uint LgtFileDataID;
        public uint OccFileDataID;
        public uint FogsFileDataID;
        public uint MpvFileDataID;
        public uint TexFileDataID;
        public uint WdlFileDataID;
        public uint Pd4FileDataID;
    }

    class MAIN
    {
        public SMAreaInfo[][] Data = new SMAreaInfo[64][];

        public void Read(BinaryReader reader)
        {
            for (var x = 0; x < 64; ++x)
            {
                Data[x] = new SMAreaInfo[64];

                for (var y = 0; y < 64; ++y)
                    Data[x][y] = reader.Read<SMAreaInfo>();
            }
        }

        public struct SMAreaInfo
        {
            public uint Flag;
            public uint AsyncId;
        }
    }

    class MAID
    {
        public SMAreaFileIDs[][] Data = new SMAreaFileIDs[64][];

        public void Read(BinaryReader reader)
        {
            for (var x = 0; x < 64; ++x)
            {
                Data[x] = new SMAreaFileIDs[64];

                for (var y = 0; y < 64; ++y)
                    Data[x][y] = reader.Read<SMAreaFileIDs>();
            }
        }

        public struct SMAreaFileIDs
        {
            public uint RootADT;         // FileDataID of mapname_xx_yy.adt
            public uint Obj0ADT;         // FileDataID of mapname_xx_yy_obj0.adt
            public uint Obj1ADT;         // FileDataID of mapname_xx_yy_obj1.adt
            public uint Tex0ADT;         // FileDataID of mapname_xx_yy_tex0.adt
            public uint LodADT;          // FileDataID of mapname_xx_yy_lod.adt
            public uint MapTexture;      // FileDataID of mapname_xx_yy.blp
            public uint MapTextureN;     // FileDataID of mapname_xx_yy_n.blp
            public uint MinimapTexture;  // FileDataID of mapxx_yy.blp
        }
    }
}
