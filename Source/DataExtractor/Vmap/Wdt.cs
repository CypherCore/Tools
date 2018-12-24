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
        public WDTFile(string mapName, bool cache)
        {
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
            Stream stream = Program.CascHandler.OpenFile(_mapName + ".wdt");
            if (stream == null)
                return false;

            string dirname = Program.WmoDirectory + "dir_bin";
            using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(dirname, FileMode.Append, FileAccess.Write)))
            {
                using (BinaryReader binaryReader = new BinaryReader(stream))
                {
                    long fileLength = binaryReader.BaseStream.Length;
                    while (binaryReader.BaseStream.Position < fileLength)
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

            string name = $"{_mapName}_{x}_{y}_obj0.adt";
            ADTFile adt = new ADTFile(name, _adtCache != null);
            if (_adtCache != null)
                _adtCache[x][y] = adt;
            return adt;
        }

        string _mapName;
        List<string> _wmoNames = new List<string>();
        ADTFile[][] _adtCache;
    }
}
