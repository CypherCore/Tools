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
using System.Collections.Generic;
using System.Linq;
using Framework.Constants;

namespace DataExtractor.Vmap
{
    class VmapFile
    {
        public static void ExtractGameobjectModels()
        {
            Console.WriteLine("Extracting GameObject models...");

            string modelListPath = Program.wmoDirectory + "temp_gameobject_models";
            using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(modelListPath, FileMode.Create, FileAccess.Write)))
            {
                binaryWriter.WriteCString(SharedConst.RAW_VMAP_MAGIC);

                foreach (var record in CliDB.GameObjectDisplayInfoStorage.Values)
                {
                    uint fileId = record.FileDataID;
                    if (fileId == 0)
                        continue;

                    bool result = false;
                    string header;
                    if (!GetHeaderMagic(fileId, out header))
                        continue;

                    bool isWmo = false;
                    string fileName = $"File{fileId:X8}.xxx";
                    if (header == "REVM")
                    {
                        isWmo = true;
                        result = ExtractSingleWmo(fileId);
                    }
                    else if (header == "MD20" || header == "MD21")
                        result = ExtractSingleModel(fileId);
                    else
                    {

                    }

                    if (result)
                    {
                        binaryWriter.Write(record.Id);
                        binaryWriter.Write(isWmo);
                        binaryWriter.Write(fileName.Length);
                        binaryWriter.WriteString(fileName);
                    }
                }
            }

            Console.WriteLine("Done!");
        }

        public static void ParsMapFiles()
        {
            Dictionary<uint, Tuple<string, short>> map_ids = new Dictionary<uint, Tuple<string, short>>();
            List<uint> maps_that_are_parents = new List<uint>();

            foreach (var record in CliDB.MapStorage.Values)
            {
                map_ids[record.Id] = Tuple.Create(record.Directory, record.ParentMapID);
                if (record.ParentMapID >= 0)
                    maps_that_are_parents.Add((uint)record.ParentMapID);
            }

            Dictionary<uint, WDTFile> wdts = new Dictionary<uint, WDTFile>();
            Func<uint, WDTFile> getWDT = mapId =>
            {
                var wdtFile = wdts.LookupByKey(mapId);
                if (wdtFile == null)
                {
                    string fn = $"World\\Maps\\{map_ids[mapId].Item1}\\{map_ids[mapId].Item1}";
                    bool v1 = maps_that_are_parents.Contains(mapId);
                    bool v2 = map_ids[mapId].Item2 != -1;

                    wdtFile = new WDTFile(fn, maps_that_are_parents.Contains(mapId));
                    wdts.Add(mapId, wdtFile);
                    if (!wdtFile.init(mapId))
                    {
                        wdts.Remove(mapId);
                        return null;
                    }
                }

                return wdtFile;
            };

            foreach (var pair in map_ids)
            {
                WDTFile WDT = getWDT(pair.Key);
                if (WDT != null)
                {
                    WDTFile parentWDT = pair.Value.Item2 >= 0 ? getWDT((uint)pair.Value.Item2) : null;
                    Console.Write($"Processing Map {pair.Key}\n");
                    for (uint x = 0; x < 64; ++x)
                    {
                        for (uint y = 0; y < 64; ++y)
                        {
                            bool success = false;
                            ADTFile ADT = WDT.GetMap(x, y);
                            if (ADT != null)
                            {
                                success = ADT.init(pair.Key, x, y, pair.Key);
                            }

                            if (!success && parentWDT != null)
                            {
                                ADTFile parentADT = parentWDT.GetMap(x, y);
                                if (parentADT != null)
                                {
                                    parentADT.init(pair.Key, x, y, (uint)pair.Value.Item2);
                                }
                            }
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
                for (int i = 0; i < froot.groupFileDataIDs.Count; ++i)
                {
                    WMOGroup fgroup = new WMOGroup();
                    if (!fgroup.open(froot.groupFileDataIDs[i], froot))
                    {
                        Console.WriteLine($"Could not open all Group file for: {fileId.ToString()}");
                        file_ok = false;
                        break;
                    }

                    Wmo_nVertices += fgroup.ConvertToVMAPGroupWmo(binaryWriter, false);
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
                for (int i = 0; i < froot.groupFileDataIDs.Count; ++i)
                {
                    WMOGroup fgroup = new WMOGroup();
                    if (!fgroup.open(froot.groupFileDataIDs[i], froot))
                    {
                        Console.WriteLine($"Could not open all Group file for: {plainName}");
                        file_ok = false;
                        break;
                    }

                    Wmo_nVertices += fgroup.ConvertToVMAPGroupWmo(binaryWriter, false);
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
