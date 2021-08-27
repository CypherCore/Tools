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
using System.Collections.Generic;
using System.IO;

namespace DataExtractor.Framework.Collision
{
    public class StaticMapTree
    {
        public StaticMapTree(uint mapID, string basePath)
        {
            _mapId = mapID;
            _basePath = basePath;

            if (_basePath.Length > 0 && _basePath[_basePath.Length - 1] != '/' && _basePath[_basePath.Length - 1] != '\\')
                _basePath += '/';
        }

        static string GetTileFileName(uint mapID, uint tileX, uint tileY)
        {
            return $"{mapID:D4}_{tileY:D2}_{tileX:D2}.vmtile";
        }

        public bool InitMap(string fname)
        {
            bool success = false;
            string fullname = _basePath + fname;
            if (!File.Exists(fullname))
                return false;

            using (BinaryReader binaryReader = new(File.Open(fullname, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                if (binaryReader.ReadStringFromChars(8) == SharedConst.VMAP_MAGIC)
                {
                    if (binaryReader.ReadStringFromChars(4) == "NODE" && _tree.readFromFile(binaryReader))
                    {
                        _nTreeValues = _tree.primCount();
                        _treeValues = new ModelInstance[_nTreeValues];
                        success = true;
                    }
                }

                if (success)
                {
                    success = binaryReader.ReadStringFromChars(4) == "SIDX";
                    if (success)
                    {
                        uint spawnIndicesSize = binaryReader.ReadUInt32();
                        for (uint i = 0; i < spawnIndicesSize; ++i)
                        {
                            uint spawnId = binaryReader.ReadUInt32();
                            uint spawnIndex = binaryReader.ReadUInt32();
                            _spawnIndices[spawnId] = spawnIndex;
                        }
                    }
                }

            }
            return success;
        }

        public bool LoadMapTile(uint tileX, uint tileY, VMapManager2 vm)
        {
            if (_treeValues == null)
            {
                Console.WriteLine("StaticMapTree.LoadMapTile() : tree has not been initialized [{tileX}, {tileY}]");
                return false;
            }
            
            string tilefile = _basePath + GetTileFileName(_mapId, tileX, tileY);
            if (File.Exists(tilefile))
            {
                using BinaryReader binaryReader = new(File.Open(tilefile, FileMode.Open, FileAccess.Read, FileShare.Read));
                if (binaryReader.ReadStringFromChars(8) != SharedConst.VMAP_MAGIC)
                    return false;

                uint numSpawns = binaryReader.ReadUInt32();
                for (uint i = 0; i < numSpawns; ++i)
                {
                    // read model spawns
                    var result = ModelSpawn.ReadFromFile(binaryReader, out ModelSpawn spawn);
                    if (result)
                    {
                        // acquire model instance
                        WorldModel model = vm.AcquireModelInstance(_basePath, spawn.name);
                        if (model == null)
                            Console.WriteLine($"StaticMapTree.LoadMapTile() : could not acquire WorldModel pointer [{tileX}, {tileY}]");

                        // update tree
                        if (_spawnIndices.ContainsKey(spawn.ID))
                        {
                            uint referencedVal = _spawnIndices[spawn.ID];
                            if (!_loadedSpawns.ContainsKey(referencedVal))
                            {
                                if (referencedVal > _nTreeValues)
                                {
                                    Console.WriteLine($"StaticMapTree.LoadMapTile() : invalid tree element ({referencedVal}/{_nTreeValues}) referenced in tile {tilefile}");
                                    continue;
                                }

                                _treeValues[referencedVal] = new ModelInstance(spawn, model);
                                _loadedSpawns[referencedVal] = 1;
                            }
                            else
                                ++_loadedSpawns[referencedVal];
                        }
                    }
                }
                _loadedTiles[PackTileID(tileX, tileY)] = true;
            }
            else
                _loadedTiles[PackTileID(tileX, tileY)] = false;

            //TC_METRIC_EVENT("map_events", "LoadMapTile", "Map: " + std::to_string(iMapID) + " TileX: " + std::to_string(tileX) + " TileY: " + std::to_string(tileY));
            return true;
        }

        public void UnloadMapTile(uint tileX, uint tileY, VMapManager2 vm)
        {
            uint tileID = PackTileID(tileX, tileY);
            if (!_loadedTiles.ContainsKey(tileID))
            {
                Console.WriteLine("StaticMapTree.UnloadMapTile() : trying to unload non-loaded tile - Map:{iMapID} X:{tileX} Y:{tileY}");
                return;
            }

            var tile = _loadedTiles.LookupByKey(tileID);
            if (tile) // file associated with tile
            {
                string tilefile = _basePath + GetTileFileName(_mapId, tileX, tileY);
                if (File.Exists(tilefile))
                {
                    using BinaryReader binaryReader = new(File.Open(tilefile, FileMode.Open, FileAccess.Read, FileShare.Read));
                    bool result = true;
                    if (binaryReader.ReadStringFromChars(8) != SharedConst.VMAP_MAGIC)
                        result = false;

                    uint numSpawns = binaryReader.ReadUInt32();
                    for (uint i = 0; i < numSpawns && result; ++i)
                    {
                        // read model spawns
                        result = ModelSpawn.ReadFromFile(binaryReader, out ModelSpawn spawn);
                        if (result)
                        {
                            // release model instance
                            vm.ReleaseModelInstance(spawn.name);

                            // update tree
                            if (!_spawnIndices.ContainsKey(spawn.ID))
                                result = false;
                            else
                            {
                                uint referencedVal = _spawnIndices[spawn.ID];
                                if (!_loadedSpawns.ContainsKey(referencedVal))
                                    Console.WriteLine($"StaticMapTree.UnloadMapTile() : trying to unload non-referenced model '{spawn.name}' (ID:{spawn.ID})");
                                else if (--_loadedSpawns[referencedVal] == 0)
                                {
                                    _treeValues[referencedVal].SetUnloaded();
                                    _loadedSpawns.Remove(referencedVal);
                                }
                            }
                        }
                    }
                }
            }
            _loadedTiles.Remove(tileID);
            //TC_METRIC_EVENT("map_events", "UnloadMapTile", "Map: " + std::to_string(iMapID) + " TileX: " + std::to_string(tileX) + " TileY: " + std::to_string(tileY));
        }

        public void GetModelInstances(out ModelInstance[] models, out uint count)
        {
            models = _treeValues;
            count = _nTreeValues;
        }

        public int NumLoadedTiles() { return _loadedTiles.Count; }

        public static uint PackTileID(uint tileX, uint tileY) { return tileX << 16 | tileY; }
        public static void UnpackTileID(uint ID, out uint tileX, out uint tileY) { tileX = ID >> 16; tileY = ID & 0xFF; }

        uint _mapId;
        BIH _tree = new();
        ModelInstance[] _treeValues; // the tree entries
        uint _nTreeValues;

        Dictionary<uint, uint> _spawnIndices = new();

        // Store all the map tile idents that are loaded for that map
        // some maps are not splitted into tiles and we have to make sure, not removing the map before all tiles are removed
        // empty tiles have no tile file, hence map with bool instead of just a set (consistency check)
        Dictionary<uint, bool> _loadedTiles = new();
        // stores <tree_index, reference_count> to invalidate tree values, unload map, and to be able to report errors
        Dictionary<uint, uint> _loadedSpawns = new();
        string _basePath;
    }
}
