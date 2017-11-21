using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CASC.Handlers;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using CASC;

namespace DataExtractor
{
    class MapFile
    {
        public static int ADT_CELLS_PER_GRID = 16;
        public static int ADT_CELL_SIZE = 8;
        public static int ADT_GRID_SIZE = (ADT_CELLS_PER_GRID * ADT_CELL_SIZE);

        static uint MAP_MAGIC = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("MAPS"), 0);
        static uint MAP_VERSION_MAGIC = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("v1.8"), 0);
        static uint MAP_AREA_MAGIC = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("AREA"), 0);
        static uint MAP_HEIGHT_MAGIC = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("MHGT"), 0);
        static uint MAP_LIQUID_MAGIC = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("MLIQ"), 0);

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

        public static bool ConvertADT(CASCHandler cascHandler, string inputPath, string outputPath, int cell_y, int cell_x, uint build)
        {
            ChunkedFile adt = new ChunkedFile();
            uint time = Time.GetMSTime();
            if (!adt.loadFile(cascHandler, inputPath))
                return false;
            //Console.WriteLine($"\n Load Time: {Time.GetMSTimeDiffToNow(time)}");
            // Prepare map header
            map_fileheader map;
            map.mapMagic = MAP_MAGIC;
            map.versionMagic = MAP_VERSION_MAGIC;
            map.buildMagic = build;
            time = Time.GetMSTime();
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
                                    if (Convert.ToBoolean(liquid.flags[y][x] & (1 << 7)))
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
                        adt_liquid_header? h = h2o.getLiquidData(i, j);
                        if (!h.HasValue)
                            continue;

                        adt_liquid_header adtLiquidHeader = h.Value;

                        int count = 0;
                        ulong show = h2o.getLiquidShowMap(adtLiquidHeader);
                        for (int y = 0; y < adtLiquidHeader.height; y++)
                        {
                            int cy = i * ADT_CELL_SIZE + y + adtLiquidHeader.yOffset;
                            for (int x = 0; x < adtLiquidHeader.width; x++)
                            {
                                int cx = j * ADT_CELL_SIZE + x + adtLiquidHeader.xOffset;
                                if (Convert.ToBoolean(show & 1))
                                {
                                    liquid_show[cy][cx] = true;
                                    ++count;
                                }
                                show >>= 1;
                            }
                        }

                        liquid_entry[i][j] = adtLiquidHeader.liquidType;
                        var liquidTypeRecord = Program.liquidTypeStorage[adtLiquidHeader.liquidType];
                        switch ((LiquidType)liquidTypeRecord.LiquidType)
                        {
                            case LiquidType.Water:
                                liquid_flags[i][j] |= (byte)LiquidTypeMask.Water;
                                break;
                            case LiquidType.Ocean:
                                liquid_flags[i][j] |= (byte)LiquidTypeMask.Ocean;
                                break;
                            case LiquidType.Magma:
                                liquid_flags[i][j] |= (byte)LiquidTypeMask.Magma;
                                break;
                            case LiquidType.Slime:
                                liquid_flags[i][j] |= (byte)LiquidTypeMask.Slime;
                                break;
                            default:
                                Console.WriteLine($"\nCan't find Liquid type {adtLiquidHeader.liquidType} for map {inputPath}\nchunk {i},{j}\n");
                                break;
                        }
                        // Dark water detect
                        if ((LiquidType)liquidTypeRecord.LiquidType == LiquidType.Ocean)
                        {
                            byte[] lm = h2o.getLiquidLightMap(adtLiquidHeader);
                            if (lm == null)
                                liquid_flags[i][j] |= (byte)LiquidTypeMask.DarkWater;
                        }

                        if (count == 0 && liquid_flags[i][j] != 0)
                            Console.WriteLine("Wrong liquid detect in MH2O chunk");

                        float[] height = h2o.getLiquidHeightMap(adtLiquidHeader);
                        int pos = 0;
                        for (int y = 0; y <= adtLiquidHeader.height; y++)
                        {
                            int cy = i * ADT_CELL_SIZE + y + adtLiquidHeader.yOffset;
                            for (int x = 0; x <= adtLiquidHeader.width; x++)
                            {
                                int cx = j * ADT_CELL_SIZE + x + adtLiquidHeader.xOffset;

                                if (height != null)
                                    liquid_height[cy][cx] = height[pos];
                                else
                                    liquid_height[cy][cx] = adtLiquidHeader.heightLevel1;

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
            if (minHeight < -500.0f)
            {
                for (int y = 0; y < ADT_GRID_SIZE; y++)
                    for (int x = 0; x < ADT_GRID_SIZE; x++)
                        if (V8[y][x] < -500.0f)
                            V8[y][x] = -500.0f;
                for (int y = 0; y <= ADT_GRID_SIZE; y++)
                    for (int x = 0; x <= ADT_GRID_SIZE; x++)
                        if (V9[y][x] < -500.0f)
                            V9[y][x] = -500.0f;

                if (minHeight < -500.0f)
                    minHeight = -500.0f;
                if (maxHeight < -500.0f)
                    maxHeight = -500.0f;
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
            byte type = liquid_flags[0][0];
            bool fullType = false;
            for (int y = 0; y < ADT_CELLS_PER_GRID; y++)
            {
                for (int x = 0; x < ADT_CELLS_PER_GRID; x++)
                {
                    if (liquid_flags[y][x] != type)
                    {
                        fullType = true;
                        y = ADT_CELLS_PER_GRID;
                        break;
                    }
                }
            }

            map_liquidHeader mapLiquidHeader = new map_liquidHeader();

            // no water data (if all grid have 0 liquid type)
            if (type == 0 && !fullType)
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
                            if (minX > x) minX = x;
                            if (maxX < x) maxX = x;
                            if (minY > y) minY = y;
                            if (maxY < y) maxY = y;
                            float h = liquid_height[y][x];
                            if (maxHeight < h) maxHeight = h;
                            if (minHeight > h) minHeight = h;
                        }
                        else
                            liquid_height[y][x] = -500.0f;
                    }
                }
                map.liquidMapOffset = map.heightMapOffset + map.heightMapSize;
                map.liquidMapSize = (uint)Marshal.SizeOf<map_liquidHeader>();
                mapLiquidHeader.fourcc = MAP_LIQUID_MAGIC;
                mapLiquidHeader.flags = 0;
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
                    mapLiquidHeader.liquidType = type;
                else
                    map.liquidMapSize += 512 + 256;

                if (!Convert.ToBoolean(mapLiquidHeader.flags & 0x0002))
                    map.liquidMapSize += (uint)(sizeof(float) * mapLiquidHeader.width * mapLiquidHeader.height);
            }

            if (map.liquidMapOffset != 0)
                map.holesOffset = map.liquidMapOffset + map.liquidMapSize;
            else
                map.holesOffset = map.heightMapOffset + map.heightMapSize;

            if (hasHoles)
                map.holesSize = 2048;// (uint)(1 * holes.Length);
            else
                map.holesSize = 0;
            //Console.WriteLine($"\n Convert Time: {Time.GetMSTimeDiffToNow(time)}");
            time = Time.GetMSTime();
            // Ok all data prepared - store it
            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, true))
            {
                BinaryWriter binaryWriter = new BinaryWriter(fs);
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
                                binaryWriter.Write(liquid_height[y + mapLiquidHeader.offsetY][x + mapLiquidHeader.offsetX]);
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
            //Console.WriteLine($"\n Write Time: {Time.GetMSTimeDiffToNow(time)}");
            return true;
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

        public struct map_liquidHeader
        {
            public uint fourcc;
            public ushort flags;
            public ushort liquidType;
            public byte offsetX;
            public byte offsetY;
            public byte width;
            public byte height;
            public float liquidLevel;
        }
    }
    
    struct adt_liquid_header
    {
        public ushort liquidType;             // Index from LiquidType.dbc
        public ushort formatFlags;
        public float heightLevel1;
        public float heightLevel2;
        public byte xOffset;
        public byte yOffset;
        public byte width;
        public byte height;
        public uint offsData2a;
        public uint offsData2b;
    }

    interface IMapStruct
    {
        void Read(byte[] data);
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
                        adt_list[x][y] = reader.ReadStruct<adtData>();
                }
            }
        }

        public uint fourcc;
        public uint size;
        public adtData[][] adt_list = new adtData[64][];

        public struct adtData
        {
            public uint flag;
            public uint data1;
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
                        liquid[x][y] = reader.ReadStruct<liquid_data>();
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
        public float height1;
        public float height2;

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
            public uint light;
            public float height;
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
                        liquid[x][y] = reader.ReadStruct<adt_LIQUID>();
                }
            }
        }

        public uint fourcc;
        public uint size;
        public adt_LIQUID[][] liquid = new adt_LIQUID[MapFile.ADT_CELLS_PER_GRID][];
        byte[] _data;

        public struct adt_LIQUID
        {
            public uint offsData1;
            public uint used;
            public uint offsData2;
        }

        public adt_liquid_header? getLiquidData(int x, int y)
        {
            if (liquid[x][y].used != 0 && liquid[x][y].offsData1 != 0)
                return new BinaryReader(new MemoryStream(_data, 8 + (int)liquid[x][y].offsData1, _data.Length - (8 + (int)liquid[x][y].offsData1))).ReadStruct<adt_liquid_header>();
            return null;
        }

        public float[] getLiquidHeightMap(adt_liquid_header h)
        {
            if (Convert.ToBoolean(h.formatFlags & 0x02))
                return null;

            if (h.offsData2b != 0)
            {
                int index = 8 + (int)h.offsData2b;

                float[] value = new float[_data.Length - index];
                Buffer.BlockCopy(_data, index, value, 0, value.Length);
                return value;
            }

            return null;
        }

        public byte[] getLiquidLightMap(adt_liquid_header h)
        {
            if (Convert.ToBoolean(h.formatFlags & 0x01))
                return null;

            if (h.offsData2b != 0)
            {
                int index = (int)(8 + h.offsData2b + (h.width + 1) * (h.height + 1) * 4);
                if (Convert.ToBoolean(h.formatFlags & 0x02))
                    index = 8 + (int)h.offsData2b;

                byte[] value = new byte[_data.Length - index];
                Buffer.BlockCopy(_data, index, value, 0, value.Length);

                return value;
            }
            return null;
        }

        public ulong getLiquidShowMap(adt_liquid_header h)
        {
            if (h.offsData2a != 0)
                return BitConverter.ToUInt64(_data, 8 + (int)h.offsData2a);
            else
                return 0xFFFFFFFFFFFFFFFFu;
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
