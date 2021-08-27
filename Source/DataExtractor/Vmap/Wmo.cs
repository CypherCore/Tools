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
using DataExtractor.Framework.GameMath;
using DataExtractor.Map;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DataExtractor.Vmap
{
    class WMORoot
    {
        public bool Open(uint fileId) => Read(Program.CascHandler.OpenFile((int)fileId));
        public bool Open(string fileName) => Read(Program.CascHandler.OpenFile(fileName));

        public bool Read(Stream stream)
        {
            if (stream == null)
            {
                Console.WriteLine("No such file.");
                return false;
            }

            using (BinaryReader reader = new(stream))
            {
                long fileLength = reader.BaseStream.Length;
                while (reader.BaseStream.Position < fileLength)
                {
                    string fourcc = reader.ReadStringFromChars(4, true);
                    uint size = reader.ReadUInt32();

                    int nextpos = (int)(reader.BaseStream.Position + size);
                    if (fourcc == "MOHD") // header
                    {
                        nTextures = reader.ReadUInt32();
                        nGroups = reader.ReadUInt32();
                        nPortals = reader.ReadUInt32();
                        nLights = reader.ReadUInt32();
                        nDoodadNames = reader.ReadUInt32();
                        nDoodadDefs = reader.ReadUInt32();
                        nDoodadSets = reader.ReadUInt32();
                        color = reader.ReadUInt32();
                        RootWMOID = reader.ReadUInt32();

                        for (var i = 0; i < 3; ++i)
                            bbcorn1[i] = reader.ReadSingle();

                        for (var i = 0; i < 3; ++i)
                            bbcorn2[i] = reader.ReadSingle();

                        flags = reader.ReadUInt16();
                        numLod = reader.ReadUInt16();
                    }
                    else if (fourcc == "MODS")
                    {
                        for (var i = 0; i < size / 32; ++i)
                            DoodadData.Sets.Add(reader.ReadStruct<MODS>());
                    }
                    else if (fourcc == "MODN")
                    {
                        DoodadData.Paths = reader.ReadBytes((int)size);

                        using BinaryReader doodadReader = new(new MemoryStream(DoodadData.Paths));
                        int index = 0;
                        long endIndex = doodadReader.BaseStream.Length;
                        while (doodadReader.BaseStream.Position < endIndex)
                        {
                            string path = doodadReader.ReadCString();
                            if (VmapFile.ExtractSingleModel(path))
                                ValidDoodadNames.Add((uint)index);

                            index += path.Length + 1;
                        }
                    }
                    else if (fourcc == "MODI")
                    {
                        uint fileDataIdCount = size / 4;
                        DoodadData.FileDataIds = new uint[size / 4];

                        Buffer.BlockCopy(reader.ReadBytes((int)size), 0, DoodadData.FileDataIds, 0, DoodadData.FileDataIds.Length);
                        for (uint i = 0; i < fileDataIdCount; ++i)
                        {
                            if (DoodadData.FileDataIds[i] == 0)
                                continue;

                            string path = $"FILE{DoodadData.FileDataIds[i]:X8}.xxx";
                            if (VmapFile.ExtractSingleModel(path))
                                ValidDoodadNames.Add(i);
                        }
                    }
                    else if (fourcc == "MODD")
                    {
                        for (var i = 0; i < size / 40; ++i)
                            DoodadData.Spawns.Add(reader.Read<MODD>());
                    }
                    else if (fourcc == "GFID")
                    {
                        for (uint gp = 0; gp < nGroups; ++gp)
                        {
                            uint fileDataId = reader.ReadUInt32();
                            if (fileDataId != 0)
                                groupFileDataIDs.Add(fileDataId);
                        }
                    }

                    reader.BaseStream.Seek(nextpos, SeekOrigin.Begin);
                }
            }
            return true;
        }

        public bool ConvertToVMAPRootWmo(BinaryWriter writer)
        {
            writer.WriteCString(SharedConst.RAW_VMAP_MAGIC);
            writer.Write(0); // will be filled later
            writer.Write(nGroups);
            writer.Write(RootWMOID);
            return true;
        }

        public static void Extract(MODF.SMMapObjDef mapObjDef, string wmoInstanceName, bool isGlobalWmo, uint mapID, uint originalMapId, BinaryWriter writer, List<ADTOutputCache> dirfileCache)
        {
            // destructible wmo, do not dump. we can handle the vmap for these
            // in dynamic tree (gameobject vmaps)
            if (mapObjDef.Flags.HasAnyFlag(MODFFlags.Destroyable))
                return;

            if (!File.Exists(Program.BuildingsDirectory + wmoInstanceName))
            {
                Console.WriteLine($"WMOInstance.WMOInstance: couldn't open {wmoInstanceName}");
                return;
            }

            //-----------add_in _dir_file----------------
            using BinaryReader binaryReader = new(File.Open(Program.BuildingsDirectory + wmoInstanceName, FileMode.Open, FileAccess.Read, FileShare.Read));
            binaryReader.BaseStream.Seek(8, SeekOrigin.Begin); // get the correct no of vertices
            int nVertices = binaryReader.ReadInt32();
            if (nVertices == 0)
                return;

            Vector3 position = fixCoords(mapObjDef.Position);
            AxisAlignedBox bounds = new(fixCoords(mapObjDef.Bounds.Lo), fixCoords(mapObjDef.Bounds.Hi));

            if (isGlobalWmo)
            {
                position += new Vector3(533.33333f * 32, 533.33333f * 32, 0.0f);
                bounds += new Vector3(533.33333f * 32, 533.33333f * 32, 0.0f);
            }

            float scale = 1.0f;
            if (mapObjDef.Flags.HasAnyFlag(MODFFlags.UnkHasScale))
                scale = mapObjDef.Scale / 1024.0f;
            uint uniqueId = VmapFile.GenerateUniqueObjectId(mapObjDef.UniqueId, 0);
            uint flags = ModelFlags.HasBound;
            if (mapID != originalMapId)
                flags |= ModelFlags.ParentSpawn;

            if (uniqueId == 198660)
            {

            }

            //write mapID, Flags, ID, Pos, Rot, Scale, Bound_lo, Bound_hi, name
            writer.Write(mapID);
            writer.Write(flags);
            writer.Write(mapObjDef.NameSet);
            writer.Write(uniqueId);
            writer.WriteVector3(position);
            writer.WriteVector3(mapObjDef.Rotation);
            writer.Write(scale);
            writer.WriteVector3(bounds.Lo);
            writer.WriteVector3(bounds.Hi);
            writer.Write(wmoInstanceName.GetByteCount());
            writer.WriteString(wmoInstanceName);

            if (dirfileCache != null)
            {
                ADTOutputCache cacheModelData = new();
                cacheModelData.Flags = flags & ~ModelFlags.ParentSpawn;

                MemoryStream stream = new();
                BinaryWriter cacheData = new(stream);
                cacheData.Write(mapObjDef.NameSet);
                cacheData.Write(uniqueId);
                cacheData.WriteVector3(position);
                cacheData.WriteVector3(mapObjDef.Rotation);
                cacheData.Write(scale);
                cacheData.WriteVector3(bounds.Lo);
                cacheData.WriteVector3(bounds.Hi);
                cacheData.Write(wmoInstanceName.GetByteCount());
                cacheData.WriteString(wmoInstanceName);

                cacheModelData.Data = stream.ToArray();
                dirfileCache.Add(cacheModelData);
            }
        }

        static Vector3 fixCoords(Vector3 v) { return new Vector3(v.Z, v.X, v.Y); }

        uint color;
        uint nTextures;
        uint nGroups;
        uint nPortals;
        uint nLights;
        uint nDoodadNames;
        uint nDoodadDefs;
        uint nDoodadSets;
        uint RootWMOID;
        float[] bbcorn1 = new float[3];
        float[] bbcorn2 = new float[3];
        public ushort flags;
        ushort numLod;

        public WMODoodadData DoodadData = new();
        public List<uint> ValidDoodadNames = new();
        public List<uint> groupFileDataIDs = new();
    }

    class WMOGroup
    {
        public bool Open(uint fileId, WMORoot rootWmo)
        {
            Stream stream = Program.CascHandler.OpenFile((int)fileId);
            if (stream == null)
            {
                Console.WriteLine("No such file.");
                return false;
            }

            using (BinaryReader reader = new(stream))
            {
                long fileLength = reader.BaseStream.Length;
                while (reader.BaseStream.Position < fileLength)
                {
                    string fourcc = reader.ReadStringFromChars(4, true);
                    uint size = reader.ReadUInt32();

                    if (fourcc == "MOGP")//Fix sizeoff = Data size.
                        size = 68;

                    long nextpos = reader.BaseStream.Position + size;

                    if (fourcc == "MOGP")//header
                    {
                        groupName = reader.ReadInt32();
                        descGroupName = reader.ReadInt32();
                        mogpFlags = reader.ReadInt32();

                        for (var i = 0; i < 3; ++i)
                            bbcorn1[i] = reader.ReadSingle();

                        for (var i = 0; i < 3; ++i)
                            bbcorn2[i] = reader.ReadSingle();

                        moprIdx = reader.ReadUInt16();
                        moprNItems = reader.ReadUInt16();
                        nBatchA = reader.ReadUInt16();
                        nBatchB = reader.ReadUInt16();
                        nBatchC = reader.ReadUInt32();
                        fogIdx = reader.ReadUInt32();
                        groupLiquid = reader.ReadUInt32();
                        groupWMOID = reader.ReadUInt32();

                        // according to WoW.Dev Wiki:
                        if (Convert.ToBoolean(rootWmo.flags & 4))
                            groupLiquid = GetLiquidTypeId(groupLiquid);
                        else if (groupLiquid == 15)
                            groupLiquid = 0;
                        else
                            groupLiquid = GetLiquidTypeId(groupLiquid + 1);

                        if (groupLiquid != 0)
                            liquflags |= 2;
                    }
                    else if (fourcc == "MOPY")
                    {
                        mopy_size = (int)size;
                        nTriangles = (int)size / 2;
                        MOPY = reader.ReadString((int)size);
                    }
                    else if (fourcc == "MOVI")
                    {
                        MOVI = new ushort[size / 2];
                        for (var i = 0; i < size / 2; ++i)
                            MOVI[i] = reader.ReadUInt16();
                    }
                    else if (fourcc == "MOVT")
                    {
                        MOVT = new float[size / 4];
                        for (var i = 0; i < size / 4; ++i)
                            MOVT[i] = reader.ReadSingle();

                        nVertices = size / 12;
                    }
                    else if (fourcc == "MONR")
                    {
                    }
                    else if (fourcc == "MOTV")
                    {
                    }
                    else if (fourcc == "MOBA")
                    {
                        MOBA = new ushort[size / 2];
                        moba_size = (int)(size / 2);

                        for (var i = 0; i < size / 2; ++i)
                            MOBA[i] = reader.ReadUInt16();
                    }
                    else if (fourcc == "MODR")
                    {
                        for (var i = 0; i < size / 2; ++i)
                            DoodadReferences.Add(reader.Read<ushort>());
                    }
                    else if (fourcc == "MLIQ")
                    {
                        liquflags |= 1;
                        hlq = reader.Read<WMOLiquidHeader>();
                        LiquEx_size = 8 * hlq.xverts * hlq.yverts;
                        LiquEx = new WMOLiquidVert[hlq.xverts * hlq.yverts];
                        for (var i = 0; i < hlq.xverts * hlq.yverts; ++i)
                            LiquEx[i] = reader.Read<WMOLiquidVert>();

                        int nLiquBytes = hlq.xtiles * hlq.ytiles;
                        LiquBytes = reader.ReadBytes(nLiquBytes);

                        // Determine legacy liquid type
                        if (groupLiquid == 0)
                        {
                            for (int i = 0; i < hlq.xtiles * hlq.ytiles; ++i)
                            {
                                if ((LiquBytes[i] & 0xF) != 15)
                                {
                                    groupLiquid = GetLiquidTypeId((uint)(LiquBytes[i] & 0xF) + 1);
                                    break;
                                }
                            }
                        }

                        /* std::ofstream llog("Buildings/liquid.log", ios_base::out | ios_base::app);
                        llog << filename;
                        llog << "\nbbox: " << bbcorn1[0] << ", " << bbcorn1[1] << ", " << bbcorn1[2] << " | " << bbcorn2[0] << ", " << bbcorn2[1] << ", " << bbcorn2[2];
                        llog << "\nlpos: " << hlq->pos_x << ", " << hlq->pos_y << ", " << hlq->pos_z;
                        llog << "\nx-/yvert: " << hlq->xverts << "/" << hlq->yverts << " size: " << size << " expected size: " << 30 + hlq->xverts*hlq->yverts*8 + hlq->xtiles*hlq->ytiles << std::endl;
                        llog.close(); */
                    }
                    reader.BaseStream.Seek((int)nextpos, SeekOrigin.Begin);
                }
            }

            return true;
        }

        public int ConvertToVMAPGroupWmo(BinaryWriter writer, bool preciseVectorData)
        {
            writer.Write(mogpFlags);
            writer.Write(groupWMOID);
            // group bound
            for (var i = 0; i < 3; ++i)
                writer.Write(bbcorn1[i]);

            for (var i = 0; i < 3; ++i)
                writer.Write(bbcorn2[i]);

            writer.Write(liquflags);

            writer.WriteString("GRP ");
            writer.Write(0);
            writer.Write(0);

            //-------INDX------------------------------------
            //-------MOPY--------
            MoviEx = new ushort[nTriangles * 3]; // "worst case" size...
            int[] IndexRenum = new int[nVertices];
            for (var i = 0; i < nVertices; ++i)
                IndexRenum[i] = -1;

            int nColTriangles = 0;
            for (int i = 0; i < nTriangles; ++i)
            {
                // Skip no collision triangles
                bool isRenderFace = Convert.ToBoolean(MOPY[2 * i] & (int)MopyFlags.Render) && !Convert.ToBoolean(MOPY[2 * i] & (int)MopyFlags.Detail);
                bool isCollision = (MOPY[2 * i] & (int)MopyFlags.Collision) != 0 || isRenderFace;

                if (!isCollision)
                    continue;

                // Use this triangle
                for (int j = 0; j < 3; ++j)
                {
                    IndexRenum[MOVI[3 * i + j]] = 1;
                    MoviEx[3 * nColTriangles + j] = MOVI[3 * i + j];
                }
                ++nColTriangles;
            }

            // assign new vertex index numbers
            int nColVertices = 0;
            for (uint i = 0; i < nVertices; ++i)
            {
                if (IndexRenum[i] == 1)
                {
                    IndexRenum[i] = nColVertices;
                    ++nColVertices;
                }
            }

            // translate triangle indices to new numbers
            for (int i = 0; i < 3 * nColTriangles; ++i)
            {
                //assert(MoviEx[i] < nVertices);
                MoviEx[i] = (ushort)IndexRenum[MoviEx[i]];
            }

            // write triangle indices
            writer.WriteString("INDX");
            writer.Write(nColTriangles * 6 + 4);
            writer.Write(nColTriangles * 3);

            for (var i = 0; i < nColTriangles * 3; ++i)
                writer.Write(MoviEx[i]);

            // write vertices
            writer.WriteString("VERT");
            writer.Write(nColVertices * 3 * 4 + 4);
            writer.Write(nColVertices);
            for (uint i = 0; i < nVertices; ++i)
            {
                if (IndexRenum[i] >= 0)
                {
                    writer.Write(MOVT[3 * i]);
                    writer.Write(MOVT[3 * i + 1]);
                    writer.Write(MOVT[3 * i + 2]);
                }
            }

            //assert(check == 0);

            //------LIQU------------------------
            if (Convert.ToBoolean(liquflags & 3))
            {
                int LIQU_totalSize = 4;
                if (Convert.ToBoolean(liquflags & 1))
                {
                    LIQU_totalSize += 30;
                    LIQU_totalSize += LiquEx_size / 8 * sizeof(float);
                    LIQU_totalSize += hlq.xtiles * hlq.ytiles;
                }

                writer.WriteString("LIQU");
                writer.Write(LIQU_totalSize);
                writer.Write(groupLiquid);
                if (Convert.ToBoolean(liquflags & 1))
                {
                    writer.WriteStruct(hlq);
                    // only need height values, the other values are unknown anyway
                    for (uint i = 0; i < LiquEx_size / 8; ++i)
                        writer.Write(LiquEx[i].height);
                    // todo: compress to bit field
                    writer.Write(LiquBytes, 0, hlq.xtiles * hlq.ytiles);
                }
            }

            return nColTriangles;
        }

        uint GetLiquidTypeId(uint liquidTypeId)
        {
            if (liquidTypeId < 21 && liquidTypeId != 0)
            {
                switch (((liquidTypeId - 1) & 3))
                {
                    case 0:
                        return (uint)((((mogpFlags & 0x80000) != 0) ? 1 : 0) + 13);
                    case 1:
                        return 14;
                    case 2:
                        return 19;
                    case 3:
                        return 20;
                    default:
                        break;
                }
            }
            return liquidTypeId;
        }

        // MOGP
        string MOPY;
        ushort[] MOVI;
        ushort[] MoviEx;
        float[] MOVT;
        ushort[] MOBA;
        int[] MobaEx;
        WMOLiquidHeader hlq;
        WMOLiquidVert[] LiquEx;
        byte[] LiquBytes;
        int groupName;
        int descGroupName;
        int mogpFlags;
        float[] bbcorn1 = new float[3];
        float[] bbcorn2 = new float[3];
        ushort moprIdx;
        ushort moprNItems;
        ushort nBatchA;
        ushort nBatchB;
        uint nBatchC;
        uint fogIdx;
        uint groupLiquid;
        uint groupWMOID;

        int mopy_size;
        int moba_size;
        int LiquEx_size;
        uint nVertices; // number when loaded
        int nTriangles; // number when loaded
        uint liquflags;

        public List<ushort> DoodadReferences = new();

        struct WMOLiquidVert
        {
            public ushort unk1 { get; set; }
            public ushort unk2 { get; set; }
            public float height { get; set; }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct WMOLiquidHeader
    {
        public int xverts { get; set; }
        public int yverts { get; set; }
        public int xtiles { get; set; }
        public int ytiles { get; set; }
        public float pos_x { get; set; }
        public float pos_y { get; set; }
        public float pos_z { get; set; }
        public short material { get; set; }
    }

    class WMODoodadData
    {
        public List<MODS> Sets = new();
        public byte[] Paths;
        public uint[] FileDataIds;
        public List<MODD> Spawns = new();
        public List<ushort> References = new();
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MODS
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string Name;
        public uint StartIndex;     // index of first doodad instance in this set
        public uint Count;          // number of doodad instances in this set
        public uint _pad;
    }

    struct MODD
    {
        public uint NameIndex { get; set; }
        public Vector3 Position { get; set; }
        public Vector4 Rotation { get; set; }
        public float Scale { get; set; }
        public uint Color { get; set; }
    }
}
