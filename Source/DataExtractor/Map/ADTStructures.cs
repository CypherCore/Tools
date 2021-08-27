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
using System.Numerics;

namespace DataExtractor.Map
{
    public class MVER : IMapStruct
    {
        public void Read(byte[] data)
        {
            using BinaryReader reader = new(new MemoryStream(data));
            Version = reader.ReadUInt32();
        }

        public uint Version;
    }

    public class MCNK : IMapStruct
    {
        public void Read(byte[] data)
        {
            using BinaryReader reader = new(new MemoryStream(data));
            Flags = reader.Read<MCNKFlags>();
            IndexX = reader.ReadUInt32();
            IndexY = reader.ReadUInt32();
            LayerCount = reader.ReadUInt32();
            DoodadReferences = reader.ReadUInt32();

            for (var i = 0; i < 8; ++i)
                HighResHoles[i] = reader.ReadByte();

            MCLYOffset = reader.ReadUInt32();        // Texture layer definitions
            MCRFOffset = reader.ReadUInt32();        // A list of indices into the parent file's MDDF chunk
            MCALOffset = reader.ReadUInt32();        // Alpha maps for additional texture layers
            MCALCount = reader.ReadUInt32();
            MCSHOffset = reader.ReadUInt32();        // Shadow map for static shadows on the terrain
            MCSHCount = reader.ReadUInt32();
            AreaID = reader.ReadUInt32();
            MapObjectReferenceCount = reader.ReadUInt32();
            HolesLowRes = reader.ReadUInt32();

            for (var i = 0; i < 2; ++i)
                S[i] = reader.ReadUInt16();

            Data1 = reader.ReadUInt32();
            Data2 = reader.ReadUInt32();
            Data3 = reader.ReadUInt32();
            PredictionTexture = reader.ReadUInt32();
            EffectDoodadCount = reader.ReadUInt32();
            MCSEOffset = reader.ReadUInt32();
            MCSECount = reader.ReadUInt32();
            MCLQOffset = reader.ReadUInt32();         // Liqid level (old)
            MCLQCount = reader.ReadUInt32();         //
            zpos = reader.ReadSingle();
            xpos = reader.ReadSingle();
            ypos = reader.ReadSingle();
            MCCVOffset = reader.ReadUInt32();         // offsColorValues in WotLK
            MCLVOffset = reader.ReadUInt32();
            EffectID = reader.ReadUInt32();
        }

        public MCNKFlags Flags;
        public uint IndexX;
        public uint IndexY;
        public uint LayerCount;
        public uint DoodadReferences;
        public byte[] HighResHoles = new byte[8];
        public uint MCLYOffset;        // Texture layer definitions
        public uint MCRFOffset;        // A list of indices into the parent file's MDDF chunk
        public uint MCALOffset;        // Alpha maps for additional texture layers
        public uint MCALCount;
        public uint MCSHOffset;        // Shadow map for static shadows on the terrain
        public uint MCSHCount;
        public uint AreaID;
        public uint MapObjectReferenceCount;
        public uint HolesLowRes;
        public ushort[] S = new ushort[2];
        public uint Data1;
        public uint Data2;
        public uint Data3;
        public uint PredictionTexture;
        public uint EffectDoodadCount;
        public uint MCSEOffset;
        public uint MCSECount;
        public uint MCLQOffset;            // Liqid level (old)
        public uint MCLQCount;             //
        public float zpos;
        public float xpos;
        public float ypos;
        public uint MCCVOffset;         // offsColorValues in WotLK
        public uint MCLVOffset;
        public uint EffectID;
    }

    public class MCVT : IMapStruct
    {
        public void Read(byte[] data)
        {
            using BinaryReader reader = new(new MemoryStream(data));
            for (var i = 0; i < SharedConst.MCVT_HEIGHT_MAP_SIZE; ++i)
                HeightMap[i] = reader.ReadSingle();
        }

        public float[] HeightMap = new float[SharedConst.MCVT_HEIGHT_MAP_SIZE];
    }

    public class MCLQ : IMapStruct
    {
        public void Read(byte[] data)
        {
            using BinaryReader reader = new(new MemoryStream(data));
            for (var x = 0; x < SharedConst.ADT_CELL_SIZE + 1; ++x)
            {
                Liquid[x] = new LiquidData[SharedConst.ADT_CELL_SIZE + 1];

                for (var y = 0; y < SharedConst.ADT_CELL_SIZE + 1; ++y)
                    Liquid[x][y] = reader.Read<LiquidData>();
            }

            for (var x = 0; x < SharedConst.ADT_CELL_SIZE; ++x)
            {
                Flags[x] = new byte[SharedConst.ADT_CELL_SIZE];

                for (var y = 0; y < SharedConst.ADT_CELL_SIZE; ++y)
                    Flags[x][y] = reader.ReadByte();
            }
        }

        public LiquidData[][] Liquid = new LiquidData[SharedConst.ADT_CELL_SIZE + 1][];

