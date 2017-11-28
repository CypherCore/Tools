using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CASC.Constants;

namespace DataExtractor
{
    class SharedConst
    {
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
}
