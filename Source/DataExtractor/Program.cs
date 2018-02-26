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

using Framework.CASC.Constants;
using Framework.CASC.Handlers;
using Framework.ClientReader;
using Framework.Constants;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DataExtractor.Vmap.Collision;
using DataExtractor.Vmap;
using Framework.CASC;
using DataExtractor.Mmap;
using System.Linq;

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

            baseDirectory = Environment.CurrentDirectory;

            if (args.Length > 0)
                baseDirectory = Path.GetDirectoryName(args[0]);

            PrintInstructions();

            string answer = Console.ReadLine();
            if (answer == "5")
                Environment.Exit(0);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Initializing CASC library...");
            cascHandler = new CASCHandler(baseDirectory);
            Console.WriteLine("Done.");

            List<Locale> locales = new List<Locale>();
            var buildInfoLocales = Regex.Matches(cascHandler.buildInfo["Tags"], " ([A-Za-z]{4}) speech");
            foreach (Match m in buildInfoLocales)
            {
                var localFlag = (Locale)Enum.Parse(typeof(Locale), m.Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0]);

                if (!locales.Contains(localFlag))
                    locales.Add(localFlag);
            }

            LocaleMask installedLocalesMask = (LocaleMask)cascHandler.GetInstalledLocalesMask();
            LocaleMask firstInstalledLocale = LocaleMask.None;

            for (Locale i = 0; i < Locale.Total; ++i)
            {
                if (i == Locale.None)
                    continue;

                if (!Convert.ToBoolean(installedLocalesMask & SharedConst.WowLocaleToCascLocaleFlags[(int)i]))
                    continue;

                firstInstalledLocale = SharedConst.WowLocaleToCascLocaleFlags[(int)i];
                break;
            }

            if (firstInstalledLocale < LocaleMask.None)
            {
                Console.WriteLine("No locales detected");
                return;
            }

            uint build = cascHandler.GetBuildNumber();
            if (build == 0)
            {
                Console.WriteLine("No build detected");
                return;
            }

            Console.WriteLine($"Detected client build: {build}");
            Console.WriteLine($"Detected client locale: {firstInstalledLocale}");

            do
            {
                cascHandler.SetLocale(firstInstalledLocale);
                switch (answer)
                {
                    case "1":
                        ExtractDbcFiles(installedLocalesMask);
                        ExtractMaps(build);
                        break;
                    case "2":
                        ExtractVMaps();
                        break;
                    case "3":
                        ExtractMMaps();
                        break;
                    case "4":
                    default:
                        ExtractDbcFiles(installedLocalesMask);
                        ExtractMaps(build);
                        ExtractVMaps();
                        //ExtractMMaps();
                        break;
                }

                PrintInstructions();
            } while ((answer = Console.ReadLine()) != "5");
        }

        static void PrintInstructions()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Select your task.");
            Console.WriteLine("1 - Extract DB2 and maps");
            Console.WriteLine("2 - Extract vmaps(needs maps to be extracted before you run this)");
            //Console.WriteLine("3 - Extract mmaps(needs vmaps to be extracted before you run this, may take hours)");
            Console.WriteLine("4 - Extract all(may take hours)");
            Console.WriteLine("5 - EXIT");
            Console.WriteLine();
        }

        static void ExtractDbcFiles(LocaleMask localeMask)
        {
            string path = $"{baseDirectory}/dbc";
            CreateDirectory(path);

            Console.WriteLine("Extracting db2 files...");
            uint count = 0;
            for (Locale locale = 0; locale < Locale.Total; ++locale)
            {
                if (locale == Locale.None)
                    continue;

                if (!Convert.ToBoolean(localeMask & SharedConst.WowLocaleToCascLocaleFlags[(int)locale]))
                    continue;

                string currentPath = $"{path}/{locale}";
                CreateDirectory(currentPath);

                cascHandler.SetLocale(SharedConst.WowLocaleToCascLocaleFlags[(int)locale]);
                foreach (var fileName in FileList.DBFilesClientList)
                {
                    var dbcStream = cascHandler.ReadFile(fileName);
                    if (dbcStream == null)
                    {
                        Console.WriteLine($"Unable to open file {fileName} in the archive for locale {locale}\n");
                        continue;
                    }

                    FileWriter.WriteFile(dbcStream, currentPath + $"/{ fileName.Replace(@"\\", "").Replace(@"DBFilesClient\", "")}");
                    count++;
                }
            }

            Console.WriteLine($"Extracted {count} db2 files.");

            ExtractCameraFiles();
            ExtractGameTablesFiles();
        }

        static void ExtractCameraFiles()
        {
            Console.WriteLine("Extracting Camera files...");
            var stream = cascHandler.ReadFile("DBFilesClient\\CinematicCamera.db2");
            if (stream == null)
            {
                Console.WriteLine("Unable to open file DBFilesClient\\CinematicCamera.db2s in the archive");
                return;
            }

            Dictionary<uint, CinematicCameraRecord> cinematicCameraStorage = DBReader.Read<CinematicCameraRecord>(stream);
            if (cinematicCameraStorage == null)
            {
                Console.WriteLine("Invalid CinematicCamera.db2 file format. Camera extract aborted.\n");
                return;
            }

            List<uint> CameraFileNames = new List<uint>();
            // get camera file list from DB2
            foreach (var record in cinematicCameraStorage.Values)
                CameraFileNames.Add(record.ModelFileDataID);

            Console.WriteLine($"Done! ({CameraFileNames.Count} CinematicCameras loaded)\n");

            string path = $"{baseDirectory}/cameras";
            CreateDirectory(path);

            // extract M2s
            uint count = 0;
            foreach (int cameraFileId in CameraFileNames)
            {
                var cameraStream = cascHandler.ReadFile(cameraFileId);
                if (cameraStream != null)
                {
                    string file = path + $"/File{cameraFileId:X8}.xxx";
                    if (!File.Exists(file))
                    {
                        FileWriter.WriteFile(cameraStream, file);
                        ++count;
                    }
                }
                else
                    Console.WriteLine($"Unable to open file {$"File{cameraFileId:X8}.xxx"} in the archive: \n");
            }

            Console.WriteLine($"Extracted {count} Camera files.");
        }

        static void ExtractGameTablesFiles()
        {
            Console.WriteLine("Extracting game tables...\n");

            string path = $"{baseDirectory}/gt";
            CreateDirectory(path);

            uint count = 0;
            foreach (var fileName in FileList.GameTables)
            {
                var dbcFile = cascHandler.ReadFile(fileName);
                if (dbcFile == null)
                {
                    Console.WriteLine($"Unable to open file {fileName} in the archive\n");
                    continue;
                }

                string file = $"{Environment.CurrentDirectory}/gt/{fileName.Replace("GameTables\\", "")}";
                if (!File.Exists(file))
                {
                    FileWriter.WriteFile(dbcFile, file);
                    ++count;
                }
            }

            Console.WriteLine($"Extracted {count} files\n\n");
        }

        static void ExtractMaps(uint build)
        {
            Console.WriteLine("Extracting maps...\n");
            Console.WriteLine("Loading db2 files...\n");

            var stream = cascHandler.ReadFile("DBFilesClient\\Map.db2");
            if (stream == null)
            {
                Console.WriteLine("Unable to open file DBFilesClient\\Map.db2 in the archive\n");
                return;
            }
            mapStorage = DBReader.Read<MapRecord>(stream);
            if (mapStorage == null)
            {
                Console.WriteLine("Fatal error: Invalid Map.db2 file format!\n");
                return;
            }

            stream = cascHandler.ReadFile("DBFilesClient\\LiquidType.db2");
            if (stream == null)
            {
                Console.WriteLine("Unable to open file DBFilesClient\\LiquidType.db2 in the archive\n");
                return;
            }

            liquidTypeStorage = DBReader.Read<LiquidTypeRecord>(stream);
            if (liquidTypeStorage == null)
            {
                Console.WriteLine("Fatal error: Invalid LiquidType.db2 file format!\n");
                return;
            }

            string path = $"{baseDirectory}/maps";
            CreateDirectory(path);

            Console.WriteLine("Convert map files\n");
            int count = 1;
            foreach (var map in mapStorage.Values)
            {
                Console.Write($"Extract {map.MapName} ({count}/{mapStorage.Count})                  \n");
                // Loadup map grid data
                string storagePath = $"World\\Maps\\{map.Directory}\\{map.Directory}.wdt";
                ChunkedFile wdt = new ChunkedFile();
                if (wdt.loadFile(cascHandler, storagePath))
                {
                    wdt_MAIN main = wdt.GetChunk("MAIN").As<wdt_MAIN>();
                    for (int y = 0; y < 64; ++y)
                    {
                        for (int x = 0; x < 64; ++x)
                        {
                            if (Convert.ToBoolean(main.adt_list[y][x].flag & 0x1))
                            {
                                storagePath = $"World\\Maps\\{map.Directory}\\{map.Directory}_{x}_{y}.adt";
                                string outputFileName = $"{path}/{map.Id:D4}_{y:D2}_{x:D2}.map";
                                MapFile.ConvertADT(cascHandler, storagePath, outputFileName, y, x, build);
                            }
                        }
                        // draw progress bar
                        Console.Write($"Processing........................{(100 * (y + 1)) / 64}%\r");
                    }
                    count++;
                }
            }
            Console.WriteLine("\n");
        }

        static void ExtractVMaps()
        {
            Console.WriteLine("Extracting Vmap files...");

            CreateDirectory(wmoDirectory);
            File.Delete(wmoDirectory + "dir_bin");

            // Extract models, listed in GameObjectDisplayInfo.dbc
            VmapFile.ExtractGameobjectModels();

            Console.WriteLine("Read Map.dbc file... ");
            if (mapStorage == null)
            {
                var stream = cascHandler.ReadFile("DBFilesClient\\Map.db2");
                if (stream == null)
                {
                    Console.WriteLine("Unable to open file DBFilesClient\\Map.db2 in the archive\n");
                    return;
                }
                mapStorage = DBReader.Read<MapRecord>(stream);
            }

            if (mapStorage == null)
            {
                Console.WriteLine("Fatal error: Invalid Map.db2 file format!\n");
                return;
            }

            VmapFile.ParsMapFiles();
            Console.WriteLine("Extracting Done!");

            Console.WriteLine("Converting Vmap files...");
            CreateDirectory("./vmaps");

            TileAssembler ta = new TileAssembler(wmoDirectory, "vmaps");
            if (!ta.convertWorld2())
                return;

            Console.WriteLine("Converting Done!");
        }

        static void ExtractMMaps()
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

            Console.WriteLine("Extracting MMap files...");

            MapBuilder builder = new MapBuilder();

            var watch = System.Diagnostics.Stopwatch.StartNew();
            builder.buildAllMaps(0);

            Console.WriteLine($"Finished. MMAPS were built in {watch.ElapsedMilliseconds} ms!");
        }

        public static void CreateDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);

            Directory.CreateDirectory(path);
        }

        public static CASCHandler cascHandler { get; set; }
        public static string baseDirectory { get; set; }
        public static string wmoDirectory { get; set; } = "./Buildings/";

        static Dictionary<uint, MapRecord> mapStorage = new Dictionary<uint, MapRecord>();
        public static Dictionary<uint, LiquidTypeRecord> liquidTypeStorage = new Dictionary<uint, LiquidTypeRecord>();
    }
}
