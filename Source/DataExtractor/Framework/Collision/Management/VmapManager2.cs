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

namespace DataExtractor.Framework.Collision
{
    public enum VMAPLoadResult
    {
        Error,
        OK,
        Ignored
    }

    public class VMapManager2
    {
        public void Initialize(MultiMap<uint, uint> mapData)
        {
            _childMapData = mapData;
            foreach (var pair in mapData)
                _parentMapData[pair.Value] = pair.Key;
        }

        public VMAPLoadResult LoadMap(string basePath, uint mapId, uint x, uint y)
        {
            var result = VMAPLoadResult.Ignored;
            if (LoadSingleMap(mapId, basePath, x, y))
            {
                result = VMAPLoadResult.OK;
                var childMaps = _childMapData.LookupByKey(mapId);
                foreach (uint childMapId in childMaps)
                    if (!LoadSingleMap(childMapId, basePath, x, y))
                        result = VMAPLoadResult.Error;
            }
            else
                result = VMAPLoadResult.Error;

            return result;
        }

        public bool LoadSingleMap(uint mapId, string basePath, uint tileX, uint tileY)
        {
            var instanceTree = _instanceMapTrees.LookupByKey(mapId);
            if (instanceTree == null)
            {
                string mapFileName = GetMapFileName(mapId);
                StaticMapTree newTree = new(mapId, basePath);
                if (!newTree.InitMap(mapFileName))
                    return false;

                _instanceMapTrees.Add(mapId, newTree);

                instanceTree = newTree;
            }

            return instanceTree.LoadMapTile(tileX, tileY, this);
        }

        public WorldModel AcquireModelInstance(string basepath, string filename)
        {
            filename = filename.TrimEnd('\0');
            var model = _loadedModelFiles.LookupByKey(filename);
            if (model == null)
            {
                WorldModel worldmodel = new();
                if (!worldmodel.readFile(basepath + filename))
                {
                    Console.WriteLine($"VMapManager: could not load '{filename}.vmo'");
                    return null;
                }

                model = new ManagedModel();
                model.SetModel(worldmodel);

                _loadedModelFiles.Add(filename, model);
            }
            model.IncRefCount();
            return model.GetModel();
        }

        public void ReleaseModelInstance(string filename)
        {
            filename = filename.TrimEnd('\0');
            var model = _loadedModelFiles.LookupByKey(filename);
            if (model == null)
            {
                Console.WriteLine($"VMapManager: trying to unload non-loaded file '{filename}'");
                return;
            }
            if (model.DecRefCount() == 0)
            {
                //Console.WriteLine($"VMapManager: unloading file '{filename}'");
                _loadedModelFiles.Remove(filename);
            }
        }

        public void GetInstanceMapTree(out Dictionary<uint, StaticMapTree> instanceMapTree)
        {
            instanceMapTree = _instanceMapTrees;
        }

        public static string GetMapFileName(uint mapId)
        {
            return string.Format("{0:D4}.vmtree", mapId);
        }

        public void UnloadMap(uint mapId, uint x, uint y)
        {
            var childMaps = _childMapData.LookupByKey(mapId);
            foreach (uint childMapId in childMaps)
                UnloadSingleMap(childMapId, x, y);

            UnloadSingleMap(mapId, x, y);
        }

        public void UnloadSingleMap(uint mapId, uint x, uint y)
        {
            var instanceTree = _instanceMapTrees.LookupByKey(mapId);
            if (instanceTree != null)
            {
                instanceTree.UnloadMapTile(x, y, this);
                if (instanceTree.NumLoadedTiles() == 0)
                {
                    _instanceMapTrees.Remove(mapId);
                }
            }
        }

        public int GetParentMapId(uint mapId)
        {
            if (_parentMapData.ContainsKey(mapId))
                return (int)_parentMapData[mapId];

            return -1;
        }

        Dictionary<string, ManagedModel> _loadedModelFiles = new();
        Dictionary<uint, StaticMapTree> _instanceMapTrees = new();
        MultiMap<uint, uint> _childMapData = new();
        Dictionary<uint, uint> _parentMapData = new();
    }

    public class ManagedModel
    {
        public ManagedModel()
        {
            _model = null;
            _refCount = 0;
        }

        public void SetModel(WorldModel model) { _model = model; }
        public WorldModel GetModel() { return _model; }
        public void IncRefCount() { ++_refCount; }
        public int DecRefCount() { return --_refCount; }

        WorldModel _model;
        int _refCount;
    }
}

