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
using Framework.GameMath;
using Framework.Constants;
using System.Linq;
using Framework.Collision;
using System.Threading.Tasks;
using DataExtractor.Vmap;

namespace DataExtractor.Vmap.Collision
{
    class TileAssembler
    {
        public TileAssembler(string pSrcDirName, string pDestDirName)
        {
            iDestDir = pDestDirName;
            iSrcDir = pSrcDirName;
        }

        public bool convertWorld2()
        {
            bool success = readMapSpawns();
            if (!success)
                return false;

            // export Map data
            foreach (var mapPair in mapData)
            {
                var mapSpawn = mapPair.Value;
                // build global map tree
                List<ModelSpawn> mapSpawns = new List<ModelSpawn>();

                Console.WriteLine($"Calculating model bounds for map {mapPair.Key}...");
                foreach (var entry in mapSpawn.UniqueEntries.Values)
                {
                    // M2 models don't have a bound set in WDT/ADT placement data, i still think they're not used for LoS at all on retail
                    if (Convert.ToBoolean(entry.flags & ModelFlags.M2))
                    {
                        if (!calculateTransformedBound(entry))
                            continue;
                    }
                    else if (Convert.ToBoolean(entry.flags & ModelFlags.WorldSpawn)) // WMO maps and terrain maps use different origin, so we need to adapt :/
                    {
                        /// @todo remove extractor hack and uncomment below line:
                        //entry.second.iPos += Vector3(533.33333f*32, 533.33333f*32, 0.f);
                        entry.iBound = entry.iBound + new Vector3(533.33333f * 32, 533.33333f * 32, 0.0f);
                    }
                    mapSpawns.Add(entry);
                    spawnedModelFiles.Add(entry.name);
                }

                Console.WriteLine($"Creating map tree for map {mapPair.Key}...");
                BIH pTree = new BIH();
                pTree.build(mapSpawns, BoundsTrait.getBounds);

                // ===> possibly move this code to StaticMapTree class

                // write map tree file
                string mapfilename = $"{iDestDir}/{mapPair.Key:D4}.vmtree";
                using (BinaryWriter writer = new BinaryWriter(File.Open(mapfilename, FileMode.Create, FileAccess.Write)))
                {
                    //general info
                    writer.WriteString(SharedConst.VMAP_MAGIC);
                    uint globalTileID = StaticMapTree.packTileID(65, 65);
                    var globalRange = mapSpawn.TileEntries.LookupByKey(globalTileID);
                    bool isTiled = globalRange.Count == 0; // only maps without terrain (tiles) have global WMO
                    writer.Write(isTiled);

                    // Nodes
                    writer.WriteString("NODE");
                    pTree.writeToFile(writer);
                    // global map spawns (WDT), if any (most instances)
                    writer.WriteString("GOBJ");

                    foreach (var glob in globalRange)
                        ModelSpawn.writeToFile(writer, mapSpawn.UniqueEntries[glob.Id]);

                    // spawn id to index map
                    writer.WriteString("SIDX");
                    writer.Write(mapSpawns.Count);
                    for (int i = 0; i < mapSpawns.Count; ++i)
                    {
                        writer.Write(mapSpawns[i].ID);
                        writer.Write(i);
                    }
                }

                // write map tile files, similar to ADT files, only with extra BSP tree node info
                foreach (var key in mapSpawn.TileEntries.Keys)
                {
                    var spawnList = mapSpawn.TileEntries[key];

                    if (Convert.ToBoolean(spawnList.First().Flags & ModelFlags.WorldSpawn)) // WDT spawn, saved as tile 65/65 currently...
                        continue;
                    if (Convert.ToBoolean(spawnList.First().Flags & ModelFlags.ParentSpawn)) // tile belongs to parent map
                        continue;

                    uint x, y;
                    StaticMapTree.unpackTileID(key, out x, out y);
                    string tilefilename =  $"{iDestDir}/{mapPair.Key:D4}_{x:D2}_{y:D2}.vmtile";
                    using (BinaryWriter writer = new BinaryWriter(File.Open(tilefilename, FileMode.Create, FileAccess.Write)))
                    {
                        // file header
                        writer.WriteString(SharedConst.VMAP_MAGIC);
                        // write number of tile spawns
                        writer.Write(spawnList.Count);
                        // write tile spawns
                        foreach (var nSpawn in spawnList)
                        {
                            ModelSpawn spawn2 = mapSpawn.UniqueEntries[nSpawn.Id];
                            ModelSpawn.writeToFile(writer, spawn2);
                        }
                    }
                }
            }

            // add an object models, listed in temp_gameobject_models file
            exportGameobjectModels();

            // export objects
            Console.WriteLine("Converting Model Files");
            foreach (var mfile in spawnedModelFiles)
            {
                Console.WriteLine($"Converting {mfile}");
                convertRawFile(mfile);
            }

            return success;
        }

