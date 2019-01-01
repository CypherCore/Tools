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

using DataExtractor.Framework.ClientReader;
using DataExtractor.Framework.Collision;
using DataExtractor.Framework.Constants;
using DataExtractor.Framework.GameMath;
using System;
using System.Collections.Generic;
using System.IO;

namespace DataExtractor.Mmap
{
    class TerrainBuilder
    {
        public TerrainBuilder(VMapManager2 vm)
        {
            vmapManager = vm;

            liquidTypeStorage = DBReader.Read<LiquidTypeRecord>("DBFilesClient\\LiquidType.db2");
            if (liquidTypeStorage == null)
                Console.WriteLine("Fatal error: Invalid LiquidType.db2 file format!");
        }

        void getLoopVars(Spot portion, ref int loopStart, ref int loopEnd, ref int loopInc)
        {
            switch (portion)
            {
                case Spot.Entire:
                    loopStart = 0;
                    loopEnd = SharedConst.V8_SIZE_SQ;
                    loopInc = 1;
                    break;
                case Spot.Top:
                    loopStart = 0;
                    loopEnd = SharedConst.V8_SIZE;
                    loopInc = 1;
                    break;
                case Spot.Left:
                    loopStart = 0;
                    loopEnd = SharedConst.V8_SIZE_SQ - SharedConst.V8_SIZE + 1;
                    loopInc = SharedConst.V8_SIZE;
                    break;
                case Spot.Right:
                    loopStart = SharedConst.V8_SIZE - 1;
                    loopEnd = SharedConst.V8_SIZE_SQ;
                    loopInc = SharedConst.V8_SIZE;
                    break;
                case Spot.Bottom:
                    loopStart = SharedConst.V8_SIZE_SQ - SharedConst.V8_SIZE;
                    loopEnd = SharedConst.V8_SIZE_SQ;
                    loopInc = 1;
                    break;
            }
        }

        /**************************************************************************/
        public void loadMap(uint mapID, uint tileX, uint tileY, MeshData meshData, VMapManager2 vm)
        {
            if (loadMap(mapID, tileX, tileY, meshData, Spot.Entire))
            {
                loadMap(mapID, tileX + 1, tileY, meshData, Spot.Left);
                loadMap(mapID, tileX - 1, tileY, meshData, Spot.Right);
                loadMap(mapID, tileX, tileY + 1, meshData, Spot.Top);
                loadMap(mapID, tileX, tileY - 1, meshData, Spot.Bottom);
            }
        }

