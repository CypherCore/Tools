using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataExtractor.ClientReader
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
        public uint SpellID;
        public float MaxDarkenDepth;
        public float FogDarkenIntensity;
        public float AmbDarkenIntensity;
        public float DirDarkenIntensity;
        public float ParticleScale;
        public string[] Texture = new string[6];
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
        public uint SoundID;
    }

    public sealed class MapRecord
    {
        public uint Id;

        public string Directory;
        public uint[] Flags = new uint[2];
        public float MinimapIconScale;
        public float CorpsePosX;                                        // entrance coordinates in ghost mode  (in most cases = normal entrance)
        public float CorpsePosY;
        public string MapName;
        public string MapDescription0;                               // Horde
        public string MapDescription1;                               // Alliance
        public string ShortDescription;
        public string LongDescription;
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