        bool readMapSpawns()
        {
            string fname = iSrcDir + "dir_bin";
            if (!File.Exists(fname))
            {
                Console.WriteLine("Could not read dir_bin file!");
                return false;
            }

            using (BinaryReader binaryReader = new BinaryReader(File.Open(fname, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                Console.WriteLine("Read coordinate mapping...");
                uint mapID, tileX, tileY;
                ModelSpawn spawn;

                while (binaryReader.BaseStream.Position + 4 < binaryReader.BaseStream.Length)
                {
                    // read mapID, tileX, tileY, Flags, NameSet, UniqueId, Pos, Rot, Scale, Bound_lo, Bound_hi, name
                    mapID = binaryReader.ReadUInt32();
                    tileX = binaryReader.ReadUInt32();
                    tileY = binaryReader.ReadUInt32();
                    if (!ModelSpawn.readFromFile(binaryReader, out spawn))
                        break;

                    MapSpawns current;
                    if (!mapData.ContainsKey(mapID))
                    {
                        Console.WriteLine($"spawning Map {mapID}");
                        current = new MapSpawns();
                        mapData[mapID] = current;
                    }
                    else
                        current = mapData[mapID];

                    if (!current.UniqueEntries.ContainsKey(spawn.ID))
                        current.UniqueEntries.Add(spawn.ID, spawn);

                    current.TileEntries.Add(StaticMapTree.packTileID(tileX, tileY), new TileSpawn(spawn.ID, spawn.flags));
                }
            }

            return true;
        }

        bool calculateTransformedBound(ModelSpawn spawn)
        {
            string modelFilename = iSrcDir + spawn.name;

            ModelPosition modelPosition = new ModelPosition();
            modelPosition.iDir = spawn.iRot;
            modelPosition.iScale = spawn.iScale;
            modelPosition.init();

            WorldModel_Raw raw_model = new WorldModel_Raw();
            if (!raw_model.Read(modelFilename))
                return false;

            int groups = raw_model.groupsArray.Length;
            if (groups != 1)
                Console.WriteLine($"Warning: '{modelFilename}' does not seem to be a M2 model!");

            AxisAlignedBox modelBound = AxisAlignedBox.Zero();
            bool boundEmpty = true;

            for (uint g = 0; g < groups; ++g) // should be only one for M2 files...
            {
                var vertices = raw_model.groupsArray[g].vertexArray;

                if (vertices.Empty())
                {
                    Console.WriteLine($"error: model {spawn.name} has no geometry!");
                    continue;
                }

                int nvectors = vertices.Count;
                for (int i = 0; i < nvectors; ++i)
                {
                    Vector3 v = modelPosition.transform(vertices[i]);

                    if (boundEmpty)
                    {
                        modelBound = new AxisAlignedBox(v, v);
                        boundEmpty = false;
                    }
                    else
                        modelBound.merge(v);
                }
            }
            spawn.iBound = modelBound + spawn.iPos;
            spawn.flags |= ModelFlags.HasBound;
            return true;
        }

        void convertRawFile(string pModelFilename)
        {
            string filename = iSrcDir + pModelFilename;

            WorldModel_Raw raw_model = new WorldModel_Raw();
            if (!raw_model.Read(filename))
                return;

            // write WorldModel
            WorldModel model = new WorldModel();
            model.setRootWmoID(raw_model.RootWMOID);
            if (!raw_model.groupsArray.Empty())
            {
                List<GroupModel> groupsArray = new List<GroupModel>();

                int groups = raw_model.groupsArray.Length;
                for (uint g = 0; g < groups; ++g)
                {
                    GroupModel_Raw raw_group = raw_model.groupsArray[g];
                    var groupModel = new GroupModel(raw_group.mogpflags, raw_group.GroupWMOID, raw_group.bounds);
                    groupModel.setMeshData(raw_group.vertexArray, raw_group.triangles.ToList());
                    groupModel.setLiquidData(raw_group.liquid);
                    groupsArray.Add(groupModel);
                }

                model.setGroupModels(groupsArray);
            }

            model.writeFile(iDestDir + "/" + pModelFilename + ".vmo");
        }

        void exportGameobjectModels()
        {
            if (!File.Exists(iSrcDir + "/" + "temp_gameobject_models"))
                return;

            using (BinaryReader reader = new BinaryReader(File.Open(iSrcDir + "/" + "temp_gameobject_models", FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                string magic = reader.ReadCString();
                if (magic != SharedConst.RAW_VMAP_MAGIC)
                    return;

                using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(iDestDir + "/" + "GameObjectModels.dtree")))
                {
                    writer.WriteString(SharedConst.VMAP_MAGIC);

                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        uint displayId = reader.ReadUInt32();
                        bool isWmo = reader.ReadBoolean();
                        int name_length = reader.ReadInt32();
                        string model_name = reader.ReadString(name_length);

                        WorldModel_Raw raw_model = new WorldModel_Raw();
                        if (!raw_model.Read((iSrcDir + "/" + model_name)))
                            continue;

                        spawnedModelFiles.Add(model_name);
                        AxisAlignedBox bounds = AxisAlignedBox.Zero();
                        bool boundEmpty = true;
                        for (uint g = 0; g < raw_model.groupsArray.Length; ++g)
                        {
                            var vertices = raw_model.groupsArray[g].vertexArray;
                            if (vertices == null)
                                continue;

                            for (int i = 0; i < vertices.Count; ++i)
                            {
                                Vector3 v = vertices[i];
                                if (boundEmpty)
                                {
                                    bounds = new AxisAlignedBox(v, v);
                                    boundEmpty = false;
                                }
                                else
                                    bounds.merge(v);
                            }
                        }

                        if (bounds.isEmpty())
                        {
                            Console.WriteLine($"Model {model_name} has empty bounding box");
                            continue;
                        }

                        if (bounds.isFinite())
                        {
                            Console.WriteLine($"Model {model_name} has invalid bounding box");
                            continue;
                        }

                        writer.Write(displayId);
                        writer.Write(isWmo);
                        writer.Write(name_length);
                        writer.WriteString(model_name);
                        writer.WriteVector3(bounds.Lo);
                        writer.WriteVector3(bounds.Hi);
                    }
                }
            }

        }

        string iDestDir;
        string iSrcDir;

        Dictionary<uint, MapSpawns> mapData = new Dictionary<uint, MapSpawns>();
        HashSet<string> spawnedModelFiles = new HashSet<string>();
    }

