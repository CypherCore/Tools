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
using System;
using System.Collections.Generic;
using System.IO;

namespace DataExtractor.Framework.Collision
{
    public struct MeshTriangle
    {
        public MeshTriangle(uint na, uint nb, uint nc)
        {
            idx0 = na;
            idx1 = nb;
            idx2 = nc;
        }

        public uint idx0;
        public uint idx1;
        public uint idx2;
    }

    public class WmoLiquid
    {
        public WmoLiquid() { }
        public WmoLiquid(uint width, uint height, Vector3 corner, uint type)
        {
            iTilesX = width;
            iTilesY = height;
            iCorner = corner;
            iType = type;

            if (width != 0 && height != 0)
            {
                iHeight = new float[(width + 1) * (height + 1)];
                iFlags = new byte[width * height];
            }
            else
            {
                iHeight = new float[1];
                iFlags = null;
            }
        }
        public WmoLiquid(WmoLiquid other)
        {
            if (this == other)
                return;

            iTilesX = other.iTilesX;
            iTilesY = other.iTilesY;
            iCorner = other.iCorner;
            iType = other.iType;
            if (other.iHeight != null)
            {
                iHeight = new float[(iTilesX + 1) * (iTilesY + 1)];
                Buffer.BlockCopy(other.iHeight, 0, iHeight, 0, (int)((iTilesX + 1) * (iTilesY + 1)));
            }
            else
                iHeight = null;
            if (other.iFlags != null)
            {
                iFlags = new byte[iTilesX * iTilesY];
                Buffer.BlockCopy(other.iFlags, 0, iFlags, 0, (int)(iTilesX * iTilesY));
            }
            else
                iFlags = null;
        }

        public uint GetFileSize()
        {
            return 2 * sizeof(uint) + (sizeof(float) * 3) +
                sizeof(uint) + (iFlags != null ? ((iTilesX + 1) * (iTilesY + 1) * sizeof(float) + iTilesX * iTilesY) : sizeof(float));
        }

        public bool WriteToFile(BinaryWriter writer)
        {
            writer.Write(iTilesX);
            writer.Write(iTilesY);

            writer.Write(iCorner.X);
            writer.Write(iCorner.Y);
            writer.Write(iCorner.Z);
            writer.Write(iType);

            if (iTilesX != 0 && iTilesY != 0)
            {
                uint size = (iTilesX + 1) * (iTilesY + 1);
                for (var i = 0; i < size; i++)
                    writer.Write(iHeight[i]);

                size = iTilesX * iTilesY;
                for (var i = 0; i < size; i++)
                    writer.Write(iFlags[i]);
            }
            else
                writer.Write(iHeight[0]);

            return true;
        }

        public static WmoLiquid ReadFromFile(BinaryReader reader)
        {
            WmoLiquid liquid = new WmoLiquid();

            liquid.iTilesX = reader.ReadUInt32();
            liquid.iTilesY = reader.ReadUInt32();
            liquid.iCorner = reader.Read<Vector3>();
            liquid.iType = reader.ReadUInt32();

            if (liquid.iTilesX != 0 && liquid.iTilesY != 0)
            {
                uint size = (liquid.iTilesX + 1) * (liquid.iTilesY + 1);
                liquid.iHeight = new float[size];
                for (var i = 0; i < size; i++)
                    liquid.iHeight[i] = reader.ReadSingle();

                size = liquid.iTilesX * liquid.iTilesY;
                liquid.iFlags = new byte[size];
                for (var i = 0; i < size; i++)
                    liquid.iFlags[i] = reader.ReadByte();
            }
            else
            {
                liquid.iHeight = new float[1];
                liquid.iHeight[0] = reader.ReadSingle();
            }

            return liquid;
        }

        public void GetPosInfo(out uint tilesX, out uint tilesY, out Vector3 corner)
        {
            tilesX = iTilesX;
            tilesY = iTilesY;
            corner = iCorner;
        }

        public uint GetLiquidType() { return iType; }

        uint iTilesX;
        uint iTilesY;
        Vector3 iCorner;
        public uint iType;
        public float[] iHeight;
        public byte[] iFlags;
    }

    public class GroupModel
    {
        public GroupModel()
        {
            iLiquid = null;
        }
        public GroupModel(uint mogpFlags, uint groupWMOID, AxisAlignedBox bound)
        {
            iBound = bound;
            iMogpFlags = mogpFlags;
            iGroupWMOID = groupWMOID;
            iLiquid = null;
        }

        public void SetMeshData(List<Vector3> vert, List<MeshTriangle> tri)
        {
            vertices = vert;
            triangles = tri;
            TriBoundFunc bFunc = new TriBoundFunc(vertices);
            meshTree.build(triangles, bFunc.Invoke);
        }

        public void SetLiquidData(WmoLiquid liquid)
        {
            iLiquid = liquid;
            liquid = null;
        }

