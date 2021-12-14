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
using DataExtractor.CASCLib;

namespace DataExtractor.Framework.Constants
{
    class SharedConst
    {
        public const uint MAP_MAGIC = 0x5350414D; //"MAPS";
        public const uint MAP_VERSION_MAGIC = 0x392E3176; // v1.9

        public const uint MAP_AREA_MAGIC = 0x41455241; //"AREA";
        public const uint MAP_HEIGHT_MAGIC = 0x5447484D; //"MHGT";
        public const uint MAP_LIQUID_MAGIC = 0x51494C4D; //"MLIQ";

        public const string VMAP_MAGIC = "VMAP_4.9";
        public const string RAW_VMAP_MAGIC = "VMAP049";

        public const uint MMAP_MAGIC = 0x4D4D4150;   // 'MMAP'
        public const uint MMAP_VERSION = 9;

        public const float LIQUID_TILE_SIZE = 533.333f / 128.0f;

        public static LocaleFlags[] WowLocaleToCascLocaleFlags =
        {
            LocaleFlags.enUS | LocaleFlags.enGB,
            LocaleFlags.koKR,
            LocaleFlags.frFR,
            LocaleFlags.deDE,
            LocaleFlags.zhCN,
            LocaleFlags.zhTW,
            LocaleFlags.esES,
            LocaleFlags.esMX,
            LocaleFlags.ruRU,
            0,
            LocaleFlags.ptBR | LocaleFlags.ptPT,
            LocaleFlags.itIT
        };

        public const int DT_NAVMESH_VERSION = 7;
        public const int DT_VERTS_PER_POLYGON = 6;
        public const int RC_WALKABLE_AREA = 63;
        public const int DT_POLY_BITS = 31;

        public const int V9_SIZE = 129;
        public const int V9_SIZE_SQ = V9_SIZE * V9_SIZE;
        public const int V8_SIZE = 128;
        public const int V8_SIZE_SQ = V8_SIZE * V8_SIZE;
        public const float GRID_SIZE = 533.3333f;
        public const float GRID_PART_SIZE = GRID_SIZE / V8_SIZE;

        // see contrib/extractor/system.cpp, CONF_use_minHeight
        public const float INVALID_MAP_LIQ_HEIGHT = -2000.0f;
        public const float INVALID_MAP_LIQ_HEIGHT_MAX = 5000.0f;

        public static int ADT_CELLS_PER_GRID = 16;
        public static int ADT_CELL_SIZE = 8;
        public static int ADT_GRID_SIZE = (ADT_CELLS_PER_GRID * ADT_CELL_SIZE);

        public static int MCVT_HEIGHT_MAP_SIZE = (ADT_CELL_SIZE + 1) * (ADT_CELL_SIZE + 1) + ADT_CELL_SIZE * ADT_CELL_SIZE;
    }

    [Flags]
    public enum AreaHeaderFlags : ushort
    {
        None = 0x00,
        NoArea = 0x01
    }

    [Flags]
    public enum HeightHeaderFlags : byte
    {
        None = 0x00,
        NoHeight = 0x01,
        AsInt16 = 0x02,
        AsInt8 = 0x04,
        HasFlightBounds = 0x08
    }

    [Flags]
    public enum LiquidHeaderFlags : byte
    {
        None = 0x00,
        NoType = 0x01,
        NoHeight = 0x02
    }

    [Flags]
    public enum LiquidHeaderTypeFlags : byte
    {
        NoWater = 0x00,
        Water = 0x01,
        Ocean = 0x02,
        Magma = 0x04,
        Slime = 0x08,

        DarkWater = 0x10,

        AllLiquids = Water | Ocean | Magma | Slime
    }

    public enum LiquidType
    {
        Water = 0,
        Ocean = 1,
        Magma = 2,
        Slime = 3
    }

    [Flags]
    public enum MopyFlags
    {
        Unk01 = 0x01,
        NoCamCollide = 0x02,
        Detail = 0x04,
        Collision = 0x08,
        Hint = 0x10,
        Render = 0x20,
        WallSurface = 0x40, // Guessed
        CollideHit = 0x80
    }

    [Flags]
    public enum MCNKFlags : uint
    {
        HasMCSH = 0x00001,
        Impass = 0x00002,
        LiquidRiver = 0x00004,
        LiquidOcean = 0x00008,
        LiquidMagma = 0x00010,
        LiquidSlime = 0x00020,
        HasMCCV = 0x00040,
        DoNotFixAlphaMap = 0x08000,
        HighResHoles = 0x10000,
    }

    public enum LiquidVertexFormatType : short
    {
        HeightDepth = 0,
        HeightTextureCoord = 1,
        Depth = 2,
        HeightDepthTextureCoord = 3,
        Unk4 = 4,
        Unk5 = 5
    }

    public struct ModelFlags
    {
        public const uint M2 = 1;
        public const uint HasBound = 1 << 1;
        public const uint ParentSpawn = 1 << 2;
    }

    public enum Spot
    {
        Top = 1,
        Right = 2,
        Left = 3,
        Bottom = 4,
        Entire = 5
    }

    public enum Grid
    {
        V8,
        V9
    }

    public enum NavArea
    {
        Empty = 0,
        // areas 1-60 will be used for destructible areas (currently skipped in vmaps, WMO with flag 1)
        // ground is the highest value to make recast choose ground over water when merging surfaces very close to each other (shallow water would be walkable) 
        MagmaSlime = 61, // don't need to differentiate between them
        Water = 62,
        Ground = 63,
    }

    public enum NavTerrainFlag
    {
        Empty = 0x00,
        Ground = 1 << (63 - NavArea.Ground),
        Water = 1 << (63 - NavArea.Water),
        MagmaSlime = 1 << (63 - NavArea.MagmaSlime)
    }

    [Flags]
    public enum MODFFlags : ushort
    {
        Destroyable = 0x01,
        UseLod = 0x02,
        UnkHasScale = 0x04,
        EntryIsFileID = 0x08,
        seSetsFromMWDS = 0x80,
    }

    [Flags]
    public enum MDDFFlags : ushort
    {
        Biodome = 0x001,
        Shrubbery = 0x002,
        Unk0x4 = 0x004,
        Unk0x8 = 0x008,
        LiquidKnown = 0x020,
        EntryIsFileID = 0x040,
        Unk0x100 = 0x100,
    }
}
