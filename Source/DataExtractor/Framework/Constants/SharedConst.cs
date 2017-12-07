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
        public const string VMAP_MAGIC = "VMAP_4.5";
        public const string RAW_VMAP_MAGIC = "VMAP045";

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

        DarkWater = 0x10,
        WmoWater = 0x20
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
    };
}
