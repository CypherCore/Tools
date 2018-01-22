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

using Framework.Constants;
using Framework.GameMath;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Framework.Collision
{
    public enum VMAPLoadResult
    {
        Error,
        OK,
        Ignored
    }

    public class VMapManager2
    {
        public VMAPLoadResult loadMap(string basePath, uint mapId, uint x, uint y)
        {
            var result = VMAPLoadResult.Ignored;
            if (_loadMap(mapId, basePath, x, y))
                result = VMAPLoadResult.OK;
            else
                result = VMAPLoadResult.Error;

            return result;
        }

        bool _loadMap(uint mapId, string basePath, uint tileX, uint tileY)
        {
            var instanceTree = iInstanceMapTrees.LookupByKey(mapId);
            if (instanceTree == null)
            {
                string mapFileName = getMapFileName(mapId);
                StaticMapTree newTree = new StaticMapTree(mapId, basePath);
                if (!newTree.InitMap(mapFileName, this))
                    return false;

                iInstanceMapTrees.Add(mapId, newTree);

                instanceTree = newTree;
            }

            return instanceTree.LoadMapTile(tileX, tileY, this);
        }

        public WorldModel acquireModelInstance(string basepath, string filename)
        {
            var model = iLoadedModelFiles.LookupByKey(filename);
            if (model == null)
            {
                WorldModel worldmodel = new WorldModel();
                if (!worldmodel.readFile(basepath + filename + ".vmo"))
                {
                    Console.WriteLine($"VMapManager: could not load '{filename}.vmo'");
                    return null;
                }

                //Console.WriteLine($"VMapManager: loading file '{filename}'");
                iLoadedModelFiles.Add(filename, new ManagedModel());
                model = iLoadedModelFiles.LookupByKey(filename);
                model.setModel(worldmodel);
            }
            model.incRefCount();
            return model.getModel();
        }

        public void releaseModelInstance(string filename)
        {
            var model = iLoadedModelFiles.LookupByKey(filename);
            if (model == null)
            {
                Console.WriteLine($"VMapManager: trying to unload non-loaded file '{filename}'");
                return;
            }
            if (model.decRefCount() == 0)
            {
                //Console.WriteLine($"VMapManager: unloading file '{filename}'");
                iLoadedModelFiles.Remove(filename);
            }
        }

        public void getInstanceMapTree(out Dictionary<uint, StaticMapTree> instanceMapTree)
        {
            instanceMapTree = iInstanceMapTrees;
        }

        public static string getMapFileName(uint mapId)
        {
            return string.Format("{0:D4}.vmtree", mapId);
        }

        public void unloadMap(uint mapId, uint x, uint y)
        {
            var instanceTree = iInstanceMapTrees.LookupByKey(mapId);
            if (instanceTree != null)
            {
                instanceTree.UnloadMapTile(x, y, this);
                if (instanceTree.numLoadedTiles() == 0)
                {
                    iInstanceMapTrees.Remove(mapId);
                }
            }
        }

        Dictionary<string, ManagedModel> iLoadedModelFiles = new Dictionary<string, ManagedModel>();
        Dictionary<uint, StaticMapTree> iInstanceMapTrees = new Dictionary<uint, StaticMapTree>();
    }

    public class ManagedModel
    {
        public ManagedModel()
        {
            iModel = null;
            iRefCount = 0;
        }

        public void setModel(WorldModel model) { iModel = model; }
        public WorldModel getModel() { return iModel; }
        public void incRefCount() { ++iRefCount; }
        public int decRefCount() { return --iRefCount; }

        WorldModel iModel;
        int iRefCount;
    }
}

