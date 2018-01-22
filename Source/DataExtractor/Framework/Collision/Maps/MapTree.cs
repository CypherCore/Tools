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
using System.Text;
using Framework.GameMath;
using System.IO;
using Framework.Constants;

namespace Framework.Collision
{
    public class StaticMapTree
    {
        public StaticMapTree(uint mapID, string basePath)
        {
            iMapID = mapID;
            iBasePath = basePath;

            if (iBasePath.Length > 0 && iBasePath[iBasePath.Length - 1] != '/' && iBasePath[iBasePath.Length - 1] != '\\')
                iBasePath += '/';
        }

        static string getTileFileName(uint mapID, uint tileX, uint tileY)
        {
            return $"{mapID:D4}_{tileY:D2}_{tileX:D2}.vmtile";
        }

        public bool InitMap(string fname, VMapManager2 vm)
        {
            //VMAP_DEBUG_LOG("maps", "StaticMapTree::InitMap() : initializing StaticMapTree '%s'", fname);
            bool success = false;
            string fullname = iBasePath + fname;
            if (!File.Exists(fullname))
                return false;

            using (BinaryReader binaryReader = new BinaryReader(File.Open(fullname, FileMode.Open, FileAccess.Read)))
            {

                if (binaryReader.ReadStringFromChars(8) == SharedConst.VMAP_MAGIC)
                {
                    iIsTiled = binaryReader.ReadBoolean();
                    if (binaryReader.ReadStringFromChars(4) == "NODE" && iTree.readFromFile(binaryReader))
                    {
                        iNTreeValues = iTree.primCount();
                        iTreeValues = new ModelInstance[iNTreeValues];
                        success = binaryReader.ReadStringFromChars(4) == "GOBJ";
                    }
                }

                // global model spawns
                // only non-tiled maps have them, and if so exactly one (so far at least...)
                ModelSpawn spawn;
                if (!iIsTiled && ModelSpawn.readFromFile(binaryReader, out spawn))
                {
                    WorldModel model = vm.acquireModelInstance(iBasePath, spawn.name);
                    //VMAP_DEBUG_LOG("maps", "StaticMapTree::InitMap() : loading %s", spawn.name);
                    if (model != null)
                    {
                        // assume that global model always is the first and only tree value (could be improved...)
                        iTreeValues[0] = new ModelInstance(spawn, model);
                        iLoadedSpawns[0] = 1;
                    }
                    else
                    {
                        success = false;
                        Console.WriteLine("StaticMapTree.InitMap() : could not acquire WorldModel pointer for '{spawn.name}'");
                    }
                }

            }
            return success;
        }

        public bool LoadMapTile(uint tileX, uint tileY, VMapManager2 vm)
        {
            if (!iIsTiled)
            {
                // currently, core creates grids for all maps, whether it has terrain tiles or not
                // so we need "fake" tile loads to know when we can unload map geometry
                iLoadedTiles[packTileID(tileX, tileY)] = false;
                return true;
            }
            if (iTreeValues == null)
            {
                Console.WriteLine("StaticMapTree.LoadMapTile() : tree has not been initialized [{tileX}, {tileY}]");
                return false;
            }
            
            string tilefile = iBasePath + getTileFileName(iMapID, tileX, tileY);
            if (File.Exists(tilefile))
            {
                using (BinaryReader binaryReader = new BinaryReader(File.Open(tilefile, FileMode.Open, FileAccess.Read)))
                {
                    if (binaryReader.ReadStringFromChars(8) != SharedConst.VMAP_MAGIC)
                        return false;

                    uint numSpawns = binaryReader.ReadUInt32();
                    for (uint i = 0; i < numSpawns; ++i)
                    {
                        // read model spawns
                        ModelSpawn spawn;
                        var result = ModelSpawn.readFromFile(binaryReader, out spawn);
                        if (result)
                        {
                            // acquire model instance
                            WorldModel model = vm.acquireModelInstance(iBasePath, spawn.name);
                            if (model == null)
                                Console.WriteLine($"StaticMapTree.LoadMapTile() : could not acquire WorldModel pointer [{tileX}, {tileY}]");

                            // update tree
                            uint referencedVal = binaryReader.ReadUInt32();
                            if (!iLoadedSpawns.ContainsKey(referencedVal))
                            {
                                if (referencedVal > iNTreeValues)
                                {
                                    Console.WriteLine($"StaticMapTree.LoadMapTile() : invalid tree element ({referencedVal}/{iNTreeValues}) referenced in tile {tilefile}");
                                    continue;
                                }

                                iTreeValues[referencedVal] = new ModelInstance(spawn, model);
                                iLoadedSpawns[referencedVal] = 1;
                            }
                            else
                                ++iLoadedSpawns[referencedVal];
                        }
                    }
                    iLoadedTiles[packTileID(tileX, tileY)] = true;
                }
            }
            else
                iLoadedTiles[packTileID(tileX, tileY)] = false;

            //TC_METRIC_EVENT("map_events", "LoadMapTile", "Map: " + std::to_string(iMapID) + " TileX: " + std::to_string(tileX) + " TileY: " + std::to_string(tileY));
            return true;
        }

