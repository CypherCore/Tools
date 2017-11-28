using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Numerics;

namespace DataExtractor
{
    class WMORoot
    {
        public bool open(uint fileId)
        {
            return Read(Program.cascHandler.ReadFile((int)fileId));
        }

        public bool open(string fileName)
        {
            return Read(Program.cascHandler.ReadFile(fileName));
        }

        public bool Read(MemoryStream stream)
        {
            if (stream == null)
            {
                Console.WriteLine("No such file.");
                return false;
            }

            using (BinaryReader reader = new BinaryReader(stream))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    string fourcc = reader.ReadStringFromChars(4);
                    uint size = reader.ReadUInt32();

                    int nextpos = (int)(reader.BaseStream.Position + size);
                    if (fourcc == "DHOM") // header
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
                    else if (fourcc == "DIFG")
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
            //printf("Convert RootWmo...\n");

            writer.WriteCString("VMAP045");
            writer.Write(0); // will be filled later
            writer.Write(nGroups);
            writer.Write(RootWMOID);
            return true;
        }

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

        public List<uint> groupFileDataIDs = new List<uint>();
    }

    class WMOGroup
    {
        public bool open(uint fileId)
        {
            MemoryStream stream = Program.cascHandler.ReadFile((int)fileId);
            if (stream == null)
            {
                Console.WriteLine("No such file.");
                return false;
            }

            using (BinaryReader reader = new BinaryReader(stream))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    string fourcc = reader.ReadStringFromChars(4, true);
                    uint size = reader.ReadUInt32();

                    if (fourcc == "MOGP")//Fix sizeoff = Data size.
                        size = 68;

                    long nextpos = reader.BaseStream.Position + size;
                    LiquEx_size = 0;
                    liquflags = 0;

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
                        liquidType = reader.ReadUInt32();
                        groupWMOID = reader.ReadUInt32();

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
                    else if (fourcc == "MLIQ")
                    {
                        liquflags |= 1;
                        hlq = reader.ReadStruct<WMOLiquidHeader>(30);
                        LiquEx_size = 8 * hlq.xverts * hlq.yverts;
                        LiquEx = new WMOLiquidVert[hlq.xverts * hlq.yverts];
                        for (var i = 0; i < hlq.xverts * hlq.yverts; ++i)
                            LiquEx[i] = reader.ReadStruct<WMOLiquidVert>();

                        int nLiquBytes = hlq.xtiles * hlq.ytiles;
                        LiquBytes = reader.ReadBytes(nLiquBytes);

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

        public int ConvertToVMAPGroupWmo(BinaryWriter writer, WMORoot rootWMO, bool preciseVectorData)
        {
            writer.Write(mogpFlags);
            writer.Write(groupWMOID);
            // group bound
            for (var i = 0; i < 3; ++i)
                writer.Write(bbcorn1[i]);
            for (var i = 0; i < 3; ++i)
                writer.Write(bbcorn2[i]);
            writer.Write(liquflags);
            int nColTriangles = 0;
            if (preciseVectorData)
            {
                writer.WriteString("GRP ");

                int k = 0;
                int moba_batch = moba_size / 12;
                MobaEx = new int[moba_batch * 4];
                for (int i = 8; i < moba_size; i += 12)
                {
                    MobaEx[k++] = MOBA[i];
                }
                int moba_size_grp = moba_batch * 4 + 4;
                writer.Write(moba_size_grp);
                writer.Write(moba_batch);
                for (var i = 0; i < k; ++i)
                    writer.Write(MobaEx[i]);

                int nIdexes = nTriangles * 3;

                writer.WriteString("INDX");
                int wsize = 4 + 2 * nIdexes;
                writer.Write(wsize);
                writer.Write(nIdexes);
                if (nIdexes > 0)
                    for (var i = 0; i < nIdexes; ++i)
                        writer.Write(MOVI[i]);

                writer.WriteString("VERT");
                wsize = (int)(4 + 4 * 3 * nVertices);
                writer.Write(wsize);
                writer.Write(nVertices);
                if (nVertices > 0)
                    for (var i = 0; i < nVertices; ++i)//May need nVertices * 3
                        writer.Write(MOVT[i]);

                nColTriangles = nTriangles;
            }
            else
            {
                writer.WriteString("GRP ");
                int k = 0;
                int moba_batch = moba_size / 12;
                MobaEx = new int[moba_batch * 4];
                for (int i = 8; i < moba_size; i += 12)
                {
                    MobaEx[k++] = MOBA[i];
                }

                int moba_size_grp = moba_batch * 4 + 4;
                writer.Write(moba_size_grp);
                writer.Write(moba_batch);
                for (var i = 0; i < k; ++i)
                    writer.Write(MobaEx[i]);

                //-------INDX------------------------------------
                //-------MOPY--------
                MoviEx = new ushort[nTriangles * 3]; // "worst case" size...
                int[] IndexRenum = new int[nVertices];
                for (var i = 0; i < nVertices; ++i)
                    IndexRenum[i] = 0xFF;
                for (int i = 0; i < nTriangles; ++i)
                {
                    // Skip no collision triangles
                    bool isRenderFace = Convert.ToBoolean(MOPY[2 * i] & (int)MopyFlags.Render) && !Convert.ToBoolean(MOPY[2 * i] & (int)MopyFlags.Detail);
                    bool isDetail = (MOPY[2 * i] & (int)MopyFlags.Detail) != 0;
                    bool isCollision = (MOPY[2 * i] & (int)MopyFlags.Collision) != 0;

                    if (!isRenderFace && !isDetail && !isCollision)
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
                writer.Write(0x58444E49);
                writer.Write(nColTriangles * 6 + 4);
                writer.Write(nColTriangles * 3);

                for (var i = 0; i< nColTriangles * 3; ++i)
                    writer.Write(MoviEx[i]);

                // write vertices
                int check = 3 * nColVertices;
                writer.Write(0x54524556);
                writer.Write( nColVertices * 3 * 4 + 4);
                writer.Write(nColVertices);
                for (uint i = 0; i < nVertices; ++i)
                {
                    if (IndexRenum[i] >= 0)
                    {
                        writer.Write(MOVT[3 * i]);
                        writer.Write(MOVT[3 * i + 1]);
                        writer.Write(MOVT[3 * i + 2]);
                        check -= 4;
                    }
                }

                //assert(check == 0);
            }

            //------LIQU------------------------
            if (LiquEx_size != 0)
            {
                writer.Write(0x5551494C);
                writer.Write((32 + LiquEx_size) + hlq.xtiles * hlq.ytiles);

                // according to WoW.Dev Wiki:
                uint liquidEntry;
                if (Convert.ToBoolean(rootWMO.flags & 4))
                    liquidEntry = liquidType;
                else if (liquidType == 15)
                    liquidEntry = 0;
                else
                    liquidEntry = liquidType + 1;

                if (liquidEntry == 0)
                {
                    int v1; // edx@1
                    int v2; // eax@1

                    v1 = hlq.xtiles * hlq.ytiles;
                    v2 = 0;
                    if (v1 > 0)
                    {
                        while ((LiquBytes[v2] & 0xF) == 15)
                        {
                            ++v2;
                            if (v2 >= v1)
                                break;
                        }

                        if (v2 < v1 && (LiquBytes[v2] & 0xF) != 15)
                            liquidEntry = (LiquBytes[v2] & 0xFu) + 1;
                    }
                }

                if (liquidEntry != 0 && liquidEntry < 21)
                {
                    switch ((liquidEntry - 1) & 3)
                    {
                        case 0:
                            liquidEntry = ((mogpFlags & 0x80000) != 0) ? 1 : 0 + 13u;
                            break;
                        case 1:
                            liquidEntry = 14;
                            break;
                        case 2:
                            liquidEntry = 19;
                            break;
                        case 3:
                            liquidEntry = 20;
                            break;
                    }
                }

                hlq.type = (short)liquidEntry;

                /* std::ofstream llog("Buildings/liquid.log", ios_base::out | ios_base::app);
                llog << filename;
                llog << ":\nliquidEntry: " << liquidEntry << " type: " << hlq->type << " (root:" << rootWMO->flags << " group:" << flags << ")\n";
                llog.close(); */

                writer.WriteStruct(hlq);
                // only need height values, the other values are unknown anyway
                for (uint i = 0; i < LiquEx_size / 8; ++i)
                    writer.Write(LiquEx[i].height);
                // todo: compress to bit field
                writer.Write(LiquBytes, 0, hlq.xtiles * hlq.ytiles);
            }

            return nColTriangles;
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
        uint liquidType;
        uint groupWMOID;

        int mopy_size;
        int moba_size;
        int LiquEx_size;
        uint nVertices; // number when loaded
        int nTriangles; // number when loaded
        uint liquflags;

        struct WMOLiquidHeader
        {
            public int xverts;
            public int yverts;
            public int xtiles;
            public int ytiles;
            public float pos_x;
            public float pos_y;
            public float pos_z;
            public short type;
        }

        struct WMOLiquidVert
        {
            public ushort unk1;
            public ushort unk2;
            public float height;
        };
    }

    class WMOInstance
    {
        public WMOInstance(BinaryReader reader, string WmoInstName, uint mapID, uint tileX, uint tileY, BinaryWriter writer)
        {
            id = reader.ReadUInt32();

            float[] ff = new float[3];
            pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            rot = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            pos2 = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); // bounding box corners
            pos3 = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());// bounding box corners

            ushort fflags = reader.ReadUInt16();
            ushort doodadSet = reader.ReadUInt16();
            ushort adtId = reader.ReadUInt16();
            ushort trash = reader.ReadUInt16();

            // destructible wmo, do not dump. we can handle the vmap for these
            // in dynamic tree (gameobject vmaps)
            if ((fflags & 0x01) != 0)
                return;

            if (!File.Exists(Program.wmoDirectory + WmoInstName))
            {
                Console.WriteLine($"WMOInstance.WMOInstance: couldn't open {WmoInstName}");
                return;
            }

            //-----------add_in _dir_file----------------
            using (var fs = new FileStream(Program.wmoDirectory + WmoInstName, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader inputReader = new BinaryReader(fs))
                {
                    inputReader.BaseStream.Seek(8, SeekOrigin.Begin); // get the correct no of vertices
                    int nVertices = inputReader.ReadInt32();
                    if (nVertices == 0)
                        return;

                    float x = pos.X;
                    float z = pos.Z;
                    if (x == 0 && z == 0)
                    {
                        pos.X = 533.33333f * 32;
                        pos.Z = 533.33333f * 32;
                    }
                    pos = fixCoords(pos);
                    pos2 = fixCoords(pos2);
                    pos3 = fixCoords(pos3);

                    float scale = 1.0f;
                    uint flags = 1u << 2; //hasbound
                    if (tileX == 65 && tileY == 65) flags |= 1 << 1; //worldspawn
                    //write mapID, tileX, tileY, Flags, ID, Pos, Rot, Scale, Bound_lo, Bound_hi, name
                    writer.Write(mapID);
                    writer.Write(tileX);
                    writer.Write(tileY);
                    writer.Write(flags);
                    writer.Write(adtId);
                    writer.Write(id);
                    writer.WriteVector3(pos);
                    writer.WriteVector3(rot);
                    writer.Write(scale);
                    writer.WriteVector3(pos2);
                    writer.WriteVector3(pos3);
                    writer.Write(WmoInstName.Length);
                    writer.Write(WmoInstName);
                }
            }
        }

        Vector3 fixCoords(Vector3 v) { return new Vector3(v.Z, v.X, v.Y); }

        static List<int> ids = new List<int>();

        string MapName;
        int currx;
        int curry;
        WMOGroup wmo;
        int doodadset;
        Vector3 pos;
        Vector3 pos2;
        Vector3 pos3;
        Vector3 rot;
        uint indx;
        uint id;
    }
}