        // 1<<0 - ocean
        // 1<<1 - lava/slime
        // 1<<2 - water
        // 1<<6 - all water
        // 1<<7 - dark water
        // == 0x0F - not show liquid
        public byte[][] Flags = new byte[SharedConst.ADT_CELL_SIZE][];

        public struct LiquidData
        {
            public uint Light;
            public float Height;
        }
    }
    public class MH2O : IMapStruct
    {
        public void Read(byte[] data)
        {
            this.data = data;
            using BinaryReader reader = new(new MemoryStream(data));
            var basePosition = reader.BaseStream.Position;

            for (var x = 0; x < SharedConst.ADT_CELLS_PER_GRID; ++x)
            {
                Liquid[x] = new MH2OHeader[SharedConst.ADT_CELLS_PER_GRID];

                for (var y = 0; y < SharedConst.ADT_CELLS_PER_GRID; ++y)
                    Liquid[x][y] = reader.Read<MH2OHeader>();
            }
        }

        public MH2OHeader[][] Liquid = new MH2OHeader[SharedConst.ADT_CELLS_PER_GRID][];

        byte[] data;

        public MH2OInstance? GetLiquidInstance(int x, int y)
        {
            if (Liquid[x][y].LayerCount != 0 && Liquid[x][y].OffsetInstances != 0)
                return new BinaryReader(new MemoryStream(data, (int)Liquid[x][y].OffsetInstances, data.Length - (int)Liquid[x][y].OffsetInstances)).Read<MH2OInstance>();
            return null;
        }

        public MH2OChunkAttribute? GetLiquidAttributes(int x, int y)
        {
            if (Liquid[x][y].LayerCount != 0)
            {
                if (Liquid[x][y].OffsetAttributes != 0)
                    return new BinaryReader(new MemoryStream(data, (int)Liquid[x][y].OffsetAttributes, data.Length - (int)Liquid[x][y].OffsetAttributes)).Read<MH2OChunkAttribute>();
                return new() { Fishable = 0xFFFFFFFFFFFFFFFF, Deep = 0xFFFFFFFFFFFFFFFF };
            }
            return null;
        }

        public ushort GetLiquidType(MH2OInstance h)
        {
            if (GetLiquidVertexFormat(h) == LiquidVertexFormatType.Depth)
                return 2;

            return h.LiquidType;
        }

        public float GetLiquidHeight(MH2OInstance h, int x, int y, int pos)
        {
            if (h.OffsetVertexData == 0)
                return 0.0f;
            if (GetLiquidVertexFormat(h) == LiquidVertexFormatType.Depth)
                return 0.0f;

            switch (GetLiquidVertexFormat(h))
            {
                case LiquidVertexFormatType.HeightDepth:
                case LiquidVertexFormatType.HeightTextureCoord:
                case LiquidVertexFormatType.HeightDepthTextureCoord:
                    return BitConverter.ToSingle(data, (int)(h.OffsetVertexData + pos * 4));
                case LiquidVertexFormatType.Depth:
                    return 0.0f;
                case LiquidVertexFormatType.Unk4:
                case LiquidVertexFormatType.Unk5:
                    return BitConverter.ToSingle(data, (int)(h.OffsetVertexData + 4 + (pos * 4) * 2));
                default:
                    return 0.0f;
            }
        }

        public int GetLiquidDepth(MH2OInstance h, int pos)
        {
            if (h.OffsetVertexData == 0)
                return -1;

            switch (GetLiquidVertexFormat(h))
            {
                case LiquidVertexFormatType.HeightDepth:
                    return (sbyte)data[h.OffsetVertexData + (h.GetWidth() + 1) * (h.GetHeight() + 1) * 4 + pos];
                case LiquidVertexFormatType.HeightTextureCoord:
                    return 0;
                case LiquidVertexFormatType.Depth:
                    return (sbyte)data[h.OffsetVertexData + pos];
                case LiquidVertexFormatType.HeightDepthTextureCoord:
                    return (sbyte)data[h.OffsetVertexData + (h.GetWidth() + 1) * (h.GetHeight() + 1) * 8 + pos];
                case LiquidVertexFormatType.Unk4:
                    return (sbyte)data[h.OffsetVertexData + pos * 8];
                case LiquidVertexFormatType.Unk5:
                    return 0;
                default:
                    break;
            }
            return 0;
        }

        ushort? GetLiquidTextureCoordMap(MH2OInstance h, int pos)
        {
            if (h.OffsetVertexData == 0)
                return null;

            switch (GetLiquidVertexFormat(h))
            {
                case LiquidVertexFormatType.HeightDepth:
                case LiquidVertexFormatType.Depth:
                case LiquidVertexFormatType.Unk4:
                    return null;
                case LiquidVertexFormatType.HeightTextureCoord:
                case LiquidVertexFormatType.HeightDepthTextureCoord:
                    return BitConverter.ToUInt16(data, (int)(h.OffsetVertexData + 4 * ((h.GetWidth() + 1) * (h.GetHeight() + 1) + pos)));
                case LiquidVertexFormatType.Unk5:
                    return BitConverter.ToUInt16(data, (int)(h.OffsetVertexData + 8 * ((h.GetWidth() + 1) * (h.GetHeight() + 1) + pos)));
                default:
                    break;
            }
            return null;
        }