        public void WriteToFile(BinaryWriter writer)
        {
            int count;

            writer.WriteVector3(iBound.Lo);
            writer.WriteVector3(iBound.Hi);
            writer.Write(iMogpFlags);
            writer.Write(iGroupWMOID);

            // write vertices
            writer.WriteString("VERT");
            count = vertices.Count;
            writer.Write(4 + 12 * count);
            writer.Write(count);
            if (count == 0) // models without (collision) geometry end here, unsure if they are useful
                return;

            foreach (var vector in vertices)
                writer.WriteVector3(vector);

            // write triangle mesh
            writer.WriteString("TRIM");
            count = triangles.Count;
            writer.Write(4 + 12 * count);
            writer.Write(count);
            foreach (var triangle in triangles)
            {
                writer.Write(triangle.idx0);
                writer.Write(triangle.idx1);
                writer.Write(triangle.idx2);
            }

            // write mesh BIH
            writer.WriteString("MBIH");
            meshTree.writeToFile(writer);

            // write liquid data
            writer.WriteString("LIQU");
            if (iLiquid == null)
            {
                writer.Write(0);
                return;
            }

            writer.Write(iLiquid.GetFileSize());
            iLiquid.WriteToFile(writer);
        }

        public bool ReadFromFile(BinaryReader reader)
        {
            uint chunkSize = 0;
            uint count = 0;
            triangles.Clear();
            vertices.Clear();
            iLiquid = null;

            iBound = reader.Read<AxisAlignedBox>();
            iMogpFlags = reader.ReadUInt32();
            iGroupWMOID = reader.ReadUInt32();

            // read vertices
            if (reader.ReadStringFromChars(4) != "VERT")
                return false;

            chunkSize = reader.ReadUInt32();
            count = reader.ReadUInt32();
            if (count == 0)
                return false;

            for (var i = 0; i < count; ++i)
                vertices.Add(reader.Read<Vector3>());

            // read triangle mesh
            if (reader.ReadStringFromChars(4) != "TRIM")
                return false;

            chunkSize = reader.ReadUInt32();
            count = reader.ReadUInt32();

            for (var i = 0; i < count; ++i)
                triangles.Add(reader.Read<MeshTriangle>());

            // read mesh BIH
            if (reader.ReadStringFromChars(4) != "MBIH")
                return false;
            meshTree.readFromFile(reader);

            // write liquid data
            if (reader.ReadStringFromChars(4).ToString() != "LIQU")
                return false;
            chunkSize = reader.ReadUInt32();
            if (chunkSize > 0)
                iLiquid = WmoLiquid.ReadFromFile(reader);

            return true;
        }

        public void GetMeshData(out List<Vector3> outVertices, out List<MeshTriangle> outTriangles, out WmoLiquid liquid)
        {
            outVertices = vertices;
            outTriangles = triangles;
            liquid = iLiquid;
        }

        public AxisAlignedBox GetBound() { return iBound; }

        AxisAlignedBox iBound = AxisAlignedBox.NaN;
        uint iMogpFlags;
        uint iGroupWMOID;
        List<Vector3> vertices = new List<Vector3>();
        List<MeshTriangle> triangles = new List<MeshTriangle>();
        BIH meshTree = new BIH();
        WmoLiquid iLiquid;
    }

    struct BoundsTrait
    {
        public static void GetBounds(GroupModel obj, out AxisAlignedBox box) { box = obj.GetBound(); }
        public static void GetBounds(ModelSpawn obj, out AxisAlignedBox box) { box = obj.GetBounds(); }
    }

    public class WorldModel
    {
        public WorldModel()
        {
            RootWMOID = 0;
        }

        public void setGroupModels(List<GroupModel> models)
        {
            groupModels = models;
            groupTree.build(groupModels, BoundsTrait.GetBounds, 1);
        }

        public void writeFile(string filename)
        {
            using (BinaryWriter writer = new BinaryWriter(File.Open(filename.TrimEnd('\0'), FileMode.Create)))
            {
                writer.WriteString(SharedConst.VMAP_MAGIC);
                writer.WriteString("WMOD");
                writer.Write(4 + 4);
                writer.Write(RootWMOID);

                // write group models
                if (groupModels.Count != 0)
                {
                    writer.WriteString("GMOD");
                    writer.Write(groupModels.Count);
                    for (int i = 0; i < groupModels.Count; ++i)
                        groupModels[i].WriteToFile(writer);

                    // write group BIH
                    writer.WriteString("GBIH");
                    groupTree.writeToFile(writer);
                }
            }
        }

        public bool readFile(string filename)
        {
            if (!File.Exists(filename))
            {
                filename = filename + ".vmo";
                if (!File.Exists(filename))
                    return false;
            }

            using (BinaryReader binaryReader = new BinaryReader(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                uint chunkSize = 0;
                uint count = 0;
                if (binaryReader.ReadStringFromChars(8) != SharedConst.VMAP_MAGIC)
                    return false;

                if (binaryReader.ReadStringFromChars(4) != "WMOD")
                    return false;
                chunkSize = binaryReader.ReadUInt32();
                RootWMOID = binaryReader.ReadUInt32();

                // read group models
                if (binaryReader.ReadStringFromChars(4) != "GMOD")
                    return false;

                count = binaryReader.ReadUInt32();
                for (var i = 0; i < count; ++i)
                {
                    GroupModel group = new GroupModel();
                    group.ReadFromFile(binaryReader);
                    groupModels.Add(group);
                }

                // read group BIH
                if (binaryReader.ReadStringFromChars(4) != "GBIH")
                    return false;

                groupTree.readFromFile(binaryReader);
                return true;
            }
        }

        public void getGroupModels(out List<GroupModel> outGroupModels)
        {
            outGroupModels = groupModels;
        }

        public void setRootWmoID(uint id) { RootWMOID = id; }

        List<GroupModel> groupModels = new List<GroupModel>();
        BIH groupTree = new BIH();
        uint RootWMOID;
    }
}

