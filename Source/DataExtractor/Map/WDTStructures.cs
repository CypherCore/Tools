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
using System.IO;

namespace DataExtractor.Map
{
    public class MPHD : IMapStruct
    {
        public void Read(byte[] data)
        {
            using BinaryReader reader = new(new MemoryStream(data));
            Flags = reader.ReadUInt32();
            LgtFileDataID = reader.ReadUInt32();
            OccFileDataID = reader.ReadUInt32();
            FogsFileDataID = reader.ReadUInt32();
            MpvFileDataID = reader.ReadUInt32();
            TexFileDataID = reader.ReadUInt32();
            WdlFileDataID = reader.ReadUInt32();
            Pd4FileDataID = reader.ReadUInt32();
        }

        public uint Flags { get; set; }
        public uint LgtFileDataID { get; set; }
        public uint OccFileDataID { get; set; }
        public uint FogsFileDataID { get; set; }
        public uint MpvFileDataID { get; set; }
        public uint TexFileDataID { get; set; }
        public uint WdlFileDataID { get; set; }
        public uint Pd4FileDataID { get; set; }
    }

    public class MAIN : IMapStruct
    {
        public void Read(byte[] data)
        {
            using BinaryReader reader = new(new MemoryStream(data));
            for (var x = 0; x < 64; ++x)
            {
                MapAreaInfo[x] = new SMAearInfo[64];

                for (var y = 0; y < 64; ++y)
                    MapAreaInfo[x][y] = reader.Read<SMAearInfo>();
            }
        }

        public SMAearInfo[][] MapAreaInfo = new SMAearInfo[64][];

        public struct SMAearInfo
        {
            public uint Flag;
            public uint AsyncID;
        }
    }

    public class MAID : IMapStruct
    {
        public void Read(byte[] data)
        {
            using BinaryReader reader = new(new MemoryStream(data));
            for (var x = 0; x < 64; ++x)
            {
                MapFileDataIDs[x] = new MapFileData[64];
                for (var y = 0; y < 64; ++y)
                    MapFileDataIDs[x][y] = reader.Read<MapFileData>();
            }
        }

        public MapFileData[][] MapFileDataIDs = new MapFileData[64][];

        public struct MapFileData
        {
            public uint RootADT;           // FileDataID of mapname_xx_yy.adt
            public uint Obj0ADT;           // FileDataID of mapname_xx_yy_obj0.adt
            public uint Obj1ADT;           // FileDataID of mapname_xx_yy_obj1.adt
            public uint Tex0ADT;           // FileDataID of mapname_xx_yy_tex0.adt
            public uint LodADT;            // FileDataID of mapname_xx_yy_lod.adt
            public uint MapTexture;        // FileDataID of mapname_xx_yy.blp
            public uint MapTextureN;       // FileDataID of mapname_xx_yy_n.blp
            public uint MinimapTexture;    // FileDataID of mapxx_yy.blp
        }
    }
}
