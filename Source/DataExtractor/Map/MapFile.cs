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
using System;
using System.IO;
using System.Runtime.InteropServices;
using DataExtractor.CASCLib;
using System.Collections.Generic;
using DataExtractor.Framework.ClientReader;

namespace DataExtractor
{
    class MapFile
    {
        public static int ADT_CELLS_PER_GRID = 16;
        public static int ADT_CELL_SIZE = 8;
        public static int ADT_GRID_SIZE = (ADT_CELLS_PER_GRID * ADT_CELL_SIZE);

        const uint MAP_MAGIC = 0x5350414D; //"MAPS";
        const uint MAP_VERSION_MAGIC = 0x392E3176; //"v1.9";
        const uint MAP_AREA_MAGIC = 0x41455241; //"AREA";
        const uint MAP_HEIGHT_MAGIC = 0x5447484D; //"MHGT";
        const uint MAP_LIQUID_MAGIC = 0x51494C4D; //"MLIQ";

        static MapFile()
        {
            for (var i = 0; i < ADT_CELLS_PER_GRID; ++i)
            {
                area_ids[i] = new ushort[ADT_CELLS_PER_GRID];
                liquid_entry[i] = new ushort[ADT_CELLS_PER_GRID];
                liquid_flags[i] = new byte[ADT_CELLS_PER_GRID];
            }

            for (var i = 0; i < ADT_CELLS_PER_GRID; ++i)
            {
                holes[i] = new byte[ADT_CELLS_PER_GRID][];
                for (var x = 0; x < ADT_CELLS_PER_GRID; ++x)
                    holes[i][x] = new byte[8];
            }

            for (var i = 0; i < ADT_GRID_SIZE; ++i)
            {
                V8[i] = new float[ADT_GRID_SIZE];
                uint16_V8[i] = new ushort[ADT_GRID_SIZE];
                uint8_V8[i] = new byte[ADT_GRID_SIZE];
                liquid_show[i] = new bool[ADT_GRID_SIZE];
            }

            for (var i = 0; i < ADT_GRID_SIZE + 1; ++i)
            {
                V9[i] = new float[ADT_GRID_SIZE + 1];
                uint16_V9[i] = new ushort[ADT_GRID_SIZE + 1];
                uint8_V9[i] = new byte[ADT_GRID_SIZE + 1];
                liquid_height[i] = new float[ADT_GRID_SIZE + 1];
            }

            for (var i = 0; i < 3; ++i)
            {
                flight_box_max[i] = new short[3];
                flight_box_min[i] = new short[3];
            }

            LoadRequiredDb2Files();
        }

        static bool LoadRequiredDb2Files()
        {
            liquidMaterialStorage = DBReader.Read<LiquidMaterialRecord>(1132538);
            if (liquidMaterialStorage == null)
            {
                Console.WriteLine("Fatal error: Invalid LiquidMaterial.db2 file format!");
                return false;
            }

            liquidTypeStorage = DBReader.Read<LiquidTypeRecord>(1371380);
            if (liquidTypeStorage == null)
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
            ChunkedFile adt = new ChunkedFile();

            if (!adt.loadFile(cascHandler, fileName))
                return false;

            return ConvertADT(adt, mapName, outputPath, gx, gy, cascHandler.Config.GetBuildNumber(), ignoreDeepWater);
        }

        public static bool ConvertADT(CASCHandler cascHandler, uint fileDataId, string mapName, string outputPath, int gx, int gy, bool ignoreDeepWater)
        {
            ChunkedFile adt = new ChunkedFile();

            if (!adt.loadFile(cascHandler, fileDataId, $"Map {mapName} grid [{gx},{gy}]"))
                return false;

            return ConvertADT(adt, mapName, outputPath, gx, gy, cascHandler.Config.GetBuildNumber(), ignoreDeepWater);
        }

