using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CASC.Handlers;
using DataExtractor.Constants;
using System.Text.RegularExpressions;
using CASC.Constants;
using System.IO;
using CASC;
using DataExtractor.ClientReader;

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

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Select your task.");
            Console.WriteLine("1 - Extract DB2 and maps");
            //Console.WriteLine("2 - Extract vmaps(needs maps to be extracted before you run this)");
            //Console.WriteLine("3 - Extract mmaps(needs vmaps to be extracted before you run this, may take hours)");
            Console.WriteLine("4 - Extract all(may take hours)");
            Console.WriteLine("5 - EXIT");

            string answer = Console.ReadLine();
            if (answer == "5")
                Environment.Exit(0);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Initializing CASC library...");
            cascHandler = new CASCHandler(Environment.CurrentDirectory);
            currentDirectory = Environment.CurrentDirectory;
            Console.WriteLine("Done.");

            switch (answer)
            {
                case "1":
                    ExtractDbcFiles();
                    ExtractMaps(cascHandler.GetBuildNumber());
                    break;
                case "2":
                    ExtractVMaps();
                    break;
                case "3":
                    ExtractMMaps();
                    break;
                case "4":
                default:
                    ExtractDbcFiles();
                    ExtractMaps(cascHandler.GetBuildNumber());
                    ExtractVMaps();
                    ExtractMMaps();
                    break;
            }        
        }

        static void ExtractDbcFiles()
        {
            string path = $"{currentDirectory}/dbc";
            CreateDirectory(path);

            List<Locale> locales = new List<Locale>();
            var buildInfoLocales = Regex.Matches(cascHandler.buildInfo["Tags"], " ([A-Za-z]{4}) speech");
            foreach (Match m in buildInfoLocales)
            {
                var localFlag = (Locale)Enum.Parse(typeof(Locale), m.Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0]);

                if (!locales.Contains(localFlag))
                    locales.Add(localFlag);
            }

            Console.WriteLine("Extracting db2 files...");
            uint count = 0;
            for (int i = 0; i < locales.Count; ++i)
            {
                if (locales[i] == Locale.None)
                    continue;

                string currentPath = $"{path}/{locales[i]}";
                CreateDirectory(currentPath);

                foreach (var fileName in FileList.DBFilesClientList)
                {
                    var dbcStream = cascHandler.ReadFile(fileName, SharedConst.WowLocaleToCascLocaleFlags[(int)locales[i]]);
                    if (dbcStream == null)
                    {
                        Console.WriteLine($"Unable to open file {fileName} in the archive for locale {locales[i]}\n");
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

            Dictionary<uint, CinematicCameraRecord> cinematicCameraStorage = DB6Reader.Read<CinematicCameraRecord>(stream, DB6Metas.CinematicCameraMeta);
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

            string path = $"{currentDirectory}/cameras";
            CreateDirectory(path);

            // extract M2s
            uint count = 0;
            foreach (int cameraFileId in CameraFileNames)
            {
                var cameraStream = cascHandler.ReadFile(cameraFileId);
                if (cameraStream != null)
                {
                    string file = string.Format(path + "/File{0:X8}.xxx", cameraFileId);
                    if (!File.Exists(file))
                    {
                        FileWriter.WriteFile(cameraStream, file);
                        ++count;
                    }
                }
                else
                    Console.WriteLine($"Unable to open file {String.Format("File{0:x8}.xxx", cameraFileId)} in the archive: \n");
            }

            Console.WriteLine($"Extracted {count} Camera files.");
        }

        static void ExtractGameTablesFiles()
        {
            Console.WriteLine("Extracting game tables...\n");

            string path = $"{currentDirectory}/gt";
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
            mapStorage = DB6Reader.Read<MapRecord>(stream, DB6Metas.MapMeta);
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

            liquidTypeStorage = DB6Reader.Read<LiquidTypeRecord>(stream, DB6Metas.LiquidTypeMeta);
            if (liquidTypeStorage == null)
            {
                Console.WriteLine("Fatal error: Invalid LiquidType.db2 file format!\n");
                return;
            }

            string path = $"{currentDirectory}/maps";
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
            //CreateDirectory($"{currentDirectory}/Buildings/dir");
            //CreateDirectory($"{currentDirectory}/Buildings/dir_bin");

            //Console.WriteLine("Extracting Vmap files...");

            // Extract models, listed in GameObjectDisplayInfo.dbc
            //VmapFile.ExtractGameobjectModels();
        }

        static void ExtractMMaps()
        {

        }

        static void CreateDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);

            Directory.CreateDirectory(path);
        }

        static CASCHandler cascHandler;
        static string currentDirectory;

        static Dictionary<uint, MapRecord> mapStorage = new Dictionary<uint, MapRecord>();
        public static Dictionary<uint, LiquidTypeRecord> liquidTypeStorage = new Dictionary<uint, LiquidTypeRecord>();
    }
}
