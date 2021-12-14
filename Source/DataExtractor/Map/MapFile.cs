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

using DataExtractor.CASCLib;
using DataExtractor.Framework.ClientReader;
using DataExtractor.Framework.Constants;
using DataExtractor.Map;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace DataExtractor
{
    class MapFile
    {
        static MapFile()
        {
            for (var i = 0; i < SharedConst.ADT_CELLS_PER_GRID; ++i)
            {
                AreaIDs[i] = new ushort[SharedConst.ADT_CELLS_PER_GRID];
                LiquidEntries[i] = new ushort[SharedConst.ADT_CELLS_PER_GRID];
                LiquidFlags[i] = new LiquidHeaderTypeFlags[SharedConst.ADT_CELLS_PER_GRID];
            }

            for (var i = 0; i < SharedConst.ADT_CELLS_PER_GRID; ++i)
            {
                Holes[i] = new byte[SharedConst.ADT_CELLS_PER_GRID][];
                for (var x = 0; x < SharedConst.ADT_CELLS_PER_GRID; ++x)
                    Holes[i][x] = new byte[8];
            }

            for (var i = 0; i < SharedConst.ADT_GRID_SIZE; ++i)
            {
                V8[i] = new float[SharedConst.ADT_GRID_SIZE];
                UInt16_V8[i] = new ushort[SharedConst.ADT_GRID_SIZE];
                UInt8_V8[i] = new byte[SharedConst.ADT_GRID_SIZE];
                LiquidSnow[i] = new bool[SharedConst.ADT_GRID_SIZE];
            }

            for (var i = 0; i < SharedConst.ADT_GRID_SIZE + 1; ++i)
            {
                V9[i] = new float[SharedConst.ADT_GRID_SIZE + 1];
                UInt16_V9[i] = new ushort[SharedConst.ADT_GRID_SIZE + 1];
                UInt8_V9[i] = new byte[SharedConst.ADT_GRID_SIZE + 1];
                LiquidHeight[i] = new float[SharedConst.ADT_GRID_SIZE + 1];
            }

            for (var i = 0; i < 3; ++i)
            {
                FlightBoxMax[i] = new short[3];
                FlightBoxMin[i] = new short[3];
            }

            LoadRequiredDb2Files();
        }

        static bool LoadRequiredDb2Files()
        {
            LiquidMaterialStorage = DBReader.Read<LiquidMaterialRecord>(1132538);
            if (LiquidMaterialStorage == null)
            {
                Console.WriteLine("Fatal error: Invalid LiquidMaterial.db2 file format!");
                return false;
            }

            LiquidTypeStorage = DBReader.Read<LiquidTypeRecord>(1371380);
            if (LiquidTypeStorage == null)
            {
                Console.WriteLine("Fatal error: Invalid LiquidType.db2 file format!\n");
                return false;
            }

            return true;
        }

        static bool TransformToHighRes(ushort lowResHoles, byte[] hiResHoles)
        {
            for (byte i = 0; i < 8; i++)
            {
                for (byte j = 0; j < 8; j++)
                {
                    int holeIdxL = (i / 2) * 4 + (j / 2);
                    if (((lowResHoles >> holeIdxL) & 1) == 1)
                        hiResHoles[i] |= (byte)(1 << j);
                }
            }

            return BitConverter.ToUInt64(hiResHoles, 0) != 0;
        }

        public static bool ConvertADT(CASCHandler cascHandler, string fileName, string mapName, string outputPath, int gx, int gy, bool ignoreDeepWater)
        {
            ChunkedFile adt = new();

            if (!adt.LoadFile(cascHandler, fileName))
                return false;

            return ConvertADT(adt, mapName, outputPath, gx, gy, cascHandler.Config.GetBuildNumber(), ignoreDeepWater);
        }

        public static bool ConvertADT(CASCHandler cascHandler, uint fileDataId, string mapName, string outputPath, int gx, int gy, bool ignoreDeepWater)
        {
            ChunkedFile adt = new();

            if (!adt.LoadFile(cascHandler, fileDataId, $"Map {mapName} grid [{gx},{gy}]"))
                return false;

            return ConvertADT(adt, mapName, outputPath, gx, gy, cascHandler.Config.GetBuildNumber(), ignoreDeepWater);
        }

        public static bool ConvertADT(ChunkedFile adt, string mapName, string outputPath, int gx, int gy, uint build, bool ignoreDeepWater)
        {
            // Prepare map header
            MapFileHeader map;
            map.mapMagic = SharedConst.MAP_MAGIC;
            map.versionMagic = SharedConst.MAP_VERSION_MAGIC;
            map.buildMagic = build;

            // Get area flags data
            for (var x = 0; x < SharedConst.ADT_CELLS_PER_GRID; ++x)
            {
                for (var y = 0; y < SharedConst.ADT_CELLS_PER_GRID; ++y)
                {
                    AreaIDs[x][y] = 0;
                    LiquidEntries[x][y] = 0;
                    LiquidFlags[x][y] = 0;
                }
            }

            for (var x = 0; x < SharedConst.ADT_GRID_SIZE; ++x)
            {
                for (var y = 0; y < SharedConst.ADT_GRID_SIZE; ++y)
                {
                    V8[x][y] = 0;
                    LiquidSnow[x][y] = false;
                }
            }

            for (var x = 0; x < SharedConst.ADT_GRID_SIZE + 1; ++x)
                for (var y = 0; y < SharedConst.ADT_GRID_SIZE + 1; ++y)
                    V9[x][y] = 0;

            for (var x = 0; x < SharedConst.ADT_CELLS_PER_GRID; ++x)
                for (var y = 0; y < SharedConst.ADT_CELLS_PER_GRID; ++y)
                    for (var z = 0; z < 8; ++z)
                        Holes[x][y][z] = 0;

            bool hasHoles = false;
            bool hasFlightBox = false;

            foreach (var fileChunk in adt.chunks.LookupByKey("MCNK"))
            {
                MCNK mcnk = fileChunk.As<MCNK>();

                // Area data
                AreaIDs[mcnk.IndexY][mcnk.IndexX] = (ushort)mcnk.AreaID;

                // Height
                // Height values for triangles stored in order:
                // 1     2     3     4     5     6     7     8     9
                //    10    11    12    13    14    15    16    17
                // 18    19    20    21    22    23    24    25    26
                //    27    28    29    30    31    32    33    34
                // . . . . . . . .
                // For better get height values merge it to V9 and V8 map
                // V9 height map:
                // 1     2     3     4     5     6     7     8     9
                // 18    19    20    21    22    23    24    25    26
                // . . . . . . . .
                // V8 height map:
                //    10    11    12    13    14    15    16    17
                //    27    28    29    30    31    32    33    34
                // . . . . . . . .

                // Set map height as grid height
                for (int y = 0; y <= SharedConst.ADT_CELL_SIZE; y++)
                {
                    int cy = (int)mcnk.IndexY * SharedConst.ADT_CELL_SIZE + y;
                    for (int x = 0; x <= SharedConst.ADT_CELL_SIZE; x++)
                    {
                        int cx = (int)mcnk.IndexX * SharedConst.ADT_CELL_SIZE + x;
                        V9[cy][cx] = mcnk.ypos;
                    }
                }

                for (int y = 0; y < SharedConst.ADT_CELL_SIZE; y++)
                {
                    int cy = (int)mcnk.IndexY * SharedConst.ADT_CELL_SIZE + y;
                    for (int x = 0; x < SharedConst.ADT_CELL_SIZE; x++)
                    {
                        int cx = (int)mcnk.IndexX * SharedConst.ADT_CELL_SIZE + x;
                        V8[cy][cx] = mcnk.ypos;
                    }
                }

                // Get custom height
                FileChunk chunk = fileChunk.GetSubChunk("MCVT");
                if (chunk != null)
                {
                    MCVT mcvt = chunk.As<MCVT>();

                    // get V9 height map
                    for (int y = 0; y <= SharedConst.ADT_CELL_SIZE; y++)
                    {
                        int cy = (int)mcnk.IndexY * SharedConst.ADT_CELL_SIZE + y;
                        for (int x = 0; x <= SharedConst.ADT_CELL_SIZE; x++)
                        {
                            int cx = (int)mcnk.IndexX * SharedConst.ADT_CELL_SIZE + x;
                            V9[cy][cx] += mcvt.HeightMap[y * (SharedConst.ADT_CELL_SIZE * 2 + 1) + x];
                        }
                    }
                    // get V8 height map
                    for (int y = 0; y < SharedConst.ADT_CELL_SIZE; y++)
                    {
                        int cy = (int)mcnk.IndexY * SharedConst.ADT_CELL_SIZE + y;
                        for (int x = 0; x < SharedConst.ADT_CELL_SIZE; x++)
                        {
                            int cx = (int)mcnk.IndexX * SharedConst.ADT_CELL_SIZE + x;
                            V8[cy][cx] += mcvt.HeightMap[y * (SharedConst.ADT_CELL_SIZE * 2 + 1) + SharedConst.ADT_CELL_SIZE + 1 + x];
                        }
                    }
                }

                // Liquid data
                if (mcnk.MCLQCount > 8)
                {
                    FileChunk lquidChunk = fileChunk.GetSubChunk("MCLQ");
                    if (lquidChunk != null)
                    {
                        MCLQ liquid = lquidChunk.As<MCLQ>();
                        int count = 0;
                        for (int y = 0; y < SharedConst.ADT_CELL_SIZE; ++y)
                        {
                            int cy = (int)mcnk.IndexY * SharedConst.ADT_CELL_SIZE + y;
                            for (int x = 0; x < SharedConst.ADT_CELL_SIZE; ++x)
                            {
                                int cx = (int)mcnk.IndexX * SharedConst.ADT_CELL_SIZE + x;
                                if (liquid.Flags[y][x] != 0x0F)
                                {
                                    LiquidSnow[cy][cx] = true;
                                    if (!ignoreDeepWater && Convert.ToBoolean(liquid.Flags[y][x] & (1 << 7)))
                                        LiquidFlags[mcnk.IndexY][mcnk.IndexX] |= LiquidHeaderTypeFlags.DarkWater;
                                    ++count;
                                }
                            }
                        }

                        if (mcnk.Flags.HasAnyFlag(MCNKFlags.LiquidRiver))
                        {
                            LiquidEntries[mcnk.IndexY][mcnk.IndexX] = 1;
                            LiquidFlags[mcnk.IndexY][mcnk.IndexX] |= LiquidHeaderTypeFlags.Water;            // water
                        }
                        if (mcnk.Flags.HasAnyFlag(MCNKFlags.LiquidOcean))
                        {
                            LiquidEntries[mcnk.IndexY][mcnk.IndexX] = 2;
                            LiquidFlags[mcnk.IndexY][mcnk.IndexX] |= LiquidHeaderTypeFlags.Ocean;            // ocean
                        }
                        if (mcnk.Flags.HasAnyFlag(MCNKFlags.LiquidMagma))
                        {
                            LiquidEntries[mcnk.IndexY][mcnk.IndexX] = 3;
                            LiquidFlags[mcnk.IndexY][mcnk.IndexX] |= LiquidHeaderTypeFlags.Magma;            // magma
                        }
                        if (mcnk.Flags.HasAnyFlag(MCNKFlags.LiquidSlime))
                        {
                            LiquidEntries[mcnk.IndexY][mcnk.IndexX] = 4;
                            LiquidFlags[mcnk.IndexY][mcnk.IndexX] |= LiquidHeaderTypeFlags.Slime;            // slime
                        }

                        if (count == 0 && LiquidFlags[mcnk.IndexY][mcnk.IndexX] != 0)
                            Console.WriteLine("Wrong liquid detect in MCLQ chunk");

                        for (int y = 0; y <= SharedConst.ADT_CELL_SIZE; ++y)
                        {
                            int cy = (int)mcnk.IndexY * SharedConst.ADT_CELL_SIZE + y;
                            for (int x = 0; x <= SharedConst.ADT_CELL_SIZE; ++x)
                            {
                                int cx = (int)mcnk.IndexX * SharedConst.ADT_CELL_SIZE + x;
                                LiquidHeight[cy][cx] = liquid.Liquid[y][x].Height;
                            }
                        }
                    }
                }

                // Hole data
                if (!mcnk.Flags.HasAnyFlag(MCNKFlags.HighResHoles))
                {
                    uint hole = mcnk.HolesLowRes;
                    if (hole != 0)
                        if (TransformToHighRes((ushort)hole, Holes[mcnk.IndexY][mcnk.IndexX]))
                            hasHoles = true;
                }
                else
                {
                    Buffer.BlockCopy(mcnk.HighResHoles, 0, Holes[mcnk.IndexY][mcnk.IndexX], 0, 8);
                    if (BitConverter.ToUInt64(Holes[mcnk.IndexY][mcnk.IndexX], 0) != 0)
                        hasHoles = true;
                }
            }

            // Get liquid map for grid (in WOTLK used MH2O chunk)
            FileChunk chunkMH2O = adt.GetChunk("MH2O");
            if (chunkMH2O != null)
            {
                MH2O h2o = chunkMH2O.As<MH2O>();
                for (int i = 0; i < SharedConst.ADT_CELLS_PER_GRID; i++)
                {
                    for (int j = 0; j < SharedConst.ADT_CELLS_PER_GRID; j++)
                    {
                        MH2OInstance? h = h2o.GetLiquidInstance(i, j);
                        if (!h.HasValue)
                            continue;

                        MH2OInstance adtLiquidHeader = h.Value;
                        MH2OChunkAttribute? attrs = h2o.GetLiquidAttributes(i, j);

                        int count = 0;
                        ulong existsMask = h2o.GetLiquidExistsBitmap(adtLiquidHeader);
                        for (int y = 0; y < adtLiquidHeader.GetHeight(); y++)
                        {
                            int cy = i * SharedConst.ADT_CELL_SIZE + y + adtLiquidHeader.GetOffsetY();
                            for (int x = 0; x < adtLiquidHeader.GetWidth(); x++)
                            {
                                int cx = j * SharedConst.ADT_CELL_SIZE + x + adtLiquidHeader.GetOffsetX();
                                if (Convert.ToBoolean(existsMask & 1))
                                {
                                    LiquidSnow[cy][cx] = true;
                                    ++count;
                                }
                                existsMask >>= 1;
                            }
                        }

                        LiquidEntries[i][j] = h2o.GetLiquidType(adtLiquidHeader);
                        if (LiquidEntries[i][j] != 0)
                        {
                            var liquidTypeRecord = LiquidTypeStorage[LiquidEntries[i][j]];
                            switch ((LiquidType)liquidTypeRecord.SoundBank)
                            {
                                case LiquidType.Water:
                                    LiquidFlags[i][j] |= LiquidHeaderTypeFlags.Water;
                                    break;
                                case LiquidType.Ocean:
                                    LiquidFlags[i][j] |= LiquidHeaderTypeFlags.Ocean;
                                    if (!ignoreDeepWater && attrs.Value.Deep != 0)
                                        LiquidFlags[i][j] |= LiquidHeaderTypeFlags.DarkWater;
                                    break;
                                case LiquidType.Magma:
                                    LiquidFlags[i][j] |= LiquidHeaderTypeFlags.Magma;
                                    break;
                                case LiquidType.Slime:
                                    LiquidFlags[i][j] |= LiquidHeaderTypeFlags.Slime;
                                    break;
                                default:
                                    Console.WriteLine($"\nCan't find Liquid type {adtLiquidHeader.LiquidType} for map {mapName}\nchunk {i},{j}\n");
                                    break;
                            }

                            if (count == 0 && LiquidFlags[i][j] != 0)
                                Console.WriteLine("Wrong liquid detect in MH2O chunk");
                        }
                        else
                            Console.WriteLine($"LiquidEntries is 0 for [{i}][{j}] ({i * j})");

                        int pos = 0;
                        for (int y = 0; y <= adtLiquidHeader.GetHeight(); y++)
                        {
                            int cy = i * SharedConst.ADT_CELL_SIZE + y + adtLiquidHeader.GetOffsetY();
                            for (int x = 0; x <= adtLiquidHeader.GetWidth(); x++)
                            {
                                int cx = j * SharedConst.ADT_CELL_SIZE + x + adtLiquidHeader.GetOffsetX();

                                LiquidHeight[cy][cx] = h2o.GetLiquidHeight(adtLiquidHeader, i, j, pos);

                                pos++;
                            }
                        }
                    }
                }
            }

            FileChunk chunkMFBO = adt.GetChunk("MFBO");
            if (chunkMFBO != null)
            {
                MFBO mfbo = chunkMFBO.As<MFBO>();
                for (var i = 0; i < 3; ++i)
                {
                    FlightBoxMax[i][0] = mfbo.Max.coords[0 + i * 3];
                    FlightBoxMax[i][1] = mfbo.Max.coords[1 + i * 3];
                    FlightBoxMax[i][2] = mfbo.Max.coords[2 + i * 3];

                    FlightBoxMin[i][0] = mfbo.Min.coords[0 + i * 3];
                    FlightBoxMin[i][1] = mfbo.Min.coords[1 + i * 3];
                    FlightBoxMin[i][2] = mfbo.Min.coords[2 + i * 3];
                }
                hasFlightBox = true;
            }

            //============================================
            // Try pack area data
            //============================================
            bool fullAreaData = false;
            uint areaId = AreaIDs[0][0];
            for (int y = 0; y < SharedConst.ADT_CELLS_PER_GRID; ++y)
            {
                for (int x = 0; x < SharedConst.ADT_CELLS_PER_GRID; ++x)
                {
                    if (AreaIDs[y][x] != areaId)
                    {
                        fullAreaData = true;
                        break;
                    }
                }
            }

            map.areaMapOffset = 44;
            map.areaMapSize = 8;

            MapAreaHeader areaHeader;
            areaHeader.fourcc = SharedConst.MAP_AREA_MAGIC;
            areaHeader.flags = 0;
            if (fullAreaData)
            {
                areaHeader.gridArea = 0;
                map.areaMapSize += 512;
            }
            else
            {
                areaHeader.flags |= AreaHeaderFlags.NoArea;
                areaHeader.gridArea = (ushort)areaId;
            }

            //============================================
            // Try pack height data
            //============================================
            float maxHeight = -20000;
            float minHeight = 20000;
            for (int y = 0; y < SharedConst.ADT_GRID_SIZE; y++)
            {
                for (int x = 0; x < SharedConst.ADT_GRID_SIZE; x++)
                {
                    float h = V8[y][x];
                    if (maxHeight < h)
                        maxHeight = h;
                    if (minHeight > h)
                        minHeight = h;
                }
            }
            for (int y = 0; y <= SharedConst.ADT_GRID_SIZE; y++)
            {
                for (int x = 0; x <= SharedConst.ADT_GRID_SIZE; x++)
                {
                    float h = V9[y][x];
                    if (maxHeight < h)
                        maxHeight = h;
                    if (minHeight > h)
                        minHeight = h;
                }
            }

            // Check for allow limit minimum height (not store height in deep ochean - allow save some memory)
            if (minHeight < -2000.0f)
            {
                for (int y = 0; y < SharedConst.ADT_GRID_SIZE; y++)
                    for (int x = 0; x < SharedConst.ADT_GRID_SIZE; x++)
                        if (V8[y][x] < -2000.0f)
                            V8[y][x] = -2000.0f;
                for (int y = 0; y <= SharedConst.ADT_GRID_SIZE; y++)
                    for (int x = 0; x <= SharedConst.ADT_GRID_SIZE; x++)
                        if (V9[y][x] < -2000.0f)
                            V9[y][x] = -2000.0f;

                if (minHeight < -2000.0f)
                    minHeight = -2000.0f;
                if (maxHeight < -2000.0f)
                    maxHeight = -2000.0f;
            }

            map.heightMapOffset = map.areaMapOffset + map.areaMapSize;
            map.heightMapSize = (uint)Marshal.SizeOf<MapHeightHeader>();

            MapHeightHeader heightHeader;
            heightHeader.fourcc = SharedConst.MAP_HEIGHT_MAGIC;
            heightHeader.flags = 0;
            heightHeader.gridHeight = minHeight;
            heightHeader.gridMaxHeight = maxHeight;

            if (maxHeight == minHeight)
                heightHeader.flags |= HeightHeaderFlags.NoHeight;

            // Not need store if flat surface
            if ((maxHeight - minHeight) < 0.005f)
                heightHeader.flags |= HeightHeaderFlags.NoHeight;

            if (hasFlightBox)
            {
                heightHeader.flags |= HeightHeaderFlags.HasFlightBounds;
                map.heightMapSize += 18 + 18;
            }

            // Try store as packed in uint16 or uint8 values
            if (!heightHeader.flags.HasFlag(HeightHeaderFlags.NoHeight))
            {
                float step = 0;
                // Try Store as uint values
                if (true)//CONF_allow_float_to_int
                {
                    float diff = maxHeight - minHeight;
                    if (diff < 2.0f)      // As uint8 (max accuracy = CONF_float_to_int8_limit/256)
                    {
                        heightHeader.flags |= HeightHeaderFlags.AsInt8;
                        step = 255 / diff;
                    }
                    else if (diff < 2048.0f)  // As uint16 (max accuracy = CONF_float_to_int16_limit/65536)
                    {
                        heightHeader.flags |= HeightHeaderFlags.AsInt16;
                        step = 65535 / diff;
                    }
                }

                // Pack it to int values if need
                if (heightHeader.flags.HasFlag(HeightHeaderFlags.AsInt8))
                {
                    for (int y = 0; y < SharedConst.ADT_GRID_SIZE; y++)
                        for (int x = 0; x < SharedConst.ADT_GRID_SIZE; x++)
                            UInt8_V8[y][x] = (byte)((V8[y][x] - minHeight) * step + 0.5f);
                    for (int y = 0; y <= SharedConst.ADT_GRID_SIZE; y++)
                        for (int x = 0; x <= SharedConst.ADT_GRID_SIZE; x++)
                            UInt8_V9[y][x] = (byte)((V9[y][x] - minHeight) * step + 0.5f);
                    map.heightMapSize += 16641 + 16384;
                }
                else if (heightHeader.flags.HasFlag(HeightHeaderFlags.AsInt16))
                {
                    for (int y = 0; y < SharedConst.ADT_GRID_SIZE; y++)
                        for (int x = 0; x < SharedConst.ADT_GRID_SIZE; x++)
                            UInt16_V8[y][x] = (ushort)((V8[y][x] - minHeight) * step + 0.5f);

                    for (int y = 0; y <= SharedConst.ADT_GRID_SIZE; y++)
                        for (int x = 0; x <= SharedConst.ADT_GRID_SIZE; x++)
                            UInt16_V9[y][x] = (ushort)((V9[y][x] - minHeight) * step + 0.5f);

                    map.heightMapSize += 33282 + 32768;
                }
                else
                    map.heightMapSize += 66564 + 65536;
            }

            //============================================
            // Pack liquid data
            //============================================
            ushort firstLiquidType = LiquidEntries[0][0];
            LiquidHeaderTypeFlags firstLiquidFlag = LiquidFlags[0][0];
            bool fullType = false;
            for (int y = 0; y < SharedConst.ADT_CELLS_PER_GRID; y++)
            {
                for (int x = 0; x < SharedConst.ADT_CELLS_PER_GRID; x++)
                {
                    if (LiquidEntries[y][x] != firstLiquidType || LiquidFlags[y][x] != firstLiquidFlag)
                    {
                        fullType = true;
                        y = SharedConst.ADT_CELLS_PER_GRID;
                        break;
                    }
                }
            }

            MapLiquidHeader mapLiquidHeader = new();

            // no water data (if all grid have 0 liquid type)
            if (firstLiquidFlag == 0 && !fullType)
            {
                // No liquid data
                map.liquidMapOffset = 0;
                map.liquidMapSize = 0;
            }
            else
            {
                int minX = 255, minY = 255;
                int maxX = 0, maxY = 0;
                maxHeight = -20000;
                minHeight = 20000;
                for (int y = 0; y < SharedConst.ADT_GRID_SIZE; y++)
                {
                    for (int x = 0; x < SharedConst.ADT_GRID_SIZE; x++)
                    {
                        if (LiquidSnow[y][x])
                        {
                            if (minX > x)
                                minX = x;
                            if (maxX < x)
                                maxX = x;
                            if (minY > y)
                                minY = y;
                            if (maxY < y)
                                maxY = y;
                            float h = LiquidHeight[y][x];
                            if (maxHeight < h)
                                maxHeight = h;
                            if (minHeight > h)
                                minHeight = h;
                        }
                        else
                            LiquidHeight[y][x] = -2000.0f;
                    }
                }
                map.liquidMapOffset = map.heightMapOffset + map.heightMapSize;
                map.liquidMapSize = (uint)Marshal.SizeOf<MapLiquidHeader>();

                mapLiquidHeader.fourcc = SharedConst.MAP_LIQUID_MAGIC;
                mapLiquidHeader.flags = LiquidHeaderFlags.None;
                mapLiquidHeader.liquidType = 0;
                mapLiquidHeader.offsetX = (byte)minX;
                mapLiquidHeader.offsetY = (byte)minY;
                mapLiquidHeader.width = (byte)(maxX - minX + 1 + 1);
                mapLiquidHeader.height = (byte)(maxY - minY + 1 + 1);
                mapLiquidHeader.liquidLevel = minHeight;

                if (maxHeight == minHeight)
                    mapLiquidHeader.flags |= LiquidHeaderFlags.NoHeight;

                // Not need store if flat surface
                if ((maxHeight - minHeight) < 0.001f)
                    mapLiquidHeader.flags |= LiquidHeaderFlags.NoHeight;

                if (!fullType)
                    mapLiquidHeader.flags |= LiquidHeaderFlags.NoType;

                if (mapLiquidHeader.flags.HasFlag(LiquidHeaderFlags.NoType))
                {
                    mapLiquidHeader.liquidFlags = firstLiquidFlag;
                    mapLiquidHeader.liquidType = firstLiquidType;
                }
                else
                    map.liquidMapSize += 512 + 256;

                if (!mapLiquidHeader.flags.HasFlag(LiquidHeaderFlags.NoHeight))
                    map.liquidMapSize += (uint)(sizeof(float) * mapLiquidHeader.width * mapLiquidHeader.height);
            }

            if (hasHoles)
            {
                if (map.liquidMapOffset != 0)
                    map.holesOffset = map.liquidMapOffset + map.liquidMapSize;
                else
                    map.holesOffset = map.heightMapOffset + map.heightMapSize;

                map.holesSize = 2048;
            }
            else
            {
                map.holesOffset = 0;
                map.holesSize = 0;
            }

            // Ok all data prepared - store it
            using (BinaryWriter binaryWriter = new(File.Open(outputPath, FileMode.Create, FileAccess.Write)))
            {
                binaryWriter.WriteStruct(map);
                // Store area data
                binaryWriter.WriteStruct(areaHeader);
                if (!areaHeader.flags.HasFlag(AreaHeaderFlags.NoArea))
                {
                    for (var x = 0; x < AreaIDs.Length; ++x)
                        for (var y = 0; y < AreaIDs[x].Length; ++y)
                            binaryWriter.Write(AreaIDs[x][y]);
                }

                // Store height data
                binaryWriter.WriteStruct(heightHeader);
                if (!heightHeader.flags.HasFlag(HeightHeaderFlags.NoHeight))
                {
                    if (heightHeader.flags.HasFlag(HeightHeaderFlags.AsInt16))
                    {
                        for (var x = 0; x < UInt16_V9.Length; ++x)
                            for (var y = 0; y < UInt16_V9[x].Length; ++y)
                                binaryWriter.Write(UInt16_V9[x][y]);

                        for (var x = 0; x < UInt16_V8.Length; ++x)
                            for (var y = 0; y < UInt16_V8[x].Length; ++y)
                                binaryWriter.Write(UInt16_V8[x][y]);
                    }
                    else if (heightHeader.flags.HasFlag(HeightHeaderFlags.AsInt8))
                    {
                        for (var x = 0; x < UInt8_V9.Length; ++x)
                            for (var y = 0; y < UInt8_V9[x].Length; ++y)
                                binaryWriter.Write(UInt8_V9[x][y]);

                        for (var x = 0; x < UInt8_V8.Length; ++x)
                            for (var y = 0; y < UInt8_V8[x].Length; ++y)
                                binaryWriter.Write(UInt8_V8[x][y]);
                    }
                    else
                    {
                        for (var x = 0; x < V9.Length; ++x)
                            for (var y = 0; y < V9[x].Length; ++y)
                                binaryWriter.Write(V9[x][y]);

                        for (var x = 0; x < V8.Length; ++x)
                            for (var y = 0; y < V8[x].Length; ++y)
                                binaryWriter.Write(V8[x][y]);
                    }
                }

                if (heightHeader.flags.HasFlag(HeightHeaderFlags.HasFlightBounds))
                {
                    for (var x = 0; x < 3; ++x)
                        for (var y = 0; y < 3; ++y)
                            binaryWriter.Write(FlightBoxMax[x][y]);

                    for (var x = 0; x < 3; ++x)
                        for (var y = 0; y < 3; ++y)
                            binaryWriter.Write(FlightBoxMin[x][y]);
                }

                // Store liquid data if need
                if (map.liquidMapOffset != 0)
                {
                    binaryWriter.WriteStruct(mapLiquidHeader);
                    if (!mapLiquidHeader.flags.HasFlag(LiquidHeaderFlags.NoType))
                    {
                        for (var x = 0; x < LiquidEntries.Length; ++x)
                            for (var y = 0; y < LiquidEntries[x].Length; ++y)
                                binaryWriter.Write(LiquidEntries[x][y]);

                        for (var x = 0; x < LiquidFlags.Length; ++x)
                            for (var y = 0; y < LiquidFlags[x].Length; ++y)
                                binaryWriter.Write((byte)LiquidFlags[x][y]);
                    }

                    if (!mapLiquidHeader.flags.HasFlag(LiquidHeaderFlags.NoHeight))
                    {
                        for (int y = 0; y < mapLiquidHeader.height; y++)
                            for (int x = 0; x < mapLiquidHeader.width; x++)
                                binaryWriter.Write(LiquidHeight[y + mapLiquidHeader.offsetY][x + mapLiquidHeader.offsetX]);
                    }
                }

                // store hole data
                if (hasHoles)
                {
                    for (var x = 0; x < Holes.Length; ++x)
                        for (var y = 0; y < Holes[x].Length; ++y)
                            for (var z = 0; z < Holes[x][y].Length; ++z)
                                binaryWriter.Write(Holes[x][y][z]);
                }
            }

            return true;
        }

        public static bool IsDeepWaterIgnored(uint mapId, int x, int y)
        {
            if (mapId == 0)
            {
                //                                                                                                GRID(39, 24) || GRID(39, 25) || GRID(39, 26) ||
                //                                                                                                GRID(40, 24) || GRID(40, 25) || GRID(40, 26) ||
                //GRID(41, 18) || GRID(41, 19) || GRID(41, 20) || GRID(41, 21) || GRID(41, 22) || GRID(41, 23) || GRID(41, 24) || GRID(41, 25) || GRID(41, 26) ||
                //GRID(42, 18) || GRID(42, 19) || GRID(42, 20) || GRID(42, 21) || GRID(42, 22) || GRID(42, 23) || GRID(42, 24) || GRID(42, 25) || GRID(42, 26) ||
                //GRID(43, 18) || GRID(43, 19) || GRID(43, 20) || GRID(43, 21) || GRID(43, 22) || GRID(43, 23) || GRID(43, 24) || GRID(43, 25) || GRID(43, 26) ||
                //GRID(44, 18) || GRID(44, 19) || GRID(44, 20) || GRID(44, 21) || GRID(44, 22) || GRID(44, 23) || GRID(44, 24) || GRID(44, 25) || GRID(44, 26) ||
                //GRID(45, 18) || GRID(45, 19) || GRID(45, 20) || GRID(45, 21) || GRID(45, 22) || GRID(45, 23) || GRID(45, 24) || GRID(45, 25) || GRID(45, 26) ||
                //GRID(46, 18) || GRID(46, 19) || GRID(46, 20) || GRID(46, 21) || GRID(46, 22) || GRID(46, 23) || GRID(46, 24) || GRID(46, 25) || GRID(46, 26)

                // Vashj'ir grids completely ignore fatigue
                return (x >= 39 && x <= 40 && y >= 24 && y <= 26) || (x >= 41 && x <= 46 && y >= 18 && y <= 26);
            }

            if (mapId == 1)
            {
                // GRID(43, 39) || GRID(43, 40)
                // Thousand Needles
                return x == 43 && (y == 39 || y == 40);
            }

            return false;
        }

        static ushort[][] AreaIDs = new ushort[SharedConst.ADT_CELLS_PER_GRID][];

        static float[][] V8 = new float[SharedConst.ADT_GRID_SIZE][];
        static float[][] V9 = new float[SharedConst.ADT_GRID_SIZE + 1][];
        static ushort[][] UInt16_V8 = new ushort[SharedConst.ADT_GRID_SIZE][];
        static ushort[][] UInt16_V9 = new ushort[SharedConst.ADT_GRID_SIZE + 1][];
        static byte[][] UInt8_V8 = new byte[SharedConst.ADT_GRID_SIZE][];
        static byte[][] UInt8_V9 = new byte[SharedConst.ADT_GRID_SIZE + 1][];

        static ushort[][] LiquidEntries = new ushort[SharedConst.ADT_CELLS_PER_GRID][];
        static LiquidHeaderTypeFlags[][] LiquidFlags = new LiquidHeaderTypeFlags[SharedConst.ADT_CELLS_PER_GRID][];
        static bool[][] LiquidSnow = new bool[SharedConst.ADT_GRID_SIZE][];
        static float[][] LiquidHeight = new float[SharedConst.ADT_GRID_SIZE + 1][];
        static byte[][][] Holes = new byte[SharedConst.ADT_CELLS_PER_GRID][][];

        static short[][] FlightBoxMax = new short[3][];
        static short[][] FlightBoxMin = new short[3][];

        public static Dictionary<uint, LiquidMaterialRecord> LiquidMaterialStorage;
        public static Dictionary<uint, LiquidTypeRecord> LiquidTypeStorage;

        public struct MapFileHeader
        {
            public uint mapMagic;
            public uint versionMagic;
            public uint buildMagic;
            public uint areaMapOffset;
            public uint areaMapSize;
            public uint heightMapOffset;
            public uint heightMapSize;
            public uint liquidMapOffset;
            public uint liquidMapSize;
            public uint holesOffset;
            public uint holesSize;
        }

        public struct MapAreaHeader
        {
            public uint fourcc;
            public AreaHeaderFlags flags;
            public ushort gridArea;
        }

        public struct MapHeightHeader
        {
            public uint fourcc;
            public HeightHeaderFlags flags;
            public float gridHeight;
            public float gridMaxHeight;
        }
    }

    public struct MapLiquidHeader
    {
        public uint fourcc;
        public LiquidHeaderFlags flags;
        public LiquidHeaderTypeFlags liquidFlags;
        public ushort liquidType;
        public byte offsetX;
        public byte offsetY;
        public byte width;
        public byte height;
        public float liquidLevel;
    }

    interface IMapStruct
    {
        void Read(byte[] data);
    }
}
