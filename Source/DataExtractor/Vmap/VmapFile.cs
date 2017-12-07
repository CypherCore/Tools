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
using System.Text;
using Framework.ClientReader;
using System.IO;
using DataExtractor.Vmap.Collision;

namespace DataExtractor.Vmap
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
            using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(modelListPath, FileMode.Create, FileAccess.Write)))
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
                        binaryWriter.Write(record.Id);
                        binaryWriter.Write(fileName.Length);
                        binaryWriter.WriteString(fileName);
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

            bool file_ok = true;
            Console.WriteLine($"Extracting {fileName}");
            WMORoot froot = new WMORoot();
            if (!froot.open(fileId))
            {
                Console.WriteLine("Couldn't open RootWmo!!!");
                return true;
            }

            using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(Program.wmoDirectory + fileName, FileMode.Create, FileAccess.Write)))
            {
                froot.ConvertToVMAPRootWmo(binaryWriter);
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

                    Wmo_nVertices += fgroup.ConvertToVMAPGroupWmo(binaryWriter, froot, false);// preciseVectorData);
                }

                binaryWriter.Seek(8, SeekOrigin.Begin); // store the correct no of vertices
                binaryWriter.Write(Wmo_nVertices);

                // Delete the extracted file in the case of an error
                if (!file_ok)
                    File.Delete(Program.wmoDirectory + fileName);
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

            using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(Program.wmoDirectory + plainName, FileMode.Create, FileAccess.Write)))
            {
                froot.ConvertToVMAPRootWmo(binaryWriter);
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

                    Wmo_nVertices += fgroup.ConvertToVMAPGroupWmo(binaryWriter, froot, false);// preciseVectorData);
                }

                binaryWriter.Seek(8, SeekOrigin.Begin); // store the correct no of vertices
                binaryWriter.Write(Wmo_nVertices);

                // Delete the extracted file in the case of an error
                if (!file_ok)
                    File.Delete(Program.wmoDirectory + fileName);
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

            string outputFile = Program.wmoDirectory + fileName.GetPlainName();
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
