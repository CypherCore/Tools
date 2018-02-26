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


namespace Framework.ClientReader
{
    public sealed class CinematicCameraRecord
    {
        public uint ID;
        public uint SoundID;
        public float OriginX;
        public float OriginY;
        public float OriginZ;
        public float OriginFacing;
        public uint ModelFileDataID;
    }

    public sealed class GameObjectDisplayInfoRecord
    {
        public uint Id;
        public uint FileDataID;
        public float[] GeoBoxMin = new float[3];
        public float[] GeoBoxMax = new float[3];
        public float OverrideLootEffectScale;
        public float OverrideNameScale;
        public ushort ObjectEffectPackageID;
    }

    public sealed class LiquidTypeRecord
    {
        public uint Id;
        public string Name;
        public string[] Texture = new string[6];
        public uint SpellID;
        public float MaxDarkenDepth;
        public float FogDarkenIntensity;
        public float AmbDarkenIntensity;
        public float DirDarkenIntensity;
        public float ParticleScale;
        public uint[] Color = new uint[2];
        public float[] Float = new float[18];
        public uint[] Int = new uint[4];
        public ushort Flags;
        public ushort LightID;
        public byte LiquidType;
        public byte ParticleMovement;
        public byte ParticleTexSlots;
        public byte MaterialID;
        public byte[] DepthTexCount = new byte[6];
        public ushort SoundID;
    }

    public sealed class MapRecord
    {
        public uint Id;
        public string Directory;
        public string MapName;
        public string MapDescription0;                               // Horde
        public string MapDescription1;                               // Alliance
        public string ShortDescription;
        public string LongDescription;
        public uint[] Flags = new uint[2];
        public float MinimapIconScale;
        public float CorpsePosX;                                        // entrance coordinates in ghost mode  (in most cases = normal entrance)
        public float CorpsePosY;
        public ushort AreaTableID;
        public ushort LoadingScreenID;
        public short CorpseMapID;                                              // map_id of entrance map in ghost mode (continent always and in most cases = normal entrance)
        public ushort TimeOfDayOverride;
        public short ParentMapID;
        public short CosmeticParentMapID;
        public ushort WindSettingsID;
        public byte InstanceType;
        public byte unk5;
        public byte ExpansionID;
        public byte MaxPlayers;
        public byte TimeOffset;
    }
}
