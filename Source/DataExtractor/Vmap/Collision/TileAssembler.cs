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

using DataExtractor.Framework.Collision;
using DataExtractor.Framework.Constants;
using DataExtractor.Framework.GameMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace DataExtractor.Vmap.Collision
{
    class TileAssembler
    {
        public TileAssembler(string pSrcDirName, string pDestDirName)
        {
            destinationDirectory  = pDestDirName;
            sourceDirectory = pSrcDirName;
        }

        public bool ConvertWorld2()
        {
            bool success = ReadMapSpawns();
            if (!success)
                return false;

            float invTileSize = 1.0f / 533.33333f;

            // export Map data
            foreach (var (id, mapSpawn) in mapData)
            {
                // build global map tree
                List<ModelSpawn> mapSpawns = new();

                Console.WriteLine($"Calculating model bounds for map {id}...");
                foreach (var entry in mapSpawn.UniqueEntries.Values)
                {
                    // M2 models don't have a bound set in WDT/ADT placement data, i still think they're not used for LoS at all on retail
                    if (Convert.ToBoolean(entry.flags & ModelFlags.M2))
                    {
                        if (!CalculateTransformedBound(entry))
                            continue;
                    }

                    mapSpawns.Add(entry);
                    spawnedModelFiles.Add(entry.name);

                    var tileEntries = Convert.ToBoolean(entry.flags & ModelFlags.ParentSpawn) ? mapSpawn.ParentTileEntries : mapSpawn.TileEntries;

                    AxisAlignedBox bounds = entry.iBound;
                    Vector2 low = new(bounds.Lo.X * invTileSize, bounds.Lo.Y * invTileSize);
                    Vector2 high = new(bounds.Hi.X * invTileSize, bounds.Hi.Y * invTileSize);
                    for (uint x = (ushort)low.X; x <= (ushort)high.X; ++x)
                        for (uint y = (ushort)low.Y; y <= (ushort)high.Y; ++y)
                            tileEntries.Add(StaticMapTree.PackTileID(x, y), new TileSpawn(entry.ID, entry.flags));
                }

                Console.WriteLine($"Creating map tree for map {id}...");
                BIH pTree = new();
                pTree.build(mapSpawns, BoundsTrait.GetBounds);

                // ===> possibly move this code to StaticMapTree class

                // write map tree file
                string mapfilename = $"{destinationDirectory }/{id:D4}.vmtree";
                using (BinaryWriter writer = new(File.Open(mapfilename, FileMode.Create, FileAccess.Write)))
                {
                    //general info
                    writer.WriteString(SharedConst.VMAP_MAGIC);

                    // Nodes
                    writer.WriteString("NODE");
                    pTree.writeToFile(writer);

                    // spawn id to index map
                    writer.WriteString("SIDX");
                    writer.Write(mapSpawns.Count);
                    for (int i = 0; i < mapSpawns.Count; ++i)
                    {
                        writer.Write(mapSpawns[i].ID);
                        writer.Write(i);
                    }
                }

                // write map tile files, similar to ADT files, only with extra BIH tree node info
                foreach (var key in mapSpawn.TileEntries.Keys)
                {
                    var spawnList = mapSpawn.TileEntries[key];

                    StaticMapTree.UnpackTileID(key, out var x, out var y);
                    string tilefilename = $"{destinationDirectory }/{id:D4}_{y:D2}_{x:D2}.vmtile";
                    using BinaryWriter writer = new(File.Open(tilefilename, FileMode.Create, FileAccess.Write));
                    var parentTileEntries = mapSpawn.ParentTileEntries[key];

                    int nSpawns = spawnList.Count + parentTileEntries.Count;

                    // file header
                    writer.WriteString(SharedConst.VMAP_MAGIC);
                    // write number of tile spawns
                    writer.Write(nSpawns);
                    // write tile spawns
                    foreach (var tileSpawn in spawnList)
                        ModelSpawn.WriteToFile(writer, mapSpawn.UniqueEntries[tileSpawn.Id]);

                    foreach (var spawnItr in parentTileEntries)
                        ModelSpawn.WriteToFile(writer, mapSpawn.UniqueEntries[spawnItr.Id]);
                }
            }

            // add an object models, listed in temp_gameobject_models file
            ExportGameobjectModels();

            // export objects
            Console.WriteLine("Converting Model Files");
            foreach (var mfile in spawnedModelFiles)
            {
                Console.WriteLine($"Converting {mfile}");
                ConvertRawFile(mfile);
            }

            return success;
        }

        bool ReadMapSpawns()
        {
            string fname = sourceDirectory + "dir_bin";
            if (!File.Exists(fname))
            {
                Console.WriteLine("Could not read dir_bin file!");
                return false;
            }

            using (BinaryReader binaryReader = new(File.Open(fname, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                Console.WriteLine("Read coordinate mapping...");
                long fileLength = binaryReader.BaseStream.Length;
                while (binaryReader.BaseStream.Position + 4 < fileLength)
                {
                    // read mapID, Flags, NameSet, UniqueId, Pos, Rot, Scale, Bound_lo, Bound_hi, name
                    uint mapID = binaryReader.ReadUInt32();

                    if (!ModelSpawn.ReadFromFile(binaryReader, out ModelSpawn spawn))
                        break;

                    if (!mapData.ContainsKey(mapID))
                    {
                        Console.WriteLine($"spawning Map {mapID}");
                        mapData[mapID] = new MapSpawns(mapID);
                    }

                    MapSpawns current = mapData[mapID];
                    if (!current.UniqueEntries.ContainsKey(spawn.ID))
                        current.UniqueEntries.Add(spawn.ID, spawn);
                }
            }

            return true;
        }

        bool CalculateTransformedBound(ModelSpawn spawn)
        {
            string modelFilename = sourceDirectory + spawn.name.TrimEnd('\0');

            ModelPosition modelPosition = new();
            modelPosition.iDir = spawn.iRot;
            modelPosition.iScale = spawn.iScale;
            modelPosition.Init();

            WorldModel_Raw raw_model = new();
            if (!raw_model.Read(modelFilename))
                return false;

            int groups = raw_model.groupsArray.Length;
            if (groups != 1)
                Console.WriteLine($"Warning: '{modelFilename}' does not seem to be a M2 model!");

            AxisAlignedBox modelBound = AxisAlignedBox.NaN;
            modelBound.merge(modelPosition.Transform(raw_model.groupsArray[0].bounds.Lo));
            modelBound.merge(modelPosition.Transform(raw_model.groupsArray[0].bounds.Hi));

            spawn.iBound = modelBound + spawn.iPos;
            spawn.flags |= ModelFlags.HasBound;
            return true;
        }

        void ConvertRawFile(string pModelFilename)
        {
            string filename = sourceDirectory + pModelFilename;

            WorldModel_Raw raw_model = new();
            if (!raw_model.Read(filename))
                return;

            // write WorldModel
            WorldModel model = new();
            model.setRootWmoID(raw_model.RootWMOID);
            if (!raw_model.groupsArray.Empty())
            {
                List<GroupModel> groupsArray = new();

                int groups = raw_model.groupsArray.Length;
                for (uint g = 0; g < groups; ++g)
                {
                    GroupModel_Raw raw_group = raw_model.groupsArray[g];
                    var groupModel = new GroupModel(raw_group.mogpflags, raw_group.GroupWMOID, raw_group.bounds);
                    groupModel.SetMeshData(raw_group.vertexArray, raw_group.triangles.ToList());
                    groupModel.SetLiquidData(raw_group.liquid);
                    groupsArray.Add(groupModel);
                }

                model.setGroupModels(groupsArray);
            }

            if (!pModelFilename.Contains('\0'))
                pModelFilename += ".vmo";

            model.writeFile(destinationDirectory  + "/" + pModelFilename);
        }

        void ExportGameobjectModels()
        {
            if (!File.Exists(sourceDirectory + "/" + "temp_gameobject_models"))
                return;

            using BinaryReader reader = new(File.Open(sourceDirectory + "/" + "temp_gameobject_models", FileMode.Open, FileAccess.Read, FileShare.Read));
            string magic = reader.ReadCString();
            if (magic != SharedConst.RAW_VMAP_MAGIC)
                return;

            using BinaryWriter writer = new(File.OpenWrite(destinationDirectory + "/" + "GameObjectModels.dtree"));
            writer.WriteString(SharedConst.VMAP_MAGIC);

            long fileLength = reader.BaseStream.Length;
            while (reader.BaseStream.Position < fileLength)
            {
                uint displayId = reader.ReadUInt32();
                bool isWmo = reader.ReadBoolean();
                int nameLen = reader.ReadInt32();
                string modelName = reader.ReadString(nameLen);

                WorldModel_Raw rawModel = new();
                if (!rawModel.Read($"{sourceDirectory}/{modelName}"))
                    continue;

                spawnedModelFiles.Add(modelName);

                AxisAlignedBox bounds = AxisAlignedBox.NaN;
                bool boundEmpty = true;
                for (uint i = 0; i < rawModel.groupsArray.Length; ++i)
                {
                    var vertices = rawModel.groupsArray[i].vertexArray;
                    if (vertices == null)
                        continue;

                    foreach (var vertice in vertices)
                    {
                        if (boundEmpty)
                        {
                            bounds = new AxisAlignedBox(vertice, vertice);
                            boundEmpty = false;
                        }
                        else
                            bounds.merge(vertice);
                    }
                }

                if (bounds.isEmpty())
                {
                    Console.WriteLine($"Model {modelName} has empty bounding box");
                    continue;
                }

                if (bounds.isFinite())
                {
                    Console.WriteLine($"Model {modelName} has invalid bounding box");
                    continue;
                }

                writer.Write(displayId);
                writer.Write(isWmo);
                writer.Write(nameLen);
                writer.WriteString(modelName + '\0');
                writer.WriteVector3(bounds.Lo);
                writer.WriteVector3(bounds.Hi);
            }

        }

        string destinationDirectory ;
        string sourceDirectory;

        Dictionary<uint, MapSpawns> mapData = new();
        HashSet<string> spawnedModelFiles = new();
    }

    public class MapSpawns
    {
        public MapSpawns(uint mapId)
        {
            MapId = mapId;
        }

        public uint MapId;
        public SortedDictionary<uint, ModelSpawn> UniqueEntries = new();
        public MultiMap<uint, TileSpawn> TileEntries = new();
        public MultiMap<uint, TileSpawn> ParentTileEntries = new();
    }

    class ModelPosition
    {
        public void Init()
        {
            iRotation = Matrix3.fromEulerAnglesZYX(MathF.PI * iDir.Y / 180.0f, MathF.PI * iDir.X / 180.0f, MathF.PI * iDir.Z / 180.0f);
            iPos = new();
        }

        public Vector3 Transform(Vector3 pIn)
        {
            Vector3 outVec = pIn * iScale;
            outVec = iRotation * outVec;
            return (outVec);
        }

        public Matrix3 iRotation;
        public Vector3 iPos;
        public Vector3 iDir;
        public float iScale;
    }

    class WorldModel_Raw
    {
        public bool Read(string path)
        {
            path = path.TrimEnd('\0');
            if (!File.Exists(path))
            {
                Console.WriteLine($"ERROR: Can't open raw model file: {path}");
                return false;
            }

            using BinaryReader binaryReader = new(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));
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

            string blockId = new(reader.ReadChars(4));
            if (blockId != "GRP ")
            {
                Console.WriteLine($"Error: {blockId} != GRP {reader.BaseStream.Position}");
                return false;
            }

            int blocksize = reader.ReadInt32();
            uint branches = reader.ReadUInt32();
            for (int i = 0; i < branches; ++i)
                reader.ReadUInt32();

            // ---- indexes
            blockId = new(reader.ReadChars(4));
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
            blockId = new(reader.ReadChars(4));
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
        public List<MeshTriangle> triangles = new();
        public List<Vector3> vertexArray = new();
        public WmoLiquid liquid;
    }

    public struct TileSpawn
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