        public static bool ConvertADT(ChunkedFile adt, string mapName, string outputPath, int gx, int gy, uint build, bool ignoreDeepWater)
        {
            // Prepare map header
            map_fileheader map;
            map.mapMagic = MAP_MAGIC;
            map.versionMagic = MAP_VERSION_MAGIC;
            map.buildMagic = build;

            // Get area flags data
            for (var x = 0; x < ADT_CELLS_PER_GRID; ++x)
            {
                for (var y = 0; y < ADT_CELLS_PER_GRID; ++y)
                {
                    area_ids[x][y] = 0;
                    liquid_entry[x][y] = 0;
                    liquid_flags[x][y] = 0;
                }
            }

            for (var x = 0; x < ADT_GRID_SIZE; ++x)
            {
                for (var y = 0; y < ADT_GRID_SIZE; ++y)
                {
                    V8[x][y] = 0;
                    liquid_show[x][y] = false;
                }
            }

            for (var x = 0; x < ADT_GRID_SIZE + 1; ++x)
                for (var y = 0; y < ADT_GRID_SIZE + 1; ++y)
                    V9[x][y] = 0;

            for (var x = 0; x < ADT_CELLS_PER_GRID; ++x)
                for (var y = 0; y < ADT_CELLS_PER_GRID; ++y)
                    for (var z = 0; z < 8; ++z)
                        holes[x][y][z] = 0;

            bool hasHoles = false;
            bool hasFlightBox = false;

            foreach (var fileChunk in adt.chunks.LookupByKey("MCNK"))
            {
                adt_MCNK mcnk = fileChunk.As<adt_MCNK>();

                // Area data
                area_ids[mcnk.iy][mcnk.ix] = (ushort)mcnk.areaid;

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
                for (int y = 0; y <= ADT_CELL_SIZE; y++)
                {
                    int cy = (int)mcnk.iy * ADT_CELL_SIZE + y;
                    for (int x = 0; x <= ADT_CELL_SIZE; x++)
                    {
                        int cx = (int)mcnk.ix * ADT_CELL_SIZE + x;
                        V9[cy][cx] = mcnk.ypos;
                    }
                }

                for (int y = 0; y < ADT_CELL_SIZE; y++)
                {
                    int cy = (int)mcnk.iy * ADT_CELL_SIZE + y;
                    for (int x = 0; x < ADT_CELL_SIZE; x++)
                    {
                        int cx = (int)mcnk.ix * ADT_CELL_SIZE + x;
                        V8[cy][cx] = mcnk.ypos;
                    }
                }

                // Get custom height
                FileChunk chunk = fileChunk.GetSubChunk("MCVT");
                if (chunk != null)
                {
                    adt_MCVT mcvt = chunk.As<adt_MCVT>();

                    // get V9 height map
                    for (int y = 0; y <= ADT_CELL_SIZE; y++)
                    {
                        int cy = (int)mcnk.iy * ADT_CELL_SIZE + y;
                        for (int x = 0; x <= ADT_CELL_SIZE; x++)
                        {
                            int cx = (int)mcnk.ix * ADT_CELL_SIZE + x;
                            V9[cy][cx] += mcvt.height_map[y * (ADT_CELL_SIZE * 2 + 1) + x];
                        }
                    }
                    // get V8 height map
                    for (int y = 0; y < ADT_CELL_SIZE; y++)
                    {
                        int cy = (int)mcnk.iy * ADT_CELL_SIZE + y;
                        for (int x = 0; x < ADT_CELL_SIZE; x++)
                        {
                            int cx = (int)mcnk.ix * ADT_CELL_SIZE + x;
                            V8[cy][cx] += mcvt.height_map[y * (ADT_CELL_SIZE * 2 + 1) + ADT_CELL_SIZE + 1 + x];
                        }
                    }
                }

                // Liquid data
                if (mcnk.sizeMCLQ > 8)
                {
                    FileChunk LiquidChunk = fileChunk.GetSubChunk("MCLQ");
                    if (LiquidChunk != null)
                    {
                        adt_MCLQ liquid = LiquidChunk.As<adt_MCLQ>();
                        int count = 0;
                        for (int y = 0; y < ADT_CELL_SIZE; ++y)
                        {
                            int cy = (int)mcnk.iy * ADT_CELL_SIZE + y;
                            for (int x = 0; x < ADT_CELL_SIZE; ++x)
                            {
                                int cx = (int)mcnk.ix * ADT_CELL_SIZE + x;
                                if (liquid.flags[y][x] != 0x0F)
                                {
                                    liquid_show[cy][cx] = true;
                                    if (!ignoreDeepWater && Convert.ToBoolean(liquid.flags[y][x] & (1 << 7)))
                                        liquid_flags[mcnk.iy][mcnk.ix] |= (byte)LiquidTypeMask.DarkWater;
                                    ++count;
                                }
                            }
                        }

                        uint c_flag = mcnk.flags;
                        if (Convert.ToBoolean(c_flag & (1 << 2)))
                        {
                            liquid_entry[mcnk.iy][mcnk.ix] = 1;
                            liquid_flags[mcnk.iy][mcnk.ix] |= (byte)LiquidTypeMask.Water;            // water
                        }
                        if (Convert.ToBoolean(c_flag & (1 << 3)))
                        {
                            liquid_entry[mcnk.iy][mcnk.ix] = 2;
                            liquid_flags[mcnk.iy][mcnk.ix] |= (byte)LiquidTypeMask.Ocean;            // ocean
                        }
                        if (Convert.ToBoolean(c_flag & (1 << 4)))
                        {
                            liquid_entry[mcnk.iy][mcnk.ix] = 3;
                            liquid_flags[mcnk.iy][mcnk.ix] |= (byte)LiquidTypeMask.Magma;            // magma/slime
                        }

                        if (count == 0 && liquid_flags[mcnk.iy][mcnk.ix] != 0)
                            Console.WriteLine("Wrong liquid detect in MCLQ chunk");

                        for (int y = 0; y <= ADT_CELL_SIZE; ++y)
                        {
                            int cy = (int)mcnk.iy * ADT_CELL_SIZE + y;
                            for (int x = 0; x <= ADT_CELL_SIZE; ++x)
                            {
                                int cx = (int)mcnk.ix * ADT_CELL_SIZE + x;
                                liquid_height[cy][cx] = liquid.liquid[y][x].height;
                            }
                        }
                    }
                }

                // Hole data
                if (!Convert.ToBoolean(mcnk.flags & 0x10000))
                {
                    uint hole = mcnk.holes;
                    if (hole != 0)
                        if (TransformToHighRes((ushort)hole, holes[mcnk.iy][mcnk.ix]))
                            hasHoles = true;
                }
                else
                {
                    Buffer.BlockCopy(mcnk.HighResHoles, 0, holes[mcnk.iy][mcnk.ix], 0, 8);
                    if (BitConverter.ToUInt64(holes[mcnk.iy][mcnk.ix], 0) != 0)
                        hasHoles = true;
                }
            }

            // Get liquid map for grid (in WOTLK used MH2O chunk)
            FileChunk chunkMH20 = adt.GetChunk("MH2O");
            if (chunkMH20 != null)
            {
                adt_MH2O h2o = chunkMH20.As<adt_MH2O>();
                for (int i = 0; i < ADT_CELLS_PER_GRID; i++)
                {
                    for (int j = 0; j < ADT_CELLS_PER_GRID; j++)
                    {
                        adt_liquid_instance? h = h2o.GetLiquidInstance(i, j);
                        if (!h.HasValue)
                            continue;

                        adt_liquid_instance adtLiquidHeader = h.Value;

                        adt_liquid_attributes attrs = h2o.GetLiquidAttributes(i, j);

                        int count = 0;
                        ulong existsMask = h2o.GetLiquidExistsBitmap(adtLiquidHeader);
                        for (int y = 0; y < adtLiquidHeader.GetHeight(); y++)
                        {
                            int cy = i * ADT_CELL_SIZE + y + adtLiquidHeader.GetOffsetY();
                            for (int x = 0; x < adtLiquidHeader.GetWidth(); x++)
                            {
                                int cx = j * ADT_CELL_SIZE + x + adtLiquidHeader.GetOffsetX();
                                if (Convert.ToBoolean(existsMask & 1))
                                {
                                    liquid_show[cy][cx] = true;
                                    ++count;
                                }
                                existsMask >>= 1;
                            }
                        }

                        liquid_entry[i][j] = h2o.GetLiquidType(adtLiquidHeader);
                        var liquidTypeRecord = liquidTypeStorage[liquid_entry[i][j]];
                        switch ((LiquidType)liquidTypeRecord.SoundBank)
                        {
                            case LiquidType.Water:
                                liquid_flags[i][j] |= (byte)LiquidTypeMask.Water;
                                break;
                            case LiquidType.Ocean:
                                liquid_flags[i][j] |= (byte)LiquidTypeMask.Ocean;
                                if (!ignoreDeepWater && attrs.Deep != 0)
                                    liquid_flags[i][j] |= (byte)LiquidTypeMask.DarkWater;
                                break;
                            case LiquidType.Magma:
                                liquid_flags[i][j] |= (byte)LiquidTypeMask.Magma;
                                break;
                            case LiquidType.Slime:
                                liquid_flags[i][j] |= (byte)LiquidTypeMask.Slime;
                                break;
                            default:
                                Console.WriteLine($"\nCan't find Liquid type {adtLiquidHeader.LiquidType} for map {mapName}\nchunk {i},{j}\n");
                                break;
                        }

                        if (count == 0 && liquid_flags[i][j] != 0)
                            Console.WriteLine("Wrong liquid detect in MH2O chunk");

                        int pos = 0;
                        for (int y = 0; y <= adtLiquidHeader.GetHeight(); y++)
                        {
                            int cy = i * ADT_CELL_SIZE + y + adtLiquidHeader.GetOffsetY();
                            for (int x = 0; x <= adtLiquidHeader.GetWidth(); x++)
                            {
                                int cx = j * ADT_CELL_SIZE + x + adtLiquidHeader.GetOffsetX();

                                liquid_height[cy][cx] = h2o.GetLiquidHeight(adtLiquidHeader, pos);

                                pos++;
                            }
                        }
                    }
                }
            }

            FileChunk chunkMFBO = adt.GetChunk("MFBO");
            if (chunkMFBO != null)
            {
                adt_MFBO mfbo = chunkMFBO.As<adt_MFBO>();
                for (var i = 0; i < 3; ++i)
                {
                    flight_box_max[i][0] = mfbo.max.coords[0 + i * 3];
                    flight_box_max[i][1] = mfbo.max.coords[1 + i * 3];
                    flight_box_max[i][2] = mfbo.max.coords[2 + i * 3];

                    flight_box_min[i][0] = mfbo.min.coords[0 + i * 3];
                    flight_box_min[i][1] = mfbo.min.coords[1 + i * 3];
                    flight_box_min[i][2] = mfbo.min.coords[2 + i * 3];
                }
                hasFlightBox = true;
            }

            //============================================
            // Try pack area data
            //============================================
            bool fullAreaData = false;
            uint areaId = area_ids[0][0];
            for (int y = 0; y < ADT_CELLS_PER_GRID; ++y)
            {
                for (int x = 0; x < ADT_CELLS_PER_GRID; ++x)
                {
                    if (area_ids[y][x] != areaId)
                    {
                        fullAreaData = true;
                        break;
                    }
                }
            }

            map.areaMapOffset = 44;
            map.areaMapSize = 8;

            map_areaHeader areaHeader;
            areaHeader.fourcc = MAP_AREA_MAGIC;
            areaHeader.flags = 0;
            if (fullAreaData)
            {
                areaHeader.gridArea = 0;
                map.areaMapSize += 512;
            }
            else
            {
                areaHeader.flags |= 0x0001;
                areaHeader.gridArea = (ushort)areaId;
            }

            //============================================
            // Try pack height data
            //============================================
            float maxHeight = -20000;
            float minHeight = 20000;
            for (int y = 0; y < ADT_GRID_SIZE; y++)
            {
                for (int x = 0; x < ADT_GRID_SIZE; x++)
                {
                    float h = V8[y][x];
                    if (maxHeight < h)
                        maxHeight = h;
                    if (minHeight > h)
                        minHeight = h;
                }
            }
            for (int y = 0; y <= ADT_GRID_SIZE; y++)
            {
                for (int x = 0; x <= ADT_GRID_SIZE; x++)
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
                for (int y = 0; y < ADT_GRID_SIZE; y++)
                    for (int x = 0; x < ADT_GRID_SIZE; x++)
                        if (V8[y][x] < -2000.0f)
                            V8[y][x] = -2000.0f;
                for (int y = 0; y <= ADT_GRID_SIZE; y++)
                    for (int x = 0; x <= ADT_GRID_SIZE; x++)
                        if (V9[y][x] < -2000.0f)
                            V9[y][x] = -2000.0f;

                if (minHeight < -2000.0f)
                    minHeight = -2000.0f;
                if (maxHeight < -2000.0f)
                    maxHeight = -2000.0f;
            }

            map.heightMapOffset = map.areaMapOffset + map.areaMapSize;
            map.heightMapSize = (uint)Marshal.SizeOf<map_heightHeader>();

            map_heightHeader heightHeader;
            heightHeader.fourcc = MAP_HEIGHT_MAGIC;
            heightHeader.flags = 0;
            heightHeader.gridHeight = minHeight;
            heightHeader.gridMaxHeight = maxHeight;

            if (maxHeight == minHeight)
                heightHeader.flags |= (uint)MapHeightFlags.NoHeight;

            // Not need store if flat surface
            if ((maxHeight - minHeight) < 0.005f)
                heightHeader.flags |= (uint)MapHeightFlags.NoHeight;

            if (hasFlightBox)
            {
                heightHeader.flags |= (uint)MapHeightFlags.HasFlightBounds;
                map.heightMapSize += 18 + 18;
            }

            // Try store as packed in uint16 or uint8 values
            if (!Convert.ToBoolean(heightHeader.flags & (uint)MapHeightFlags.NoHeight))
            {
                float step = 0;
                // Try Store as uint values
                if (true)//CONF_allow_float_to_int
                {
                    float diff = maxHeight - minHeight;
                    if (diff < 2.0f)      // As uint8 (max accuracy = CONF_float_to_int8_limit/256)
                    {
                        heightHeader.flags |= (uint)MapHeightFlags.AsInt8;
                        step = 255 / diff;
                    }
                    else if (diff < 2048.0f)  // As uint16 (max accuracy = CONF_float_to_int16_limit/65536)
                    {
                        heightHeader.flags |= (uint)MapHeightFlags.AsInt16;
                        step = 65535 / diff;
                    }
                }

                // Pack it to int values if need
                if (Convert.ToBoolean(heightHeader.flags & (uint)MapHeightFlags.AsInt8))
                {
                    for (int y = 0; y < ADT_GRID_SIZE; y++)
                        for (int x = 0; x < ADT_GRID_SIZE; x++)
                            uint8_V8[y][x] = (byte)((V8[y][x] - minHeight) * step + 0.5f);
                    for (int y = 0; y <= ADT_GRID_SIZE; y++)
                        for (int x = 0; x <= ADT_GRID_SIZE; x++)
                            uint8_V9[y][x] = (byte)((V9[y][x] - minHeight) * step + 0.5f);
                    map.heightMapSize += 16641 + 16384;
                }
                else if (Convert.ToBoolean(heightHeader.flags & (uint)MapHeightFlags.AsInt16))
                {
                    for (int y = 0; y < ADT_GRID_SIZE; y++)
                        for (int x = 0; x < ADT_GRID_SIZE; x++)
                            uint16_V8[y][x] = (ushort)((V8[y][x] - minHeight) * step + 0.5f);

                    for (int y = 0; y <= ADT_GRID_SIZE; y++)
                        for (int x = 0; x <= ADT_GRID_SIZE; x++)
                            uint16_V9[y][x] = (ushort)((V9[y][x] - minHeight) * step + 0.5f);

                    map.heightMapSize += 33282 + 32768;
                }
                else
                    map.heightMapSize += 66564 + 65536;
            }

            //============================================
            // Pack liquid data
            //============================================
            ushort firstLiquidType = liquid_entry[0][0];
            byte firstLiquidFlag = liquid_flags[0][0];
            bool fullType = false;
            for (int y = 0; y < ADT_CELLS_PER_GRID; y++)
            {
                for (int x = 0; x < ADT_CELLS_PER_GRID; x++)
                {
                    if (liquid_entry[y][x] != firstLiquidType || liquid_flags[y][x] != firstLiquidFlag)
                    {
                        fullType = true;
                        y = ADT_CELLS_PER_GRID;
                        break;
                    }
                }
            }

            map_liquidHeader mapLiquidHeader = new map_liquidHeader();

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
                for (int y = 0; y < ADT_GRID_SIZE; y++)
                {
                    for (int x = 0; x < ADT_GRID_SIZE; x++)
                    {
                        if (liquid_show[y][x])
                        {
                            if (minX > x)
                                minX = x;
                            if (maxX < x)
                                maxX = x;
                            if (minY > y)
                                minY = y;
                            if (maxY < y)
                                maxY = y;
                            float h = liquid_height[y][x];
                            if (maxHeight < h)
                                maxHeight = h;
                            if (minHeight > h)
                                minHeight = h;
                        }
                        else
                            liquid_height[y][x] = -2000.0f;
                    }
                }
                map.liquidMapOffset = map.heightMapOffset + map.heightMapSize;
                map.liquidMapSize = (uint)Marshal.SizeOf<map_liquidHeader>();
                mapLiquidHeader.fourcc = MAP_LIQUID_MAGIC;
                mapLiquidHeader.flags = 0;
                mapLiquidHeader.liquidFlags = 204; //Todo Fix me. hack to make file match TC
                mapLiquidHeader.liquidType = 0;
                mapLiquidHeader.offsetX = (byte)minX;
                mapLiquidHeader.offsetY = (byte)minY;
                mapLiquidHeader.width = (byte)(maxX - minX + 1 + 1);
                mapLiquidHeader.height = (byte)(maxY - minY + 1 + 1);
                mapLiquidHeader.liquidLevel = minHeight;

                if (maxHeight == minHeight)
                    mapLiquidHeader.flags |= 0x0002;

                // Not need store if flat surface
                if ((maxHeight - minHeight) < 0.001f)
                    mapLiquidHeader.flags |= 0x0002;

                if (!fullType)
                    mapLiquidHeader.flags |= 0x0001;

                if (Convert.ToBoolean(mapLiquidHeader.flags & 0x0001))
                {
                    mapLiquidHeader.liquidFlags = firstLiquidFlag;
                    mapLiquidHeader.liquidType = firstLiquidType;
                }
                else
                    map.liquidMapSize += 512 + 256;

                if (!Convert.ToBoolean(mapLiquidHeader.flags & 0x0002))
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
            using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(outputPath, FileMode.Create, FileAccess.Write)))
            {
                binaryWriter.WriteStruct(map);
                // Store area data
                binaryWriter.WriteStruct(areaHeader);
                if (!Convert.ToBoolean(areaHeader.flags & 0x0001))
                {
                    for (var x = 0; x < area_ids.Length; ++x)
                        for (var y = 0; y < area_ids[x].Length; ++y)
                            binaryWriter.Write(area_ids[x][y]);
                }

                // Store height data
                binaryWriter.WriteStruct(heightHeader);
                if (!Convert.ToBoolean(heightHeader.flags & (uint)MapHeightFlags.NoHeight))
                {
                    if (Convert.ToBoolean(heightHeader.flags & (uint)MapHeightFlags.AsInt16))
                    {
                        for (var x = 0; x < uint16_V9.Length; ++x)
                            for (var y = 0; y < uint16_V9[x].Length; ++y)
                                binaryWriter.Write(uint16_V9[x][y]);

                        for (var x = 0; x < uint16_V8.Length; ++x)
                            for (var y = 0; y < uint16_V8[x].Length; ++y)
                                binaryWriter.Write(uint16_V8[x][y]);
                    }
                    else if (Convert.ToBoolean(heightHeader.flags & (uint)MapHeightFlags.AsInt8))
                    {
                        for (var x = 0; x < uint8_V9.Length; ++x)
                            for (var y = 0; y < uint8_V9[x].Length; ++y)
                                binaryWriter.Write(uint8_V9[x][y]);

                        for (var x = 0; x < uint8_V8.Length; ++x)
                            for (var y = 0; y < uint8_V8[x].Length; ++y)
                                binaryWriter.Write(uint8_V8[x][y]);
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

                if (Convert.ToBoolean(heightHeader.flags & (uint)MapHeightFlags.HasFlightBounds))
                {
                    for (var x = 0; x < 3; ++x)
                        for (var y = 0; y < 3; ++y)
                            binaryWriter.Write(flight_box_max[x][y]);

                    for (var x = 0; x < 3; ++x)
                        for (var y = 0; y < 3; ++y)
                            binaryWriter.Write(flight_box_min[x][y]);
                }

                // Store liquid data if need
                if (map.liquidMapOffset != 0)
                {
                    binaryWriter.WriteStruct(mapLiquidHeader);
                    if (!Convert.ToBoolean(mapLiquidHeader.flags & 0x0001))
                    {
                        for (var x = 0; x < liquid_entry.Length; ++x)
                            for (var y = 0; y < liquid_entry[x].Length; ++y)
                                binaryWriter.Write(liquid_entry[x][y]);

                        for (var x = 0; x < liquid_flags.Length; ++x)
                            for (var y = 0; y < liquid_flags[x].Length; ++y)
                                binaryWriter.Write(liquid_flags[x][y]);
                    }

                    if (!Convert.ToBoolean(mapLiquidHeader.flags & 0x0002))
                    {
                        for (int y = 0; y < mapLiquidHeader.height; y++)
                            for (int x = 0; x < mapLiquidHeader.width; x++)
                            {
                                if (binaryWriter.BaseStream.Position == 133658)
                                {

                                }
                                binaryWriter.Write(liquid_height[y + mapLiquidHeader.offsetY][x + mapLiquidHeader.offsetX]);

                            }
                    }
                }

                // store hole data
                if (hasHoles)
                {
                    for (var x = 0; x < holes.Length; ++x)
                        for (var y = 0; y < holes[x].Length; ++y)
                            for (var z = 0; z < holes[x][y].Length; ++z)
                                binaryWriter.Write(holes[x][y][z]);
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

        static ushort[][] area_ids = new ushort[ADT_CELLS_PER_GRID][];

        static float[][] V8 = new float[ADT_GRID_SIZE][];
        static float[][] V9 = new float[ADT_GRID_SIZE + 1][];
        static ushort[][] uint16_V8 = new ushort[ADT_GRID_SIZE][];
        static ushort[][] uint16_V9 = new ushort[ADT_GRID_SIZE + 1][];
        static byte[][] uint8_V8 = new byte[ADT_GRID_SIZE][];
        static byte[][] uint8_V9 = new byte[ADT_GRID_SIZE + 1][];

        static ushort[][] liquid_entry = new ushort[ADT_CELLS_PER_GRID][];
        static byte[][] liquid_flags = new byte[ADT_CELLS_PER_GRID][];
        static bool[][] liquid_show = new bool[ADT_GRID_SIZE][];
        static float[][] liquid_height = new float[ADT_GRID_SIZE + 1][];
        static byte[][][] holes = new byte[ADT_CELLS_PER_GRID][][];

        static short[][] flight_box_max = new short[3][];
        static short[][] flight_box_min = new short[3][];

        public static Dictionary<uint, LiquidMaterialRecord> liquidMaterialStorage;
        public static Dictionary<uint, LiquidTypeRecord> liquidTypeStorage;

        public struct map_fileheader
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

        struct map_areaHeader
        {
            public uint fourcc;
            public ushort flags;
            public ushort gridArea;
        }

        public struct map_heightHeader
        {
            public uint fourcc;
            public uint flags;
            public float gridHeight;
            public float gridMaxHeight;
        }
    }

    public struct map_liquidHeader
    {
        public uint fourcc;
        public byte flags;
        public byte liquidFlags;
        public ushort liquidType;
        public byte offsetX;
        public byte offsetY;
        public byte width;
        public byte height;
        public float liquidLevel;
    }

    enum LiquidVertexFormatType : int
    {
        HeightDepth = 0,
        HeightTextureCoord = 1,
        Depth = 2,
        HeightDepthTextureCoord = 3,
        Unk4 = 4,
        Unk5 = 5
    }

    struct adt_liquid_instance
    {
        public ushort LiquidType { get; set; }             // Index from LiquidType.dbc
        public ushort LiquidVertexFormat { get; set; }
        public float MinHeightLevel { get; set; }
        public float MaxHeightLevel { get; set; }
        public byte OffsetX { get; set; }
        public byte OffsetY { get; set; }
        public byte Width { get; set; }
        public byte Height { get; set; }
        public uint OffsetExistsBitmap { get; set; }
        public uint OffsetVertexData { get; set; }
        public byte GetOffsetX() { return (byte)(LiquidVertexFormat < 42 ? OffsetX : 0); }
        public byte GetOffsetY() { return (byte)(LiquidVertexFormat < 42 ? OffsetY : 0); }
        public byte GetWidth() { return (byte)(LiquidVertexFormat < 42 ? Width : 8); }
        public byte GetHeight() { return (byte)(LiquidVertexFormat < 42 ? Height : 8); }
    }

    struct adt_liquid_attributes
    {
        public ulong Fishable;
        public ulong Deep;
    }

    interface IMapStruct
    {
        void Read(byte[] data);
    }

    class file_MVER : IMapStruct
    {
        public void Read(byte[] data)
        {
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                fourcc = reader.ReadUInt32();
                size = reader.ReadUInt32();
                ver = reader.ReadUInt32();
            }
        }

        public uint fourcc;
        public uint size;
        public uint ver;
    }

    class wdt_MPHD : IMapStruct
    {
        public void Read(byte[] data)
        {
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                fourcc = reader.ReadUInt32();
                size = reader.ReadUInt32();

                flags = reader.ReadUInt32();
                lgtFileDataID = reader.ReadUInt32();
                occFileDataID = reader.ReadUInt32();
                fogsFileDataID = reader.ReadUInt32();
                mpvFileDataID = reader.ReadUInt32();
                texFileDataID = reader.ReadUInt32();
                wdlFileDataID = reader.ReadUInt32();
                pd4FileDataID = reader.ReadUInt32();
            }
        }

        public uint fourcc;
        public uint size;

        public uint flags;
        public uint lgtFileDataID;
        public uint occFileDataID;
        public uint fogsFileDataID;
        public uint mpvFileDataID;
        public uint texFileDataID;
        public uint wdlFileDataID;
        public uint pd4FileDataID;
    }

    class wdt_MAIN : IMapStruct
    {
        public void Read(byte[] data)
        {
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                fourcc = reader.ReadUInt32();
                size = reader.ReadUInt32();

                for (var x = 0; x < 64; ++x)
                {
                    adt_list[x] = new adtData[64];

                    for (var y = 0; y < 64; ++y)
                        adt_list[x][y] = reader.Read<adtData>();
                }
            }
        }

        public uint fourcc;
        public uint size;
        public adtData[][] adt_list = new adtData[64][];

        public struct adtData
        {
            public uint flag { get; set; }
            public uint data1 { get; set; }
        }
    }