        /**************************************************************************/
        bool loadMap(uint mapID, uint tileX, uint tileY, MeshData meshData, Spot portion)
        {
            string mapFileName = $"maps/{mapID:D4}_{tileY:D2}_{tileX:D2}.map";
            if (!File.Exists(mapFileName))
            {
                int parentMapId = vmapManager.GetParentMapId(mapID);
                if (parentMapId != -1)
                    mapFileName = $"maps/{parentMapId:D4}_{tileY:D2}_{tileX:D2}.map";
            }

            if (!File.Exists(mapFileName))
                return false;

            using (BinaryReader reader = new BinaryReader(File.Open(mapFileName, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                map_fileheader fheader = reader.Read<map_fileheader>();
                if (fheader.versionMagic != SharedConst.MAP_VERSION_MAGIC)
                {
                    Console.WriteLine($"{mapFileName} is the wrong version, please extract new .map files");
                    return false;
                }

                bool haveTerrain = false;
                bool haveLiquid = false;

                reader.BaseStream.Seek(fheader.heightMapOffset, SeekOrigin.Begin);
                map_heightHeader hheader = reader.Read<map_heightHeader>();
                if (hheader.fourcc == 1413957709)
                {
                    haveTerrain = !Convert.ToBoolean(hheader.flags & (uint)MapHeightFlags.NoHeight);
                    haveLiquid = fheader.liquidMapOffset != 0;// && !m_skipLiquid;
                }

                // no data in this map file
                if (!haveTerrain && !haveLiquid)
                    return false;

                // data used later
                byte[][][] holes = new byte[16][][];
                for (var i = 0; i < 16; ++i)
                {
                    holes[i] = new byte[16][];
                    for (var x = 0; x < 16; ++x)
                        holes[i][x] = new byte[8];
                }

                ushort[][] liquid_entry = new ushort[16][];
                byte[][] liquid_flags = new byte[16][];
                for (var i = 0; i < 16; ++i)
                {
                    liquid_entry[i] = new ushort[16];
                    liquid_flags[i] = new byte[16];
                }

                List<int> ltriangles = new List<int>();
                List<int> ttriangles = new List<int>();

                // terrain data
                if (haveTerrain)
                {
                    float heightMultiplier;
                    float[] V9 = new float[SharedConst.V9_SIZE_SQ];
                    float[] V8 = new float[SharedConst.V8_SIZE_SQ];
                    int expected = SharedConst.V9_SIZE_SQ + SharedConst.V8_SIZE_SQ;

                    if (Convert.ToBoolean(hheader.flags & (uint)MapHeightFlags.AsInt8))
                    {
                        byte[] v9 = new byte[SharedConst.V9_SIZE_SQ];
                        byte[] v8 = new byte[SharedConst.V8_SIZE_SQ];
                        int count = 0;
                        count += reader.Read(v9, 0, SharedConst.V9_SIZE_SQ);
                        count += reader.Read(v8, 0, SharedConst.V8_SIZE_SQ);
                        if (count != expected)
                            Console.WriteLine($"TerrainBuilder.loadMap: Failed to read some data expected {expected}, read {count}");

                        heightMultiplier = (hheader.gridMaxHeight - hheader.gridHeight) / 255;

                        for (int i = 0; i < SharedConst.V9_SIZE_SQ; ++i)
                            V9[i] = (float)v9[i] * heightMultiplier + hheader.gridHeight;

                        for (int i = 0; i < SharedConst.V8_SIZE_SQ; ++i)
                            V8[i] = (float)v8[i] * heightMultiplier + hheader.gridHeight;
                    }
                    else if (Convert.ToBoolean(hheader.flags & (uint)MapHeightFlags.AsInt16))
                    {
                        ushort[] v9 = new ushort[SharedConst.V9_SIZE_SQ];
                        ushort[] v8 = new ushort[SharedConst.V8_SIZE_SQ];

                        for (var i = 0; i < SharedConst.V9_SIZE_SQ; ++i)
                            v9[i] = reader.ReadUInt16();

                        for (var i = 0; i < SharedConst.V8_SIZE_SQ; ++i)
                            v8[i] = reader.ReadUInt16();

                        heightMultiplier = (hheader.gridMaxHeight - hheader.gridHeight) / 65535;

                        for (int i = 0; i < SharedConst.V9_SIZE_SQ; ++i)
                            V9[i] = (float)v9[i] * heightMultiplier + hheader.gridHeight;

                        for (int i = 0; i < SharedConst.V8_SIZE_SQ; ++i)
                            V8[i] = (float)v8[i] * heightMultiplier + hheader.gridHeight;
                    }
                    else
                    {
                        for (var i = 0; i < SharedConst.V9_SIZE_SQ; ++i)
                            V9[i] = reader.ReadSingle();

                        for (var i = 0; i < SharedConst.V8_SIZE_SQ; ++i)
                            V8[i] = reader.ReadSingle();
                    }

                    // hole data
                    if (fheader.holesSize != 0)
                    {
                        reader.BaseStream.Seek(fheader.holesOffset, SeekOrigin.Begin);

                        int readCount = 0;
                        for (var i = 0; i < 16; ++i)
                        {
                            for (var x = 0; x < 16; ++x)
                            {
                                for (var c = 0; c < 8; ++c)
                                {
                                    if (readCount == fheader.holesSize)
                                        break;

                                    holes[i][x][c] = reader.ReadByte();
                                }
                            }
                        }
                    }

                    int count1 = meshData.solidVerts.Count / 3;
                    float xoffset = ((float)tileX - 32) * SharedConst.GRID_SIZE;
                    float yoffset = ((float)tileY - 32) * SharedConst.GRID_SIZE;

                    float[] coord = new float[3];

                    for (int i = 0; i < SharedConst.V9_SIZE_SQ; ++i)
                    {
                        getHeightCoord(i, Grid.V9, xoffset, yoffset, ref coord, ref V9);
                        meshData.solidVerts.Add(coord[0]);
                        meshData.solidVerts.Add(coord[2]);
                        meshData.solidVerts.Add(coord[1]);
                    }

                    for (int i = 0; i < SharedConst.V8_SIZE_SQ; ++i)
                    {
                        getHeightCoord(i, Grid.V8, xoffset, yoffset, ref coord, ref V8);
                        meshData.solidVerts.Add(coord[0]);
                        meshData.solidVerts.Add(coord[2]);
                        meshData.solidVerts.Add(coord[1]);
                    }

                    int[] indices = { 0, 0, 0 };
                    int loopStart = 0, loopEnd = 0, loopInc = 0;
                    getLoopVars(portion, ref loopStart, ref loopEnd, ref loopInc);
                    for (int i = loopStart; i < loopEnd; i += loopInc)
                    {
                        for (Spot j = Spot.Top; j <= Spot.Bottom; j += 1)
                        {
                            getHeightTriangle(i, j, indices);
                            ttriangles.Add(indices[2] + count1);
                            ttriangles.Add(indices[1] + count1);
                            ttriangles.Add(indices[0] + count1);
                        }
                    }
                }

                // liquid data
                if (haveLiquid)
                {
                    reader.BaseStream.Seek(fheader.liquidMapOffset, SeekOrigin.Begin);
                    map_liquidHeader lheader = reader.Read<map_liquidHeader>();

                    float[] liquid_map = null;
                    if (!Convert.ToBoolean(lheader.flags & 0x0001))
                    {
                        for (var i = 0; i < 16; ++i)
                            for (var x = 0; x < 16; ++x)
                                liquid_entry[i][x] = reader.ReadUInt16();

                        for (var i = 0; i < 16; ++i)
                            for (var x = 0; x < 16; ++x)
                                liquid_flags[i][x] = reader.ReadByte();
                    }
                    else
                    {
                        for (var i = 0; i < 16; ++i)
                        {
                            for (var x = 0; x < 16; ++x)
                            {
                                liquid_entry[i][x] = lheader.liquidType;
                                liquid_flags[i][x] = lheader.liquidFlags;
                            }
                        }
                    }

                    if (!Convert.ToBoolean(lheader.flags & 0x0002))
                    {
                        int toRead = lheader.width * lheader.height;
                        liquid_map = new float[toRead];
                        for (var i = 0; i < toRead; ++i)
                            liquid_map[i] = reader.ReadSingle();
                    }

                    int count = meshData.liquidVerts.Count / 3;
                    float xoffset = (tileX - 32) * SharedConst.GRID_SIZE;
                    float yoffset = (tileY - 32) * SharedConst.GRID_SIZE;

                    float[] coord = new float[3];
                    int row, col;

                    // generate coordinates
                    if (!Convert.ToBoolean(lheader.flags & 0x0002))
                    {
                        int j = 0;
                        for (int i = 0; i < SharedConst.V9_SIZE_SQ; ++i)
                        {
                            row = i / SharedConst.V9_SIZE;
                            col = i % SharedConst.V9_SIZE;

                            if (row < lheader.offsetY || row >= lheader.offsetY + lheader.height ||
                                col < lheader.offsetX || col >= lheader.offsetX + lheader.width)
                            {
                                // dummy vert using invalid height
                                meshData.liquidVerts.Add((xoffset + col * SharedConst.GRID_PART_SIZE) * -1);
                                meshData.liquidVerts.Add(SharedConst.INVALID_MAP_LIQ_HEIGHT);
                                meshData.liquidVerts.Add((yoffset + row * SharedConst.GRID_PART_SIZE) * -1);
                                continue;
                            }

                            getLiquidCoord(i, j, xoffset, yoffset, ref coord, ref liquid_map);
                            meshData.liquidVerts.Add(coord[0]);
                            meshData.liquidVerts.Add(coord[2]);
                            meshData.liquidVerts.Add(coord[1]);
                            j++;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < SharedConst.V9_SIZE_SQ; ++i)
                        {
                            row = i / SharedConst.V9_SIZE;
                            col = i % SharedConst.V9_SIZE;
                            meshData.liquidVerts.Add((xoffset + col * SharedConst.GRID_PART_SIZE) * -1);
                            meshData.liquidVerts.Add(lheader.liquidLevel);
                            meshData.liquidVerts.Add((yoffset + row * SharedConst.GRID_PART_SIZE) * -1);
                        }
                    }


                    int[] indices = { 0, 0, 0 };
                    int loopStart = 0, loopEnd = 0, loopInc = 0, triInc = Spot.Bottom - Spot.Top;
                    getLoopVars(portion, ref loopStart, ref loopEnd, ref loopInc);

                    // generate triangles
                    for (int i = loopStart; i < loopEnd; i += loopInc)
                    {
                        for (Spot j = Spot.Top; j <= Spot.Bottom; j += triInc)
                        {
                            getHeightTriangle(i, j, indices, true);
                            ltriangles.Add(indices[2] + count);
                            ltriangles.Add(indices[1] + count);
                            ltriangles.Add(indices[0] + count);
                        }
                    }
                }

                // now that we have gathered the data, we can figure out which parts to keep:
                // liquid above ground, ground above liquid
                int loopStart1 = 0, loopEnd1 = 0, loopInc1 = 0, tTriCount = 4;
                bool useTerrain, useLiquid;

                float[] lverts = meshData.liquidVerts.ToArray();
                int[] ltris = ltriangles.ToArray();
                int currentLtrisIndex = 0;

                float[] tverts = meshData.solidVerts.ToArray();
                int[] ttris = ttriangles.ToArray();
                int currentTtrisIndex = 0;

                if ((ltriangles.Count + ttriangles.Count) == 0)
                    return false;

                // make a copy of liquid vertices
                // used to pad right-bottom frame due to lost vertex data at extraction
                float[] lverts_copy = null;
                if (meshData.liquidVerts.Count != 0)
                {
                    lverts_copy = new float[meshData.liquidVerts.Count];
                    Array.Copy(lverts, lverts_copy, meshData.liquidVerts.Count);
                }

                getLoopVars(portion, ref loopStart1, ref loopEnd1, ref loopInc1);
                for (int i = loopStart1; i < loopEnd1; i += loopInc1)
                {
                    for (int j = 0; j < 2; ++j)
                    {
                        // default is true, will change to false if needed
                        useTerrain = true;
                        useLiquid = true;
                        byte liquidType = 0;

                        // if there is no liquid, don't use liquid
                        if (meshData.liquidVerts.Count == 0 || ltriangles.Count == 0)
                            useLiquid = false;
                        else
                        {
                            liquidType = getLiquidType(i, liquid_flags);
                            if (Convert.ToBoolean(liquidType & (byte)LiquidTypeMask.DarkWater))
                            {
                                // players should not be here, so logically neither should creatures
                                useTerrain = false;
                                useLiquid = false;
                            }
                            else if((liquidType & (byte)(LiquidTypeMask.Water | LiquidTypeMask.Ocean)) != 0)
                                liquidType = (byte)NavArea.Water;
                            else if (Convert.ToBoolean(liquidType & (byte)(LiquidTypeMask.Magma | LiquidTypeMask.Slime)))
                                liquidType = (byte)NavArea.MagmaSlime;
                            else
                                useLiquid = false;
                        }

                        // if there is no terrain, don't use terrain
                        if (ttriangles.Count == 0)
                            useTerrain = false;

                        // while extracting ADT data we are losing right-bottom vertices
                        // this code adds fair approximation of lost data
                        if (useLiquid)
                        {
                            float quadHeight = 0;
                            uint validCount = 0;
                            for (uint idx = 0; idx < 3; idx++)
                            {
                                float h = lverts_copy[ltris[currentLtrisIndex + idx] * 3 + 1];
                                if (h != SharedConst.INVALID_MAP_LIQ_HEIGHT && h < SharedConst.INVALID_MAP_LIQ_HEIGHT_MAX)
                                {
                                    quadHeight += h;
                                    validCount++;
                                }
                            }

                            // update vertex height data
                            if (validCount > 0 && validCount < 3)
                            {
                                quadHeight /= validCount;
                                for (uint idx = 0; idx < 3; idx++)
                                {
                                    float h = lverts[ltris[currentLtrisIndex + idx] * 3 + 1];
                                    if (h == SharedConst.INVALID_MAP_LIQ_HEIGHT || h > SharedConst.INVALID_MAP_LIQ_HEIGHT_MAX)
                                        lverts[ltris[currentLtrisIndex + idx] * 3 + 1] = quadHeight;
                                }
                            }

                            // no valid vertexes - don't use this poly at all
                            if (validCount == 0)
                                useLiquid = false;
                        }

                        // if there is a hole here, don't use the terrain
                        if (useTerrain && fheader.holesSize != 0)
                            useTerrain = !isHole(i, holes);

                        // we use only one terrain kind per quad - pick higher one
                        if (useTerrain && useLiquid)
                        {
                            float minLLevel = SharedConst.INVALID_MAP_LIQ_HEIGHT_MAX;
                            float maxLLevel = SharedConst.INVALID_MAP_LIQ_HEIGHT;
                            for (uint x = 0; x < 3; x++)
                            {
                                float h = lverts[ltris[currentLtrisIndex + x] * 3 + 1];
                                if (minLLevel > h)
                                    minLLevel = h;

                                if (maxLLevel < h)
                                    maxLLevel = h;
                            }

                            float maxTLevel = SharedConst.INVALID_MAP_LIQ_HEIGHT;
                            float minTLevel = SharedConst.INVALID_MAP_LIQ_HEIGHT_MAX;
                            for (uint x = 0; x < 6; x++)
                            {
                                float h = tverts[ttris[currentTtrisIndex + x] * 3 + 1];
                                if (maxTLevel < h)
                                    maxTLevel = h;

                                if (minTLevel > h)
                                    minTLevel = h;
                            }

                            // terrain under the liquid?
                            if (minLLevel > maxTLevel)
                                useTerrain = false;

                            //liquid under the terrain?
                            if (minTLevel > maxLLevel)
                                useLiquid = false;
                        }

                        // store the result
                        if (useLiquid)
                        {
                            meshData.liquidType.Add((byte)liquidType);
                            for (int k = 0; k < 3; ++k)
                                meshData.liquidTris.Add(ltris[currentLtrisIndex + k]);
                        }

                        if (useTerrain)
                            for (int k = 0; k < 3 * tTriCount / 2; ++k)
                                meshData.solidTris.Add(ttris[currentTtrisIndex + k]);

                        currentLtrisIndex += 3;
                        //ltris = ltris.Skip(3).ToArray();
                        currentTtrisIndex += 3 * tTriCount / 2;
                        //ttris = ttris.Skip(3 * tTriCount / 2).ToArray();
                    }
                }
            }
            return meshData.solidTris.Count != 0 || meshData.liquidTris.Count != 0;
        }

        /**************************************************************************/
        void getHeightCoord(int index, Grid grid, float xOffset, float yOffset, ref float[] coord, ref float[] v)
        {
            // wow coords: x, y, height
            // coord is mirroed about the horizontal axes
            switch (grid)
            {
                case Grid.V9:
                    coord[0] = (xOffset + index % (SharedConst.V9_SIZE) * SharedConst.GRID_PART_SIZE) * -1.0f;
                    coord[1] = (yOffset + (int)(index / (SharedConst.V9_SIZE)) * SharedConst.GRID_PART_SIZE) * -1.0f;
                    coord[2] = v[index];
                    break;
                case Grid.V8:
                    coord[0] = (xOffset + index % (SharedConst.V8_SIZE) * SharedConst.GRID_PART_SIZE + SharedConst.GRID_PART_SIZE / 2.0f) * -1.0f;
                    coord[1] = (yOffset + (int)(index / (SharedConst.V8_SIZE)) * SharedConst.GRID_PART_SIZE + SharedConst.GRID_PART_SIZE / 2.0f) * -1.0f;
                    coord[2] = v[index];
                    break;
            }
        }

        /**************************************************************************/
        void getHeightTriangle(int square, Spot triangle, int[] indices, bool liquid = false)
        {
            int rowOffset = square / SharedConst.V8_SIZE;
            if (!liquid)
                switch (triangle)
                {
                    case Spot.Top:
                        indices[0] = square + rowOffset;                  //           0-----1 .... 128
                        indices[1] = square + 1 + rowOffset;                //           |\ T /|
                        indices[2] = (SharedConst.V9_SIZE_SQ) + square;               //           | \ / |
                        break;                                          //           |L 0 R| .. 127
                    case Spot.Left:                                          //           | / \ |
                        indices[0] = square + rowOffset;                  //           |/ B \|
                        indices[1] = (SharedConst.V9_SIZE_SQ) + square;               //          129---130 ... 386
                        indices[2] = square + SharedConst.V9_SIZE + rowOffset;          //           |\   /|
                        break;                                          //           | \ / |
                    case Spot.Right:                                         //           | 128 | .. 255
                        indices[0] = square + 1 + rowOffset;                //           | / \ |
                        indices[1] = square + SharedConst.V9_SIZE + 1 + rowOffset;        //           |/   \|
                        indices[2] = (SharedConst.V9_SIZE_SQ) + square;               //          258---259 ... 515
                        break;
                    case Spot.Bottom:
                        indices[0] = (SharedConst.V9_SIZE_SQ) + square;
                        indices[1] = square + SharedConst.V9_SIZE + 1 + rowOffset;
                        indices[2] = square + SharedConst.V9_SIZE + rowOffset;
                        break;
                    default: break;
                }
            else
                switch (triangle)
                {                                                           //           0-----1 .... 128
                    case Spot.Top:                                               //           |\    |
                        indices[0] = square + rowOffset;                      //           | \ T |
                        indices[1] = square + 1 + rowOffset;                    //           |  \  |
                        indices[2] = square + SharedConst.V9_SIZE + 1 + rowOffset;            //           | B \ |
                        break;                                              //           |    \|
                    case Spot.Bottom:                                            //          129---130 ... 386
                        indices[0] = square + rowOffset;                      //           |\    |
                        indices[1] = square + SharedConst.V9_SIZE + 1 + rowOffset;            //           | \   |
                        indices[2] = square + SharedConst.V9_SIZE + rowOffset;              //           |  \  |
                        break;                                              //           |   \ |
                    default: break;                                         //           |    \|
                }                                                           //          258---259 ... 515

        }

        /**************************************************************************/
        void getLiquidCoord(int index, int index2, float xOffset, float yOffset, ref float[] coord, ref float[] v)
        {
            // wow coords: x, y, height
            // coord is mirroed about the horizontal axes
            coord[0] = (xOffset + index % (SharedConst.V9_SIZE) * SharedConst.GRID_PART_SIZE) * -1.0f;
            coord[1] = (yOffset + (int)(index / (SharedConst.V9_SIZE)) * SharedConst.GRID_PART_SIZE) * -1.0f;
            coord[2] = v[index2];
        }

        /**************************************************************************/
        bool isHole(int square, byte[][][] holes)
        {
            int row = square / 128;
            int col = square % 128;
            int cellRow = row / 8;     // 8 squares per cell
            int cellCol = col / 8;
            int holeRow = row % 8;
            int holeCol = (square - (row * 128 + cellCol * 8));

            return (holes[cellRow][cellCol][holeRow] & (1 << holeCol)) != 0;
        }

        /**************************************************************************/
        byte getLiquidType(int square, byte[][] liquid_type)
        {
            int row = square / 128;
            int col = square % 128;
            int cellRow = row / 8;     // 8 squares per cell
            int cellCol = col / 8;

            return liquid_type[cellRow][cellCol];
        }

        /**************************************************************************/
        public bool loadVMap(uint mapID, uint tileX, uint tileY, MeshData meshData)
        {
            bool result = vmapManager.LoadSingleMap(mapID, "vmaps", tileX, tileY);
            bool retval = false;

            do
            {
                if (!result)
                    break;

                Dictionary<uint, StaticMapTree> instanceTrees;
                vmapManager.GetInstanceMapTree(out instanceTrees);

                if (instanceTrees[mapID] == null)
                    break;

                ModelInstance[] models = null;
                uint count;
                instanceTrees[mapID].GetModelInstances(out models, out count);

                if (models == null)
                    break;

                for (uint i = 0; i < count; ++i)
                {
                    ModelInstance instance = models[i];
                    if (instance == null)
                        continue;

                    // model instances exist in tree even though there are instances of that model in this tile
                    WorldModel worldModel = instance.GetWorldModel();
                    if (worldModel == null)
                        continue;

                    // now we have a model to add to the meshdata
                    retval = true;

                    List<GroupModel> groupModels;
                    worldModel.getGroupModels(out groupModels);

                    // all M2s need to have triangle indices reversed
                    bool isM2 = (instance.flags & ModelFlags.M2) != 0;

                    // transform data
                    float scale = instance.iScale;
                    Matrix3 rotation = Matrix3.fromEulerAnglesXYZ(MathF.PI * instance.iRot.Z / -180.0f, MathF.PI * instance.iRot.X / -180.0f, MathF.PI * instance.iRot.Y / -180.0f);
                    Vector3 position = instance.iPos;
                    position.X -= 32 * SharedConst.GRID_SIZE;
                    position.Y -= 32 * SharedConst.GRID_SIZE;

                    foreach (var it in groupModels)
                    {
                        List<Vector3> tempVertices;
                        List<Vector3> transformedVertices = new List<Vector3>();
                        List<MeshTriangle> tempTriangles;
                        WmoLiquid liquid = null;

                        it.GetMeshData(out tempVertices, out tempTriangles, out liquid);

                        // first handle collision mesh
                        transform(tempVertices.ToArray(), transformedVertices, scale, rotation, position);

                        int offset = meshData.solidVerts.Count / 3;

                        copyVertices(transformedVertices.ToArray(), meshData.solidVerts);
                        copyIndices(tempTriangles, meshData.solidTris, offset, isM2);

                        // now handle liquid data
                        if (liquid != null && liquid.iFlags != null)
                        {
                            List<Vector3> liqVerts = new List<Vector3>();
                            List<uint> liqTris = new List<uint>();
                            uint tilesX, tilesY, vertsX, vertsY;
                            Vector3 corner;
                            liquid.GetPosInfo(out tilesX, out tilesY, out corner);
                            vertsX = tilesX + 1;
                            vertsY = tilesY + 1;
                            byte[] flags = liquid.iFlags;
                            float[] data = liquid.iHeight;
                            NavArea type = NavArea.Empty;

                            // convert liquid type to NavTerrain
                            var liquidTypeRecord = liquidTypeStorage.LookupByKey(liquid.GetLiquidType());
                            uint liquidFlags = (uint)(liquidTypeRecord != null ? (1 << liquidTypeRecord.SoundBank) : 0);
                            if ((liquidFlags & (uint)(LiquidTypeMask.Water | LiquidTypeMask.Ocean)) != 0)
                                type = NavArea.Water;
                            else if ((liquidFlags & (uint)(LiquidTypeMask.Magma | LiquidTypeMask.Slime)) != 0)
                                type = NavArea.MagmaSlime;

                            // indexing is weird...
                            // after a lot of trial and error, this is what works:
                            // vertex = y*vertsX+x
                            // tile   = x*tilesY+y
                            // flag   = y*tilesY+x

                            Vector3 vert;
                            for (uint x = 0; x < vertsX; ++x)
                            {
                                for (uint y = 0; y < vertsY; ++y)
                                {
                                    vert = new Vector3(corner.X + x * SharedConst.GRID_PART_SIZE, corner.Y + y * SharedConst.GRID_PART_SIZE, data[y * vertsX + x]);
                                    vert = vert * rotation * scale + position;
                                    vert.X *= -1.0f;
                                    vert.Y *= -1.0f;
                                    liqVerts.Add(vert);
                                }
                            }

                            uint idx1, idx2, idx3, idx4;
                            uint square;
                            for (uint x = 0; x < tilesX; ++x)
                            {
                                for (uint y = 0; y < tilesY; ++y)
                                {
                                    if ((flags[x + y * tilesX] & 0x0f) != 0x0f)
                                    {
                                        square = x * tilesY + y;
                                        idx1 = square + x;
                                        idx2 = square + 1 + x;
                                        idx3 = square + tilesY + 1 + 1 + x;
                                        idx4 = square + tilesY + 1 + x;

                                        // top triangle
                                        liqTris.Add(idx3);
                                        liqTris.Add(idx2);
                                        liqTris.Add(idx1);
                                        // bottom triangle
                                        liqTris.Add(idx4);
                                        liqTris.Add(idx3);
                                        liqTris.Add(idx1);
                                    }
                                }
                            }

                            int liqOffset = meshData.liquidVerts.Count / 3;
                            for (int x = 0; x < liqVerts.Count; ++x)
                            {
                                meshData.liquidVerts.Add(liqVerts[x].Y);
                                meshData.liquidVerts.Add(liqVerts[x].Z);
                                meshData.liquidVerts.Add(liqVerts[x].X);
                            }

                            for (int x = 0; x < liqTris.Count / 3; ++x)
                            {
                                meshData.liquidTris.Add((int)(liqTris[x * 3 + 1] + liqOffset));
                                meshData.liquidTris.Add((int)(liqTris[x * 3 + 2] + liqOffset));
                                meshData.liquidTris.Add((int)(liqTris[x * 3] + liqOffset));
                                meshData.liquidType.Add((byte)type);
                            }
                        }
                    }
                }
            }
            while (false);

            vmapManager.UnloadSingleMap(mapID, tileX, tileY);

            return retval;
        }

        /**************************************************************************/
        void transform(Vector3[] source, List<Vector3> transformedVertices, float scale, Matrix3 rotation, Vector3 position)
        {
            foreach (var it in source)
            {
                // apply tranform, then mirror along the horizontal axes
                Vector3 v = new Vector3(it * rotation * scale + position);
                v.X *= -1.0f;
                v.Y *= -1.0f;
                transformedVertices.Add(v);
            }
        }

        /**************************************************************************/
        void copyVertices(Vector3[] source, List<float> dest)
        {
            foreach (var it in source)
            {
                dest.Add(it.Y);
                dest.Add(it.Z);
                dest.Add(it.X);
            }
        }

        /**************************************************************************/
        void copyIndices(List<MeshTriangle> source, List<int> dest, int offset, bool flip)
        {
            if (flip)
            {
                foreach (var it in source)
                {
                    dest.Add((int)(it.idx2 + offset));
                    dest.Add((int)(it.idx1 + offset));
                    dest.Add((int)(it.idx0 + offset));
                }
            }
            else
            {
                foreach (var it in source)
                {
                    dest.Add((int)(it.idx0 + offset));
                    dest.Add((int)(it.idx1 + offset));
                    dest.Add((int)(it.idx2 + offset));
                }
            }
        }

        public static void copyIndices(List<int> source, List<int> dest, int offset)
        {
            int[] src = source.ToArray();

            for (int i = 0; i < source.Count; ++i)
            {
                var g = src[i] + offset;
                dest.Add(src[i] + offset);

            }


        }

        /**************************************************************************/
        void copyIndices(int[] source, List<int> dest, int offset)
        {
            for (int i = 0; i < source.Length; ++i)
                dest.Add(source[i] + offset);
        }

        /**************************************************************************/
        public static void cleanVertices(List<float> verts, List<int> tris)
        {
            Dictionary<int, int> vertMap = new Dictionary<int, int>();

            List<float> cleanVerts = new List<float>();
            int index, count = 0;
            // collect all the vertex indices from triangle
            for (int i = 0; i < tris.Count; ++i)
            {
                if (vertMap.ContainsKey(tris[i]))
                    continue;

                index = tris[i];
                vertMap.Add(tris[i], count);

                cleanVerts.Add(verts[index * 3]);
                cleanVerts.Add(verts[index * 3 + 1]);
                cleanVerts.Add(verts[index * 3 + 2]);
                count++;
            }

            verts.Clear();
            verts.AddRange(cleanVerts);
            cleanVerts.Clear();

            // update triangles to use new indices
            for (int i = 0; i < tris.Count; ++i)
            {
                if (!vertMap.ContainsKey(tris[i]))
                    continue;

                tris[i] = vertMap[tris[i]];
            }

            vertMap.Clear();
        }

        /**************************************************************************/
        public void loadOffMeshConnections(uint mapID, uint tileX, uint tileY, MeshData meshData, string offMeshFilePath)
        {
            // no meshfile input given?
            if (offMeshFilePath == null)
                return;

            if (!File.Exists(offMeshFilePath))
            {
                Console.WriteLine($"loadOffMeshConnections: input file {offMeshFilePath} not found!");
                return;
            }

            using (BinaryReader binaryReader = new BinaryReader(File.Open(offMeshFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                // pretty silly thing, as we parse entire file and load only the tile we need
                // but we don't expect this file to be too large
                long fileLength = binaryReader.BaseStream.Length;
                while (binaryReader.BaseStream.Position < fileLength)
                {
                    var s = binaryReader.ReadString(512);
                    float[] p0 = new float[0];
                    float[] p1 = new float[0];
                    uint mid = 0, tx = 0, ty = 0;
                    float size = 0;

                    string[] stringValues = s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in stringValues)
                    {

                    }

                    if (mapID == mid && tileX == tx && tileY == ty)
                    {
                        meshData.offMeshConnections.Add(p0[1]);
                        meshData.offMeshConnections.Add(p0[2]);
                        meshData.offMeshConnections.Add(p0[0]);

                        meshData.offMeshConnections.Add(p1[1]);
                        meshData.offMeshConnections.Add(p1[2]);
                        meshData.offMeshConnections.Add(p1[0]);

                        meshData.offMeshConnectionDirs.Add(1);          // 1 - both direction, 0 - one sided
                        meshData.offMeshConnectionRads.Add(size);       // agent size equivalent
                                                                           // can be used same way as polygon flags
                        meshData.offMeshConnectionsAreas.Add(0xFF);
                        meshData.offMeshConnectionsFlags.Add(0xFF);  // all movement masks can make this path
                    }

                }
            }
        }

        public bool usesLiquids() { return true; }// !m_skipLiquid; }

        VMapManager2 vmapManager;
        Dictionary<uint, LiquidTypeRecord> liquidTypeStorage;
    }

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

    public struct map_heightHeader
    {
        public uint fourcc;
        public uint flags;
        public float gridHeight;
        public float gridMaxHeight;
    }

    class MeshData
    {
        public List<float> solidVerts = new List<float>();
        public List<int> solidTris = new List<int>();

        public List<float> liquidVerts = new List<float>();
        public List<int> liquidTris = new List<int>();
        public List<byte> liquidType = new List<byte>();

        // offmesh connection data
        public List<float> offMeshConnections = new List<float>();   // [p0y,p0z,p0x,p1y,p1z,p1x] - per connection
        public List<float> offMeshConnectionRads = new List<float>();
        public List<byte> offMeshConnectionDirs = new List<byte>();
        public List<byte> offMeshConnectionsAreas = new List<byte>();
        public List<ushort> offMeshConnectionsFlags = new List<ushort>();
    }
}