    class MapSpawns
    {
        public SortedDictionary<uint, ModelSpawn> UniqueEntries = new SortedDictionary<uint, ModelSpawn>();
        public MultiMap<uint, TileSpawn> TileEntries = new MultiMap<uint, TileSpawn>();
    }

    class ModelPosition
    {
        public void init()
        {
            iRotation = Matrix3.fromEulerAnglesZYX(MathF.PI * iDir.Y / 180.0f, MathF.PI * iDir.X / 180.0f, MathF.PI * iDir.Z / 180.0f);
        }

        public Vector3 transform(Vector3 pIn)
        {
            Vector3 outVec = pIn * iScale;
            outVec = iRotation * outVec;
            return (outVec);
        }

        void moveToBasePos(Vector3 pBasePos) { iPos -= pBasePos; }

        public Matrix3 iRotation;
        public Vector3 iPos;
        public Vector3 iDir;
        public float iScale;
    }

    class WorldModel_Raw
    {
        public bool Read(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"ERROR: Can't open raw model file: {path}");
                return false;
            }

            using (BinaryReader binaryReader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                string vmapMagic = binaryReader.ReadCString();
                if (vmapMagic != SharedConst.RAW_VMAP_MAGIC)
                {
                    Console.WriteLine($"Error: {vmapMagic} != {SharedConst.RAW_VMAP_MAGIC}");
                    return false;
                }

                // we have to read one int. This is needed during the export and we have to skip it here
                uint tempNVectors = binaryReader.ReadUInt32();

                uint groups = binaryReader.ReadUInt32();
                RootWMOID = binaryReader.ReadUInt32();

                bool succeed = true;
                groupsArray = new GroupModel_Raw[groups];
                for (uint g = 0; g < groups && succeed; ++g)
                {
                    groupsArray[g] = new GroupModel_Raw();
                    succeed = groupsArray[g].Read(binaryReader);
                }

                return succeed;
            }
        }

