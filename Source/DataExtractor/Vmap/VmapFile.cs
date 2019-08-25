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

using DataExtractor.Framework.Constants;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DataExtractor.Framework.ClientReader;

namespace DataExtractor.Vmap
{
    class VmapFile
    {
        public static void ExtractGameobjectModels()
        {
            var GameObjectDisplayInfoStorage = DBReader.Read<GameObjectDisplayInfoRecord>(1266277);
            if (GameObjectDisplayInfoStorage == null)
            {
                Console.WriteLine("Fatal error: Invalid GameObjectDisplayInfo.db2 file format!\n");
                return;
            }

            Console.WriteLine("Extracting GameObject models...");

            string modelListPath = Program.WmoDirectory + "temp_gameobject_models";
            using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(modelListPath, FileMode.Create, FileAccess.Write)))
            {
                binaryWriter.WriteCString(SharedConst.RAW_VMAP_MAGIC);

                foreach (var record in GameObjectDisplayInfoStorage.Values)
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
                        binaryWriter.Write(fileName.GetByteCount());
                        binaryWriter.WriteString(fileName);
                    }
                }
            }

            Console.WriteLine("Done!");
        }

        public static void ParsMapFiles()
        {
            var mapStorage = DBReader.Read<MapRecord>(1349477);
            if (mapStorage == null)
            {
                Console.WriteLine("Fatal error: Invalid Map.db2 file format!\n");
                return;
            }

            Dictionary<uint, MapRecord> map_ids = new Dictionary<uint, MapRecord>();
            List<uint> maps_that_are_parents = new List<uint>();

            foreach (var record in mapStorage.Values)
            {
                map_ids[record.Id] = record;
                if (record.ParentMapID >= 0)
                    maps_that_are_parents.Add((uint)record.ParentMapID);
            }

            Dictionary<uint, WDTFile> wdts = new Dictionary<uint, WDTFile>();
            Func<uint, WDTFile> getWDT = mapId =>
            {
                var wdtFile = wdts.LookupByKey(mapId);
                if (wdtFile == null)
                {
                    uint fileDataId = map_ids[mapId].WdtFileDataID;
                    if (fileDataId == 0)
                        return null;

                    string directory = map_ids[mapId].Directory;

                    wdtFile = new WDTFile(fileDataId, directory, maps_that_are_parents.Contains(mapId));
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
                    WDTFile parentWDT = pair.Value.ParentMapID >= 0 ? getWDT((uint)pair.Value.ParentMapID) : null;
                    Console.Write($"Processing Map {pair.Key}\n");
                    for (uint x = 0; x < 64; ++x)
                    {
                        for (uint y = 0; y < 64; ++y)
                        {
                            bool success = false;
                            ADTFile ADT = WDT.GetMap(x, y);
                            if (ADT != null)
                            {
                                success = ADT.init(pair.Key, pair.Key);
                            }

                            if (!success && parentWDT != null)
                            {
                                ADTFile parentADT = parentWDT.GetMap(x, y);
                                if (parentADT != null)
                                {
                                    parentADT.init(pair.Key, (uint)pair.Value.ParentMapID);
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
            if (File.Exists(Program.WmoDirectory + fileName))
                return true;

            bool file_ok = true;
            Console.WriteLine($"Extracting {fileName}");
            WMORoot froot = new WMORoot();
            if (!froot.open(fileId))
            {
                Console.WriteLine("Couldn't open RootWmo!!!");
                return true;
            }

            using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(Program.WmoDirectory + fileName, FileMode.Create, FileAccess.Write)))
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
                    File.Delete(Program.WmoDirectory + fileName);
            }

            return true;
        }

        public static bool ExtractSingleWmo(string fileName)
        {
            // Copy files from archive
            string plainName = fileName.GetPlainName();
            if (File.Exists(Program.WmoDirectory + plainName))
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

            using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(Program.WmoDirectory + plainName, FileMode.Create, FileAccess.Write)))
            {
                froot.ConvertToVMAPRootWmo(binaryWriter);
                   
                WmoDoodads[plainName] = froot.DoodadData;
                WMODoodadData doodads = WmoDoodads[plainName];
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
                    foreach (ushort groupReference in fgroup.DoodadReferences)
                    {
                        if (groupReference >= doodads.Spawns.Count)
                            continue;

                        uint doodadNameIndex = doodads.Spawns[groupReference].NameIndex;
                        if (!froot.ValidDoodadNames.Contains(doodadNameIndex))
                            continue;

                        doodads.References.Add(groupReference);
                    }
                }

                binaryWriter.Seek(8, SeekOrigin.Begin); // store the correct no of vertices
                binaryWriter.Write(Wmo_nVertices);

                // Delete the extracted file in the case of an error
                if (!file_ok)
                    File.Delete(Program.WmoDirectory + fileName);
            }

            return true;
        }

        public static bool ExtractSingleModel(uint fileId)
        {
            string outputFile = Program.WmoDirectory + $"File{fileId:X8}.xxx";
            if (File.Exists(outputFile))
                return true;

            Model mdl = new Model();
            if (!mdl.open(fileId))
                return false;

            return mdl.ConvertToVMAPModel(outputFile);
        }

        public static bool ExtractSingleModel(string fileName)
        {
            if (fileName.Length < 4)
                return false;

            string extension = fileName.Substring(fileName.Length - 4, 4);
            if (extension == ".mdx" || extension == ".MDX" || extension == ".mdl" || extension == ".MDL")
            {
                fileName = fileName.Remove(fileName.Length - 2, 2);
                fileName += "2";
            }

            string outputFile = Program.WmoDirectory + fileName.GetPlainName();
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
            var file = Program.CascHandler.OpenFile((int)fileId);
            if (file == null)
                return false;

            byte[] bytes = new byte[4];
            if (file.Read(bytes, 0, 4) != 4)
                return false;

            magic = Encoding.UTF8.GetString(bytes);
            return true;
        }

        public static uint GenerateUniqueObjectId(uint clientId, ushort clientDoodadId)
        {
            var key = Tuple.Create(clientId, clientDoodadId);
            if (!uniqueObjectIds.ContainsKey(key))
                uniqueObjectIds.Add(key, (uint)(uniqueObjectIds.Count + 1));

            return uniqueObjectIds[key];
        }

        static Dictionary<Tuple<uint, ushort>, uint> uniqueObjectIds = new Dictionary<Tuple<uint, ushort>, uint>();
        public static Dictionary<string, WMODoodadData> WmoDoodads = new Dictionary<string, WMODoodadData>();
    }
}