    class wdt_MAID : IMapStruct
    {
        public void Read(byte[] data)
        {
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                fourcc = reader.ReadUInt32();
                size = reader.ReadUInt32();

                for (var x = 0; x < 64; ++x)
                {
                    adt_files[x] = new adtData[64];

                    for (var y = 0; y < 64; ++y)
                        adt_files[x][y] = reader.Read<adtData>();
                }
            }
        }

        public uint fourcc;
        public uint size;
        public adtData[][] adt_files = new adtData[64][];

        public struct adtData
        {
            public uint rootADT;         // FileDataID of mapname_xx_yy.adt
            public uint obj0ADT;         // FileDataID of mapname_xx_yy_obj0.adt
            public uint obj1ADT;         // FileDataID of mapname_xx_yy_obj1.adt
            public uint tex0ADT;         // FileDataID of mapname_xx_yy_tex0.adt
            public uint lodADT;          // FileDataID of mapname_xx_yy_lod.adt
            public uint mapTexture;      // FileDataID of mapname_xx_yy.blp
            public uint mapTextureN;     // FileDataID of mapname_xx_yy_n.blp
            public uint minimapTexture;  // FileDataID of mapxx_yy.blp
        }
    }

    class adt_MCNK : IMapStruct
    {
        public void Read(byte[] data)
        {
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                fourcc = reader.ReadUInt32();
                size = reader.ReadUInt32();
                flags = reader.ReadUInt32();
                ix = reader.ReadUInt32();
                iy = reader.ReadUInt32();
                nLayers = reader.ReadUInt32();
                nDoodadRefs = reader.ReadUInt32();

                for (var i = 0; i < 8; ++i)
                    HighResHoles[i] = reader.ReadByte();

                offsMCLY = reader.ReadUInt32();        // Texture layer definitions
                offsMCRF = reader.ReadUInt32();        // A list of indices into the parent file's MDDF chunk
                offsMCAL = reader.ReadUInt32();        // Alpha maps for additional texture layers
                sizeMCAL = reader.ReadUInt32();
                offsMCSH = reader.ReadUInt32();        // Shadow map for static shadows on the terrain
                sizeMCSH = reader.ReadUInt32();
                areaid = reader.ReadUInt32();
                nMapObjRefs = reader.ReadUInt32();
                holes = reader.ReadUInt32();

                for (var i = 0; i < 2; ++i)
                    s[i] = reader.ReadUInt16();

                data1 = reader.ReadUInt32();
                data2 = reader.ReadUInt32();
                data3 = reader.ReadUInt32();
                predTex = reader.ReadUInt32();
                nEffectDoodad = reader.ReadUInt32();
                offsMCSE = reader.ReadUInt32();
                nSndEmitters = reader.ReadUInt32();
                offsMCLQ = reader.ReadUInt32();         // Liqid level (old)
                sizeMCLQ = reader.ReadUInt32();         //
                zpos = reader.ReadSingle();
                xpos = reader.ReadSingle();
                ypos = reader.ReadSingle();
                offsMCCV = reader.ReadUInt32();         // offsColorValues in WotLK
                props = reader.ReadUInt32();
                effectId = reader.ReadUInt32();
            }
        }

        public uint fourcc;
        public uint size;
        public uint flags;
        public uint ix;
        public uint iy;
        public uint nLayers;
        public uint nDoodadRefs;
        public byte[] HighResHoles = new byte[8];
        public uint offsMCLY;        // Texture layer definitions
        public uint offsMCRF;        // A list of indices into the parent file's MDDF chunk
        public uint offsMCAL;        // Alpha maps for additional texture layers
        public uint sizeMCAL;
        public uint offsMCSH;        // Shadow map for static shadows on the terrain
        public uint sizeMCSH;
        public uint areaid;
        public uint nMapObjRefs;
        public uint holes;
        public ushort[] s = new ushort[2];
        public uint data1;
        public uint data2;
        public uint data3;
        public uint predTex;
        public uint nEffectDoodad;
        public uint offsMCSE;
        public uint nSndEmitters;
        public uint offsMCLQ;         // Liqid level (old)
        public uint sizeMCLQ;         //
        public float zpos;
        public float xpos;
        public float ypos;
        public uint offsMCCV;         // offsColorValues in WotLK
        public uint props;
        public uint effectId;
    }

    class adt_MCVT : IMapStruct
    {
        public void Read(byte[] data)
        {
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                fourcc = reader.ReadUInt32();
                size = reader.ReadUInt32();

                for (var i = 0; i < height_map.Length; ++i)
                    height_map[i] = reader.ReadSingle();
            }
        }

        public uint fourcc;
        public uint size;
        public float[] height_map = new float[(MapFile.ADT_CELL_SIZE + 1) * (MapFile.ADT_CELL_SIZE + 1) + MapFile.ADT_CELL_SIZE * MapFile.ADT_CELL_SIZE];
    }

    class adt_MCLQ : IMapStruct
    {
        public void Read(byte[] data)
        {
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                fourcc = reader.ReadUInt32();
                size = reader.ReadUInt32();

                for (var x = 0; x < MapFile.ADT_CELL_SIZE + 1; ++x)
                {
                    liquid[x] = new liquid_data[MapFile.ADT_CELL_SIZE + 1];

                    for (var y = 0; y < MapFile.ADT_CELL_SIZE + 1; ++y)
                        liquid[x][y] = reader.Read<liquid_data>();
                }

                for (var x = 0; x < MapFile.ADT_CELL_SIZE; ++x)
                {
                    flags[x] = new byte[MapFile.ADT_CELL_SIZE];

                    for (var y = 0; y < MapFile.ADT_CELL_SIZE; ++y)
                        flags[x][y] = reader.ReadByte();
                }
            }
        }

        public uint fourcc;
        public uint size;

        public liquid_data[][] liquid = new liquid_data[MapFile.ADT_CELL_SIZE + 1][];

        // 1<<0 - ochen
        // 1<<1 - lava/slime
        // 1<<2 - water
        // 1<<6 - all water
        // 1<<7 - dark water
        // == 0x0F - not show liquid
        public byte[][] flags = new byte[MapFile.ADT_CELL_SIZE][];
        public byte[] data = new byte[84];

        public struct liquid_data
        {
            public uint light { get; set; }
            public float height { get; set; }
        }
    }

    class adt_MH2O : IMapStruct
    {
        public void Read(byte[] data)
        {
            _data = data;
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                fourcc = reader.ReadUInt32();
                size = reader.ReadUInt32();

                for (var x = 0; x < MapFile.ADT_CELLS_PER_GRID; ++x)
                {
                    liquid[x] = new adt_LIQUID[MapFile.ADT_CELLS_PER_GRID];

                    for (var y = 0; y < MapFile.ADT_CELLS_PER_GRID; ++y)
                        liquid[x][y] = reader.Read<adt_LIQUID>();
                }
            }
        }

        public uint fourcc;
        public uint size;
        public adt_LIQUID[][] liquid = new adt_LIQUID[MapFile.ADT_CELLS_PER_GRID][];
        byte[] _data;

        public struct adt_LIQUID
        {
            public uint OffsetInstances { get; set; }
            public uint used { get; set; }
            public uint OffsetAttributes { get; set; }
        }

        public adt_liquid_instance? GetLiquidInstance(int x, int y)
        {
            if (liquid[x][y].used != 0 && liquid[x][y].OffsetInstances != 0)
                return new BinaryReader(new MemoryStream(_data, 8 + (int)liquid[x][y].OffsetInstances, _data.Length - (8 + (int)liquid[x][y].OffsetInstances))).Read<adt_liquid_instance>();
            return null;
        }

        public adt_liquid_attributes GetLiquidAttributes(int x, int y)
        {
            if (liquid[x][y].used != 0)
            {
                if (liquid[x][y].OffsetAttributes != 0)
                    return new BinaryReader(new MemoryStream(_data, 8 + (int)liquid[x][y].OffsetAttributes, _data.Length - (8 + (int)liquid[x][y].OffsetAttributes))).Read<adt_liquid_attributes>();
                return new adt_liquid_attributes() { Fishable = 0xFFFFFFFFFFFFFFFF, Deep = 0xFFFFFFFFFFFFFFFF };
            }
            return default(adt_liquid_attributes);
        }

        public ushort GetLiquidType(adt_liquid_instance h)
        {
            if (GetLiquidVertexFormat(h) == LiquidVertexFormatType.Depth)
                return 2;

            return h.LiquidType;
        }

        public float GetLiquidHeight(adt_liquid_instance h, int pos)
        {
            if (h.OffsetVertexData == 0)
                return 0.0f;
            if (GetLiquidVertexFormat(h) == LiquidVertexFormatType.Depth)
                return 0.0f;

            switch (GetLiquidVertexFormat(h))
            {
                case LiquidVertexFormatType.HeightDepth:
                case LiquidVertexFormatType.HeightTextureCoord:
                case LiquidVertexFormatType.HeightDepthTextureCoord:
                    return BitConverter.ToSingle(_data, (int)(8 + h.OffsetVertexData + pos * 4));
                case LiquidVertexFormatType.Depth:
                    return 0.0f;
                case LiquidVertexFormatType.Unk4:
                case LiquidVertexFormatType.Unk5:
                    return BitConverter.ToSingle(_data, (int)(8 + h.OffsetVertexData + 4 + (pos * 4) * 2));
                default:
                    break;
            }

            return 0.0f;
        }

        public int GetLiquidDepth(adt_liquid_instance h, int pos)
        {
            if (h.OffsetVertexData == 0)
                return -1;

            switch (GetLiquidVertexFormat(h))
            {
                case LiquidVertexFormatType.HeightDepth:
                    return (sbyte)_data[8 + h.OffsetVertexData + (h.GetWidth() + 1) * (h.GetHeight() + 1) * 4 + pos];
                case LiquidVertexFormatType.HeightTextureCoord:
                    return 0;
                case LiquidVertexFormatType.Depth:
                    return (sbyte)_data[8 + h.OffsetVertexData + pos];
                case LiquidVertexFormatType.HeightDepthTextureCoord:
                    return (sbyte)_data[8 + h.OffsetVertexData + (h.GetWidth() + 1) * (h.GetHeight() + 1) * 8 + pos];
                case LiquidVertexFormatType.Unk4:
                    return (sbyte)_data[8 + h.OffsetVertexData + pos * 8];
                case LiquidVertexFormatType.Unk5:
                    return 0;
                default:
                    break;
            }
            return 0;
        }

        ushort? GetLiquidTextureCoordMap(adt_liquid_instance h, int pos)
        {
            if (h.OffsetVertexData == 0)
                return null;

            switch (GetLiquidVertexFormat(h))
            {
                case LiquidVertexFormatType.HeightDepth:
                case LiquidVertexFormatType.Depth:
                case LiquidVertexFormatType.Unk4:
                    return null;
                case LiquidVertexFormatType.HeightTextureCoord:
                case LiquidVertexFormatType.HeightDepthTextureCoord:
                    return BitConverter.ToUInt16(_data, (int)(8 + h.OffsetVertexData + 4 * ((h.GetWidth() + 1) * (h.GetHeight() + 1) + pos)));
                case LiquidVertexFormatType.Unk5:
                    return BitConverter.ToUInt16(_data, (int)(8 + h.OffsetVertexData + 8 * ((h.GetWidth() + 1) * (h.GetHeight() + 1) + pos)));
                default:
                    break;
            }
            return null;
        }

        public ulong GetLiquidExistsBitmap(adt_liquid_instance h)
        {
            if (h.OffsetExistsBitmap != 0)
                return BitConverter.ToUInt64(_data, (int)(8 + h.OffsetExistsBitmap));
            else
                return 0xFFFFFFFFFFFFFFFFuL;
        }

        LiquidVertexFormatType GetLiquidVertexFormat(adt_liquid_instance liquidInstance)
        {
            if (liquidInstance.LiquidVertexFormat < 42)
                return (LiquidVertexFormatType)liquidInstance.LiquidVertexFormat;

            if (liquidInstance.LiquidType == 2)
                return LiquidVertexFormatType.Depth;

            var liquidType = MapFile.liquidTypeStorage.LookupByKey(liquidInstance.LiquidType);
            if (liquidType != null)
            {
                if (MapFile.liquidMaterialStorage.ContainsKey(liquidType.MaterialID))
                    return (LiquidVertexFormatType)MapFile.liquidMaterialStorage[liquidType.MaterialID].LVF;
            }

            return (LiquidVertexFormatType)(-1);
        }
    }

    class adt_MFBO : IMapStruct
    {
        public void Read(byte[] data)
        {
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                fourcc = reader.ReadUInt32();
                size = reader.ReadUInt32();

                max = new plane();
                for (var i = 0; i < 9; ++i)
                    max.coords[i] = reader.ReadInt16();

                min = new plane();
                for (var i = 0; i < 9; ++i)
                    min.coords[i] = reader.ReadInt16();
            }
        }

        public uint fourcc;
        public uint size;
        public plane max;
        public plane min;

        public class plane
        {
            public short[] coords = new short[9];
        }
    }
}
