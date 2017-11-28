using System;
using System.Collections.Generic;
using System.Text;
using CASC.Handlers;
using DataExtractor.ClientReader;
using System.IO;
using CASC;
using System.Linq;

namespace DataExtractor
{
    class VmapFile
    {
        public static void ExtractGameobjectModels()
        {
            Console.WriteLine("Extracting GameObject models...");

            var stream = Program.cascHandler.ReadFile("DBFilesClient\\GameObjectDisplayInfo.db2");
            if (stream == null)
            {
                Console.WriteLine("Unable to open file DBFilesClient\\Map.db2 in the archive\n");
                return;
            }
            var GameObjectDisplayInfoStorage = DB6Reader.Read<GameObjectDisplayInfoRecord>(stream, DB6Metas.GameObjectDisplayInfoMeta);
            if (GameObjectDisplayInfoStorage == null)
            {
                Console.WriteLine("Fatal error: Invalid GameObjectDisplayInfo.db2 file format!\n");
                return;
            }

            string modelListPath = Program.wmoDirectory + "temp_gameobject_models";
            using (var fs = new FileStream(modelListPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, true))
            {
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    foreach (var record in GameObjectDisplayInfoStorage.Values)
                    {
                        uint fileId = record.FileDataID;
                        if (fileId == 0)
                            continue;

                        bool result = false;
                        string header;
                        if (!GetHeaderMagic(fileId, out header))
                            continue;

                        string fileName = $"File{fileId:X8}.xxx";
                        if (header == "REVM")
                            result = ExtractSingleWmo(fileId);
                        else if (header == "MD20" || header == "MD21")
                            result = ExtractSingleModel(fileId);
                        //else
                        //ASSERT(false, "%s header: %d - %c%c%c%c", fileId, header, (header >> 24) & 0xFF, (header >> 16) & 0xFF, (header >> 8) & 0xFF, header & 0xFF);

                        if (result)
                        {
                            writer.Write(record.Id);
                            writer.Write(fileName.Length);
                            writer.Write(fileName);
                        }
                    }
                }
            }

            Console.WriteLine("Done!");
        }

        public static void ParsMapFiles()
        {
            var stream = Program.cascHandler.ReadFile("DBFilesClient\\Map.db2");
            if (stream == null)
            {
                Console.WriteLine("Unable to open file DBFilesClient\\Map.db2 in the archive\n");
                return;
            }

            var mapStorage = DB6Reader.Read<MapRecord>(stream, DB6Metas.MapMeta);
            if (mapStorage == null)
            {
                Console.WriteLine("Fatal error: Invalid Map.db2 file format!\n");
                return;
            }

            foreach (var record in mapStorage.Values)
            {
                WDTFile WDT = new WDTFile();
                if (WDT.init($"World\\Maps\\{record.Directory}\\{record.Directory}.wdt", record.Id))
                {
                    Console.Write($"Processing Map {record.Id}\n");
                    for (uint x = 0; x < 64; ++x)
                    {
                        for (uint y = 0; y < 64; ++y)
                        {
                            ADTFile ADT = new ADTFile();
                            ADT.init($"World\\Maps\\{record.Directory}\\{record.Directory}_{x}_{y}_obj0.adt", record.Id, x, y);
                        }
                        // draw progress bar
                        Console.Write($"Processing........................{(100 * (x + 1)) / 64}%\r");
                    }
                    Console.Write("\n");
                }
            }
        }