        public ulong GetLiquidExistsBitmap(MH2OInstance h)
        {
            if (h.OffsetExistsBitmap != 0)
                return BitConverter.ToUInt64(data, (int)(h.OffsetExistsBitmap));
            else
                return 0xFFFFFFFFFFFFFFFFuL;
        }

        public static LiquidVertexFormatType GetLiquidVertexFormat(MH2OInstance liquidInstance)
        {
            if (liquidInstance.LiquidVertexFormat < (LiquidVertexFormatType)42)
                return liquidInstance.LiquidVertexFormat;

            if (liquidInstance.LiquidType == 2)
                return LiquidVertexFormatType.Depth;

            var liquidType = MapFile.LiquidTypeStorage.LookupByKey(liquidInstance.LiquidType);
            if (liquidType != null)
            {
                if (MapFile.LiquidMaterialStorage.ContainsKey(liquidType.MaterialID))
                    return (LiquidVertexFormatType)MapFile.LiquidMaterialStorage[liquidType.MaterialID].LVF;
            }

            return (LiquidVertexFormatType)(-1);
        }
    }

    public struct MH2OHeader
    {
        public uint OffsetInstances;
        public uint LayerCount;
        public uint OffsetAttributes;
    }

    public struct MH2OChunkAttribute
    {
        public ulong Fishable;
        public ulong Deep;
    }

    public struct MH2OInstance
    {
        public ushort LiquidType;
        public LiquidVertexFormatType LiquidVertexFormat;
        public float MinHeightLevel;
        public float MaxHeightLevel;
        public byte OffsetX;
        public byte OffsetY;
        public byte Width;
        public byte Height;
        public uint OffsetExistsBitmap;
        public uint OffsetVertexData;

        public byte GetOffsetX() { return (byte)(LiquidVertexFormat < (LiquidVertexFormatType)42 ? OffsetX : 0); }
        public byte GetOffsetY() { return (byte)(LiquidVertexFormat < (LiquidVertexFormatType)42 ? OffsetY : 0); }
        public byte GetWidth() { return (byte)(LiquidVertexFormat < (LiquidVertexFormatType)42 ? Width : 8); }
        public byte GetHeight() { return (byte)(LiquidVertexFormat < (LiquidVertexFormatType)42 ? Height : 8); }
    }

    public struct MH2OVertexData
    {
        public byte LiquidVertexFormat;
        public byte[] VertexData;
    }

    public class MFBO : IMapStruct
    {
        public void Read(byte[] data)
        {
            using BinaryReader reader = new(new MemoryStream(data));
            Max = new Plane();
            for (var i = 0; i < 9; ++i)
                Max.coords[i] = reader.ReadInt16();

            Min = new Plane();
            for (var i = 0; i < 9; ++i)
                Min.coords[i] = reader.ReadInt16();
        }

        public Plane Max;
        public Plane Min;

        public class Plane
        {
            public short[] coords = new short[9];
        }
    }

    public class FilenameChunk : IMapStruct
    {
        public void Read(byte[] data)
        {
            using BinaryReader reader = new(new MemoryStream(data));
            int size = data.Length;
            while (size > 0)
            {
                var filename = reader.ReadCString();
                Filenames.Add(filename.GetPlainName());
                size -= filename.Length + 1;
            }
        }

        public List<string> Filenames = new();
    }

    public class MDDF : IMapStruct
    {
        public void Read(byte[] data)
        {
            using BinaryReader reader = new(new MemoryStream(data));
            int size = data.Length / 36;

            DoodadDefs = new SMDoodadDef[size];
            for (int i = 0; i < size; ++i)
                DoodadDefs[i] = reader.Read<SMDoodadDef>();
        }

        public SMDoodadDef[] DoodadDefs;

        public struct SMDoodadDef
        {
            public uint Id;
            public uint UniqueId;
            public Vector3 Position;
            public Vector3 Rotation;
            public ushort Scale;
            public MDDFFlags Flags;
        }
    }

    public class MODF : IMapStruct
    {
        public void Read(byte[] data)
        {
            using BinaryReader reader = new(new MemoryStream(data));
            int size = data.Length / 64;

            MapObjDefs = new SMMapObjDef[size];
            for (int i = 0; i < size; ++i)
                MapObjDefs[i] = reader.Read<SMMapObjDef>();
        }

        public SMMapObjDef[] MapObjDefs;

        public struct SMMapObjDef
        {
            public uint Id;
            public uint UniqueId;
            public Vector3 Position;
            public Vector3 Rotation;
            public AxisAlignedBox Bounds;
            public MODFFlags Flags;
            public ushort DoodadSet;
            public ushort NameSet;
            public ushort Scale;
        }
    }
}