        public void UnloadMapTile(uint tileX, uint tileY, VMapManager2 vm)
        {
            uint tileID = packTileID(tileX, tileY);
            if (!iLoadedTiles.ContainsKey(tileID))
            {
                Console.WriteLine("StaticMapTree.UnloadMapTile() : trying to unload non-loaded tile - Map:{iMapID} X:{tileX} Y:{tileY}");
                return;
            }

            var tile = iLoadedTiles.LookupByKey(tileID);
            if (tile) // file associated with tile
            {
                string tilefile = iBasePath + getTileFileName(iMapID, tileX, tileY);
                if (File.Exists(tilefile))
                {
                    using (BinaryReader binaryReader = new BinaryReader(File.Open(tilefile, FileMode.Open, FileAccess.Read)))
                    {
                        bool result = true;
                        if (binaryReader.ReadStringFromChars(8) != SharedConst.VMAP_MAGIC)
                            result = false;

                        uint numSpawns = binaryReader.ReadUInt32();
                        for (uint i = 0; i < numSpawns && result; ++i)
                        {
                            // read model spawns
                            ModelSpawn spawn;
                            result = ModelSpawn.readFromFile(binaryReader, out spawn);
                            if (result)
                            {
                                // release model instance
                                vm.releaseModelInstance(spawn.name);

                                // update tree
                                uint referencedNode = binaryReader.ReadUInt32();
                                if (!iLoadedSpawns.ContainsKey(referencedNode))
                                    Console.WriteLine($"StaticMapTree.UnloadMapTile() : trying to unload non-referenced model '{spawn.name}' (ID:{spawn.ID})");
                                else if (--iLoadedSpawns[referencedNode] == 0)
                                {
                                    iTreeValues[referencedNode].setUnloaded();
                                    iLoadedSpawns.Remove(referencedNode);
                                }                                
                            }
                        }
                    }
                }
            }
            iLoadedTiles.Remove(tileID);
            //TC_METRIC_EVENT("map_events", "UnloadMapTile", "Map: " + std::to_string(iMapID) + " TileX: " + std::to_string(tileX) + " TileY: " + std::to_string(tileY));
        }

        public void getModelInstances(out ModelInstance[] models, out uint count)
        {
            models = iTreeValues;
            count = iNTreeValues;
        }

        public int numLoadedTiles() { return iLoadedTiles.Count; }

        public static uint packTileID(uint tileX, uint tileY) { return tileX << 16 | tileY; }
        public static void unpackTileID(uint ID, out uint tileX, out uint tileY) { tileX = ID >> 16; tileY = ID & 0xFF; }

        uint iMapID;
        bool iIsTiled;
        BIH iTree = new BIH();
        ModelInstance[] iTreeValues; // the tree entries
        uint iNTreeValues;

        // Store all the map tile idents that are loaded for that map
        // some maps are not splitted into tiles and we have to make sure, not removing the map before all tiles are removed
        // empty tiles have no tile file, hence map with bool instead of just a set (consistency check)
        Dictionary<uint, bool> iLoadedTiles = new Dictionary<uint, bool>();
        // stores <tree_index, reference_count> to invalidate tree values, unload map, and to be able to report errors
        Dictionary<uint, uint> iLoadedSpawns = new Dictionary<uint, uint>();
        string iBasePath;
    }
}