        public static bool ExtractSingleWmo(uint fileId)
        {
            // Copy files from archive
            string fileName = $"File{fileId:X8}.xxx";
            if (File.Exists(Program.wmoDirectory + fileName))
                return true;

            int p = 0;
            // Select root wmo files
            /*char const* rchr = strrchr(plain_name, '_');
            if (rchr != NULL)
            {
                char cpy[4];
                memcpy(cpy, rchr, 4);
                for (int i = 0; i < 4; ++i)
                {
                    int m = cpy[i];
                    if (isdigit(m))
                        p++;
                }
            }*/

            if (p == 3)
                return true;

            bool file_ok = true;
            Console.WriteLine($"Extracting {fileName}");
            WMORoot froot = new WMORoot();
            if (!froot.open(fileId))
            {
                Console.WriteLine("Couldn't open RootWmo!!!");
                return true;
            }

            using (var fs = new FileStream(Program.wmoDirectory + fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, true))
            {
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    froot.ConvertToVMAPRootWmo(writer);
                    int Wmo_nVertices = 0;
                    //printf("root has %d groups\n", froot->nGroups);
                    for (int i = 0; i < froot.groupFileDataIDs.Count; ++i)
                    {
                        WMOGroup fgroup = new WMOGroup();
                        if (!fgroup.open(froot.groupFileDataIDs[i]))
                        {
                            Console.WriteLine($"Could not open all Group file for: {fileId.ToString()}");
                            file_ok = false;
                            break;
                        }

                        Wmo_nVertices += fgroup.ConvertToVMAPGroupWmo(writer, froot, false);// preciseVectorData);
                    }

                    writer.Seek(8, SeekOrigin.Begin); // store the correct no of vertices
                    writer.Write(Wmo_nVertices);

                    // Delete the extracted file in the case of an error
                    if (!file_ok)
                        File.Delete(Program.wmoDirectory + fileName);
                }
            }
            return true;
        }

        public static bool ExtractSingleWmo(string fileName)
        {
            // Copy files from archive
            string plainName = fileName.GetPlainName();
            if (File.Exists(Program.wmoDirectory + plainName))
                return true;

            int p = 0;
            // Select root wmo files
            int index = plainName.IndexOf('_');
            if (index != -1)
            {
                string rchr = plainName.Substring(index);
                if (rchr != null)
                {
                    for (int i = 0; i < 4; ++i)
                    {
                        char m = rchr[i];
                        if (char.IsDigit(m))
                            p++;
                    }
                }
            }

            if (p == 3)
                return true;

            bool file_ok = true;
            Console.WriteLine($"Extracting {fileName}");
            WMORoot froot = new WMORoot();
            if (!froot.open(fileName))
            {
                Console.WriteLine("Couldn't open RootWmo!!!");
                return true;
            }

            using (var fs = new FileStream(Program.wmoDirectory + plainName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, true))
            {
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    froot.ConvertToVMAPRootWmo(writer);
                    int Wmo_nVertices = 0;
                    //printf("root has %d groups\n", froot->nGroups);
                    for (int i = 0; i < froot.groupFileDataIDs.Count; ++i)
                    {
                        WMOGroup fgroup = new WMOGroup();
                        if (!fgroup.open(froot.groupFileDataIDs[i]))
                        {
                            Console.WriteLine($"Could not open all Group file for: {plainName}");
                            file_ok = false;
                            break;
                        }

                        Wmo_nVertices += fgroup.ConvertToVMAPGroupWmo(writer, froot, false);// preciseVectorData);
                    }

                    writer.Seek(8, SeekOrigin.Begin); // store the correct no of vertices
                    writer.Write(Wmo_nVertices);

                    // Delete the extracted file in the case of an error
                    if (!file_ok)
                        File.Delete(Program.wmoDirectory + fileName);
                }
            }
            return true;
        }

        public static bool ExtractSingleModel(uint fileId)
        {
            string outputFile = Program.wmoDirectory + $"File{fileId:X8}.xxx";
            if (File.Exists(outputFile))
                return true;

            Model mdl = new Model();
            if (!mdl.open(fileId))
                return false;

            return mdl.ConvertToVMAPModel(outputFile);
        }

        public static bool ExtractSingleModel(string fileName)
        {
            if (fileName.Substring(fileName.Length - 4, 4) == ".mdx")
            {
                fileName.Remove(fileName.Length - 2, 2);
                fileName += "2";
            }

            string name = fileName.GetPlainName();
            if (name == "6Du_Highmaulraid_Arena_Elevator.m2")
            {

            }
            string outputFile = Program.wmoDirectory + name;
            if (File.Exists(outputFile))
                return true;

            Model mdl = new Model();
            if (!mdl.open(fileName))
                return false;

            return mdl.ConvertToVMAPModel(outputFile);
        }

        static bool GetHeaderMagic(uint fileId, out string magic)
        {
            magic = "";
            var file = Program.cascHandler.ReadFile((int)fileId);
            if (file == null)
                return false;

            byte[] bytes = new byte[4];
            if (file.Read(bytes, 0, 4) != 4)
                return false;

            magic = Encoding.UTF8.GetString(bytes);
            return true;
        }
    }
}
