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
using Framework.CASC.Constants;

namespace Framework.Constants
{
    class SharedConst
    {
        public const string VMAP_MAGIC = "VMAP_4.7";
        public const string RAW_VMAP_MAGIC = "VMAP047";

        public const float LIQUID_TILE_SIZE = 533.333f / 128.0f;

        public static LocaleMask[] WowLocaleToCascLocaleFlags =
        {
            LocaleMask.enUS | LocaleMask.enGB,
            LocaleMask.koKR,
            LocaleMask.frFR,
            LocaleMask.deDE,
            LocaleMask.zhCN,
            LocaleMask.zhTW,
            LocaleMask.esES,
            LocaleMask.esMX,
            LocaleMask.ruRU,
            0,
            LocaleMask.ptBR | LocaleMask.ptPT,
            LocaleMask.itIT
        };

        public const uint MMAP_MAGIC = 0x4d4d4150;   // 'MMAP'
        public const uint MMAP_VERSION = 8;
        public const uint MAP_VERSION_MAGIC = 0x382E3176;        

        public const int V9_SIZE = 129;
        public const int V9_SIZE_SQ = V9_SIZE * V9_SIZE;
        public const int V8_SIZE = 128;
        public const int V8_SIZE_SQ = V8_SIZE * V8_SIZE;
        public const float GRID_SIZE = 533.3333f;
        public const float GRID_PART_SIZE = GRID_SIZE / V8_SIZE;

        // see contrib/extractor/system.cpp, CONF_use_minHeight
        public const float INVALID_MAP_LIQ_HEIGHT = -2000.0f;
        public const float INVALID_MAP_LIQ_HEIGHT_MAX = 5000.0f;
    }

    public enum LiquidType
    {
        Water = 0,
        Ocean = 1,
        Magma = 2,
        Slime = 3
    }

    [Flags]
    public enum LiquidTypeMask
    {
        NoWater = 0x00,
        Water = 0x01,
        Ocean = 0x02,
        Magma = 0x04,
        Slime = 0x08,

        DarkWater = 0x10
    }

    public enum MapHeightFlags
    {
        NoHeight = 0x0001,
        AsInt16 = 0x0002,
        AsInt8 = 0x0004,
        HasFlightBounds = 0x0008
    }

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

    public struct ModelFlags
    {
        public const uint M2 = 1;
        public const uint WorldSpawn = 1 << 1;
        public const uint HasBound = 1 << 2;
        public const uint ParentSpawn = 1 << 3;
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

    public enum NavTerrain
    {
        Empty = 0x00,
        Ground = 0x01,
        Magma = 0x02,
        Slime = 0x04,
        Water = 0x08,
        Unused1 = 0x10,
        Unused2 = 0x20,
        Unused3 = 0x40,
        Unused4 = 0x80
        // we only have 8 bits
    }
}