        public uint RootWMOID;
        public GroupModel_Raw[] groupsArray;
    }

    class GroupModel_Raw
    {
        public bool Read(BinaryReader reader)
        {
            mogpflags = reader.ReadUInt32();
            GroupWMOID = reader.ReadUInt32();

            Vector3 vec1 = reader.Read<Vector3>();
            Vector3 vec2 = reader.Read<Vector3>();
            bounds = new AxisAlignedBox(vec1, vec2);

            liquidflags = reader.ReadUInt32();

            string blockId = reader.ReadStringFromChars(4);
            if (blockId != "GRP ")
            {
                Console.WriteLine($"Error: {blockId} != GRP ");
                return false;
            }
            int blocksize = reader.ReadInt32();
            uint branches = reader.ReadUInt32();
            for (uint b = 0; b < branches; ++b)
            {   
                // indexes for each branch (not used yet)
                uint indexes = reader.ReadUInt32();
            }

            // ---- indexes
            blockId = reader.ReadStringFromChars(4);
            if (blockId != "INDX")
            {
                Console.WriteLine($"Error: {blockId} != INDX");
                return false;
            }
            blocksize = reader.ReadInt32();
            uint nindexes = reader.ReadUInt32();
            if (nindexes > 0)
            {
                ushort[] indexarray = new ushort[nindexes];
                for (var i = 0; i < nindexes; ++i)
                    indexarray[i] = reader.ReadUInt16();

                for (uint i = 0; i < nindexes; i += 3)
                    triangles.Add(new MeshTriangle(indexarray[i], indexarray[i + 1], indexarray[i + 2]));
            }

            // ---- vectors
            blockId = reader.ReadStringFromChars(4);
            if (blockId != "VERT")
            {
                Console.WriteLine($"Error: {blockId} != VERT");
                return false;
            }
            blocksize = reader.ReadInt32();

            uint nvectors = reader.ReadUInt32();
            if (nvectors > 0)
            {
                float[] vectorarray = new float[nvectors * 3];
                for (var i = 0; i < nvectors * 3; ++i)
                    vectorarray[i] = reader.ReadSingle();

                vertexArray = new List<Vector3>();
                for (uint i = 0; i < nvectors; ++i)
                    vertexArray.Add(new Vector3(vectorarray[3 * i], vectorarray[3 * i + 1], vectorarray[3 * i + 2]));
            }

            // ----- liquid
            liquid = null;
            if (Convert.ToBoolean(liquidflags & 3))
            {
                blockId = reader.ReadStringFromChars(4);
                if (blockId != "LIQU")
                {
                    Console.WriteLine($"Error: {blockId} != LIQU");
                    return false;
                }
                blocksize = reader.ReadInt32();
                uint liquidType = reader.ReadUInt32();
                if (Convert.ToBoolean(liquidflags & 1))
                {
                    WMOLiquidHeader hlq = reader.Read<WMOLiquidHeader>();
                    liquid = new WmoLiquid((uint)hlq.xtiles, (uint)hlq.ytiles, new Vector3(hlq.pos_x, hlq.pos_y, hlq.pos_z), liquidType);
                    int size = hlq.xverts * hlq.yverts;
                    liquid.iHeight = new float[size];
                    for (var i = 0; i < size; ++i)
                        liquid.iHeight[i] = reader.ReadSingle();

                    size = hlq.xtiles * hlq.ytiles;
                    liquid.iFlags = new byte[size];
                    for (var i = 0; i < size; ++i)
                        liquid.iFlags[i] = reader.ReadByte();
                }
                else
                {
                    liquid = new WmoLiquid(0, 0, Vector3.Zero, liquidType);
                    liquid.iHeight[0] = bounds.Hi.Z;
                }
            }

            return true;
        }

        public uint mogpflags;
        public uint GroupWMOID;

        public AxisAlignedBox bounds;
        public uint liquidflags;
        public List<MeshTriangle> triangles = new List<MeshTriangle>();
        public List<Vector3> vertexArray = new List<Vector3>();
        public WmoLiquid liquid;
    }

    struct TileSpawn
    {
        public TileSpawn(uint id, uint flags)
        {
            Id = id;
            Flags = flags;
        }

        public uint Id { get; set; }
        public uint Flags { get; set; }
    }
}
