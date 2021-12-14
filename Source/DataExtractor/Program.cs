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

using DataExtractor.CASCLib;
using DataExtractor.Framework.ClientReader;
using DataExtractor.Framework.Constants;
using DataExtractor.Map;
using DataExtractor.Mmap;
using DataExtractor.Vmap;
using DataExtractor.Vmap.Collision;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace DataExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@" ____                    __                      ");
            Console.WriteLine(@"/\  _`\                 /\ \                     ");
            Console.WriteLine(@"\ \ \/\_\  __  __  _____\ \ \___      __   _ __  ");
            Console.WriteLine(@" \ \ \/_/_/\ \/\ \/\ '__`\ \  _ `\  /'__`\/\`'__\");
            Console.WriteLine(@"  \ \ \L\ \ \ \_\ \ \ \L\ \ \ \ \ \/\  __/\ \ \/ ");
            Console.WriteLine(@"   \ \____/\/`____ \ \ ,__/\ \_\ \_\ \____\\ \_\ ");
            Console.WriteLine(@"    \/___/  `/___/> \ \ \/  \/_/\/_/\/____/ \/_/ ");
            Console.WriteLine(@"               /\___/\ \_\                       ");
            Console.WriteLine(@"               \/__/  \/_/    Core Data Extractor");
            Console.WriteLine("\r");

            BaseDirectory = Environment.CurrentDirectory;
            BuildingsDirectory = $"{BaseDirectory}/Buildings/";

            if (args.Length > 0)
                BaseDirectory = Path.GetDirectoryName(args[0]);

            Console.ForegroundColor = ConsoleColor.Green;

            uint localeMask = 0;

            CASCConfig config = CASCConfig.LoadLocalStorageConfig(BaseDirectory);
            string[] tagLines = config.BuildInfo[0]["Tags"].Split(' ');
            foreach (var line in tagLines)
            {
                if (!Enum.TryParse(typeof(LocaleFlags), line, out object locale))
                    continue;

                localeMask |= Convert.ToUInt32(locale);
            }

            installedLocalesMask = (LocaleFlags)localeMask;
            firstInstalledLocale = LocaleFlags.None;

            for (Locale i = 0; i < Locale.Total; ++i)
            {
                if (i == Locale.None)
                    continue;

                if (!Convert.ToBoolean(installedLocalesMask & SharedConst.WowLocaleToCascLocaleFlags[(int)i]))
                    continue;

                firstInstalledLocale = SharedConst.WowLocaleToCascLocaleFlags[(int)i];
                break;
            }

            if (firstInstalledLocale < LocaleFlags.None)
            {
                Console.WriteLine("No locales detected");
                Shutdown();
            }

            Console.WriteLine("Initializing CASC library...");
            CascHandler = CASCHandler.OpenStorage(config);
            CascHandler.Root.SetFlags(firstInstalledLocale);
            _build = CascHandler.Config.GetBuildNumber();
            Console.WriteLine("Done.");

            while (true)
            {
                PrintInstructions();
                string[] lines = Console.ReadLine().Split(' ');
                for (var i = 0; i < lines.Length; ++i)
                {
                    switch (lines[i])
                    {
                        case "maps":
                            ExtractMaps();
                            break;
                        case "vmaps":
                            ExtractVMaps();
                            break;
                        case "mmaps":
                            ExtractMMaps(lines);
                            i = lines.Length;
                            break;
                        case "all":
                            ExtractMaps();
                            ExtractVMaps();
                            //ExtractMMaps(lines);
                            break;
                        case "exit":
                            return;
                    }
                }
            }
        }

        static void PrintInstructions()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Select your task.");
            Console.WriteLine("maps                         Extract DB2 and maps");
            Console.WriteLine("vmaps                        Extract vmaps(requires maps)");
            Console.WriteLine("mmaps [-id #][-debug]        Extract mmaps(requires maps, vmaps. may take hours)");
            Console.WriteLine("all                          Extract all(may take hours)");
            Console.WriteLine("exit                         EXIT");
            //Add args to the new of the command   so like Pick a task  ->  mmaps -debug  and many more if we need them
            Console.WriteLine();
        }

        static void ExtractDbcFiles()
        {
            string path = $"{BaseDirectory}/dbc";
            CreateDirectory(path);

            Console.WriteLine("Extracting db2 files...");
            uint count = 0;
            for (Locale locale = 0; locale < Locale.Total; ++locale)
            {
                if (locale == Locale.None)
                    continue;

                if (!Convert.ToBoolean(installedLocalesMask & SharedConst.WowLocaleToCascLocaleFlags[(int)locale]))
                    continue;

                string currentPath = $"{path}/{locale}";
                CreateDirectory(currentPath);

                CascHandler.Root.SetFlags(SharedConst.WowLocaleToCascLocaleFlags[(int)locale]);
                foreach (var (fileDataId, fileName) in FileList.DBFilesClientList)
                {
                    var dbcStream = CascHandler.OpenFile(fileDataId);
                    if (dbcStream == null)
                    {
                        Console.WriteLine($"Unable to open file {fileName} in the archive for locale {locale}");
                        continue;
                    }

                    // Unused DB2 file, 0 records
                    var reader = new BinaryReader(dbcStream);
                    reader.ReadUInt32();
                    var recordCount = reader.ReadUInt32();
                    if (recordCount == 0)
                        continue;

                    reader.BaseStream.Position = 0;
                    dbcStream.Position = 0;

                    FileWriter.WriteFile(dbcStream, currentPath + $"/{ fileName.Replace(@"\\", "").Replace(@"DBFilesClient\", "")}");
                    count++;
                }
            }

            Console.WriteLine($"Extracted {count} db2 files.");

            CascHandler.Root.SetFlags(firstInstalledLocale);
        }

        static void ExtractCameraFiles()
        {
            Dictionary<uint, CinematicCameraRecord> storage = DBReader.Read<CinematicCameraRecord>(1294214);
            if (storage == null)
            {
                Console.WriteLine("Invalid CinematicCamera.db2 file format. Camera extract aborted.\n");
                return;
            }

            Console.WriteLine($"Extracting ({storage.Values.Count} CinematicCameras!)\n");

            string path = $"{BaseDirectory}/cameras";
            CreateDirectory(path);

            // extract M2s
            uint count = 0;
            foreach (var cameraRecord in storage.Values)
            {
                var cameraStream = CascHandler.OpenFile((int)cameraRecord.FileDataID);
                if (cameraStream != null)
                {
                    string file = path + $"/FILE{cameraRecord.FileDataID:X8}.xxx";
                    if (!File.Exists(file))
                    {
                        FileWriter.WriteFile(cameraStream, file);
                        ++count;
                    }
                }
                else
                    Console.WriteLine($"Unable to open file {$"FILE{cameraRecord.FileDataID:X8}.xxx"} in the archive: \n");
            }

            Console.WriteLine($"Extracted {count} Camera files.");
        }

        static void ExtractGameTablesFiles()
        {
            Console.WriteLine("Extracting game tables...\n");

            string path = $"{BaseDirectory}/gt";
            CreateDirectory(path);

            uint count = 0;
            foreach (var pair in FileList.GameTables)
            {
                var dbcFile = CascHandler.OpenFile(pair.Key);
                if (dbcFile == null)
                {
                    Console.WriteLine($"Unable to open file {pair.Value} in the archive\n");
                    continue;
                }

                string file = $"{path}/{pair.Value.Replace("GameTables\\", "")}";
                if (!File.Exists(file))
                {
                    FileWriter.WriteFile(dbcFile, file);
                    ++count;
                }
            }

            Console.WriteLine($"Extracted {count} files\n\n");
        }

        static void ExtractMaps()
        {
            ExtractDbcFiles();
            ExtractCameraFiles();
            ExtractGameTablesFiles();

            Console.WriteLine("Extracting maps...");

            string path = $"{BaseDirectory}/maps";
            CreateDirectory(path);

            Console.WriteLine("Convert map files");
            int count = 1;

            Console.WriteLine("Loading DB2 files");
            var mapStorage = DBReader.Read<MapRecord>(1349477);
            if (mapStorage == null)
            {
                Console.WriteLine("Fatal error: Invalid Map.db2 file format!");
                return;
            }

            foreach (var record in mapStorage.Values)
            {
                if (record.WdtFileDataID == 0)
                    continue;

                Console.Write($"Extract {record.Directory} ({count++}/{mapStorage.Count})                  \n");
                // Loadup map grid data
                ChunkedFile wdt = new();

                BitArray existingTiles = new(64*64);
                if (wdt.LoadFile(CascHandler, (uint)record.WdtFileDataID, $"WDT for map {record.Id}"))
                {
                    MPHD mphd = wdt.GetChunk("MPHD").As<MPHD>();
                    MAIN main = wdt.GetChunk("MAIN").As<MAIN>();
                    for (int y = 0; y < 64; ++y)
                    {
                        for (int x = 0; x < 64; ++x)
                        {
                            if ((main.MapAreaInfo[y][x].Flag & 0x1) == 0)
                                continue;

                            string outputFileName = $"{path}/{record.Id:D4}_{y:D2}_{x:D2}.map";
                            bool ignoreDeepWater = MapFile.IsDeepWaterIgnored(record.Id, y, x);

                            if (mphd != null && (mphd.Flags & 0x200) != 0)
                            {
                                MAID maid = wdt.GetChunk("MAID").As<MAID>();
                                existingTiles[y * 64 + x] = MapFile.ConvertADT(CascHandler, maid.MapFileDataIDs[y][x].RootADT, record.MapName, outputFileName, y, x, ignoreDeepWater);
                            }
                            else
                            {
                                string storagePath = $"World\\Maps\\{record.Directory}\\{record.Directory}_{x}_{y}.adt";
                                existingTiles[y * 64 + x] = MapFile.ConvertADT(CascHandler, storagePath, record.MapName, outputFileName, y, x, ignoreDeepWater);
                            }
                        }
                        // draw progress bar
                        Console.Write($"Processing........................{(100 * (y + 1)) / 64}%\r");
                    }
                }

                using BinaryWriter binaryWriter = new(File.Open($"{path}/{record.Id:D4}.tilelist", FileMode.Create, FileAccess.Write));
                binaryWriter.Write(SharedConst.MAP_MAGIC);
                binaryWriter.Write(SharedConst.MAP_VERSION_MAGIC);
                binaryWriter.Write(_build);
                binaryWriter.WriteString(existingTiles.ToBinaryString());
            }
            Console.WriteLine("\n");
        }

        static void ExtractVMaps()
        {
            CreateDirectory(BuildingsDirectory);
            File.Delete(BuildingsDirectory + "dir_bin");

            // Extract models, listed in GameObjectDisplayInfo.dbc
            VmapFile.ExtractGameobjectModels();

            string dirname = BuildingsDirectory + "dir_bin";
            DirBinWriter = new(File.Open(dirname, FileMode.Append, FileAccess.Write));
            VmapFile.ParsMapFiles(CascHandler);
            DirBinWriter.Close();

            Console.WriteLine("Extracting Done!");

            Console.WriteLine("Converting Vmap files...");
            CreateDirectory($"{BaseDirectory}/vmaps");

            TileAssembler ta = new(BuildingsDirectory, $"{BaseDirectory}/vmaps");
            if (!ta.ConvertWorld2())
                return;

            Console.WriteLine("Converting Done!");
        }

        static void ExtractMMaps(string[] args)
        {
            if (!Directory.Exists("maps") || Directory.GetFiles("maps").Length == 0)
            {
                Console.WriteLine("'maps' directory is empty or does not exist");
                return;
            }

            if (!Directory.Exists("vmaps") || Directory.GetFiles("vmaps").Length == 0)
            {
                Console.WriteLine("'vmaps' directory is empty or does not exist");
                return;
            }

            CreateDirectory("mmaps");

            //handle args
            bool debugMaps = false;
            int mapId = -1;
            for (var i = 1; i < args.Length; ++i)
            {
                switch(args[i].ToLower())
                {
                    case "-debug":
                        CreateDirectory("mmaps/meshes");
                        debugMaps = true;
                        break;
                    case "-id":
                        mapId = int.Parse(args[i + 1]);
                        i++;
                        break;
                }
            }

            Console.WriteLine("Extracting MMap files...");
            var vm = new Framework.Collision.VMapManager2();

            var mapStorage = DBReader.Read<MapRecord>(1349477);
            if (mapStorage == null)
            {
                Console.WriteLine("Fatal error: Invalid Map.db2 file format!\n");
                return;
            }

            MultiMap<uint, uint> mapData = new();
            foreach (var record in mapStorage.Values)
            {
                if (record.ParentMapID != -1)
                    mapData.Add((uint)record.ParentMapID, record.Id);
            }

            vm.Initialize(mapData);

            MapBuilder builder = new(vm, debugMaps);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            if (mapId != -1)
                builder.buildMap((uint)mapId);
            else
                builder.buildAllMaps();

            Console.WriteLine($"Finished. MMAPS were built in {watch.ElapsedMilliseconds} ms!");
        }

        public static void CreateDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);

            while (Directory.Exists(path))
                Thread.Sleep(10);

            Directory.CreateDirectory(path);
        }

        public static void Shutdown()
        {
            Console.WriteLine("Halting process...");
            Thread.Sleep(10000);
            Environment.Exit(-1);
        }

        public static void Profile(string description, int iterations, Action func)
        {
            //Run at highest priority to minimize fluctuations caused by other processes/threads
            System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.High;
            System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Highest;

            // warm up 
            func();

            var watch = new System.Diagnostics.Stopwatch();

            // clean up
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            watch.Start();
            for (int i = 0; i < iterations; i++)
            {
                func();
            }
            watch.Stop();
            Console.Write(description);
            Console.WriteLine(" Time Elapsed {0} ms", watch.Elapsed.TotalMilliseconds);

            // clean up
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        public static CASCHandler CascHandler { get; set; }

        static uint _build;

        static LocaleFlags installedLocalesMask { get; set; }
        static LocaleFlags firstInstalledLocale { get; set; }

        public static string BaseDirectory { get; set; }
        public static string BuildingsDirectory { get; set; }

        public static BinaryWriter DirBinWriter { get; set; }
    }
}
