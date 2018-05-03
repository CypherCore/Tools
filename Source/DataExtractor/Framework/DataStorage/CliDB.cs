using Framework.CASC.Handlers;
using Framework.ClientReader;
using System;
using System.Collections.Generic;
using System.IO;

namespace DataExtractor
{
    public class CliDB
    {
        public static bool LoadFiles(CASCHandler handler)
        {
            //CinematicCamera
            using (MemoryStream stream = handler.ReadFile("DBFilesClient\\CinematicCamera.db2"))
            {
                if (stream == null)
                {
                    Console.WriteLine("Unable to open file DBFilesClient\\CinematicCamera.db2s in the archive");
                    return false;
                }

                Dictionary<uint, CinematicCameraRecord> storage = DBReader.Read<CinematicCameraRecord>(stream);
                if (storage == null)
                {
                    Console.WriteLine("Invalid CinematicCamera.db2 file format. Camera extract aborted.\n");
                    return false;
                }

                // get camera file list from DB2
                foreach (var record in storage.Values)
                    CameraFileNames.Add(record.ModelFileDataID);

                storage = null;
            }

            using (MemoryStream stream = Program.cascHandler.ReadFile("DBFilesClient\\GameObjectDisplayInfo.db2"))
            {
                if (stream == null)
                {
                    Console.WriteLine("Unable to open file DBFilesClient\\GameObjectDisplayInfo.db2 in the archive\n");
                    return false;
                }
                GameObjectDisplayInfoStorage = DBReader.Read<GameObjectDisplayInfoRecord>(stream);
                if (GameObjectDisplayInfoStorage == null)
                {
                    Console.WriteLine("Fatal error: Invalid GameObjectDisplayInfo.db2 file format!\n");
                    return false;
                }
            }

            //Map
            using (MemoryStream stream = handler.ReadFile("DBFilesClient\\Map.db2"))
            {
                if (stream == null)
                {
                    Console.WriteLine("Unable to open file DBFilesClient\\Map.db2 in the archive\n");
                    return false;
                }
                MapStorage = DBReader.Read<MapRecord>(stream);
                if (MapStorage == null)
                {
                    Console.WriteLine("Fatal error: Invalid Map.db2 file format!\n");
                    return false;
                }
            }

            //LiquidMaterial
            using (MemoryStream stream = handler.ReadFile("DBFilesClient\\LiquidMaterial.db2"))
            {
                if (stream == null)
                {
                    Console.WriteLine("Unable to open file DBFilesClient\\LiquidMaterial.db2 in the archive\n");
                    return false;
                }

                var storage = DBReader.Read<LiquidMaterialRecord>(stream);
                if (storage == null)
                {
                    Console.WriteLine("Fatal error: Invalid LiquidMaterial.db2 file format!\n");
                    return false;
                }

                foreach (var record in storage.Values)
                    LiquidMaterials[record.Id] = record.LVF;

                storage = null;
            }

            //LiquidObject
            using (MemoryStream stream = handler.ReadFile("DBFilesClient\\LiquidObject.db2"))
            {
                if (stream == null)
                {
                    Console.WriteLine("Unable to open file DBFilesClient\\LiquidObject.db2 in the archive\n");
                    return false;
                }

                var storage = DBReader.Read<LiquidObjectRecord>(stream);
                if (storage == null)
                {
                    Console.WriteLine("Fatal error: Invalid LiquidObject.db2 file format!\n");
                    return false;
                }

                foreach (var record in storage.Values)
                    LiquidObjects[record.Id] = record.LiquidTypeID;

                storage = null;
            }

            //LiquidType
            using (MemoryStream stream = handler.ReadFile("DBFilesClient\\LiquidType.db2"))
            {
                if (stream == null)
                {
                    Console.WriteLine("Unable to open file DBFilesClient\\LiquidType.db2 in the archive\n");
                    return false;
                }

                var storage = DBReader.Read<LiquidTypeRecord>(stream);
                if (storage == null)
                {
                    Console.WriteLine("Fatal error: Invalid LiquidType.db2 file format!\n");
                    return false;
                }

                foreach (var record in storage.Values)
                {
                    LiquidTypeEntry liquidType = new LiquidTypeEntry();
                    liquidType.SoundBank = record.SoundBank;
                    liquidType.MaterialID = record.MaterialID;
                    LiquidTypes[record.Id] = liquidType;
                }

                storage = null;
            }

            return true;
        }

        public static List<uint> CameraFileNames = new List<uint>();
        public static Dictionary<uint, GameObjectDisplayInfoRecord> GameObjectDisplayInfoStorage = new Dictionary<uint, GameObjectDisplayInfoRecord>();
        public static Dictionary<uint, MapRecord> MapStorage = new Dictionary<uint, MapRecord>();
        public static Dictionary<uint, sbyte> LiquidMaterials = new Dictionary<uint, sbyte>();
        public static Dictionary<uint, short> LiquidObjects = new Dictionary<uint, short>();
        public static Dictionary<uint, LiquidTypeEntry> LiquidTypes = new Dictionary<uint, LiquidTypeEntry>();
    }

    public class LiquidTypeEntry
    {
        public byte SoundBank;
        public byte MaterialID;
    }
}
