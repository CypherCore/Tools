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

using DataExtractor.Framework.ClientReader;
using DataExtractor.Framework.Constants;
using DataExtractor.Map;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DataExtractor.Vmap
{
    class VmapFile
    {
        public static void ExtractGameobjectModels()
        {
            var gobDisplayInfo = DBReader.Read<GameObjectDisplayInfoRecord>(1266277);
            if (gobDisplayInfo == null)
            {
                Console.WriteLine("Fatal error: Invalid GameObjectDisplayInfo.db2 file format!\n");
                return;
            }

            Console.WriteLine("Extracting GameObject models...");

            string modelListPath = Program.BuildingsDirectory + "temp_gameobject_models";
            using (BinaryWriter binaryWriter = new(File.Open(modelListPath, FileMode.Create, FileAccess.Write)))
            {
                binaryWriter.WriteCString(SharedConst.RAW_VMAP_MAGIC);

                int i = 0;
                foreach (var record in gobDisplayInfo.Values)
                {
                    uint fileId = record.FileDataID;
                    if (fileId == 0)
                        continue;

                    bool result = false;
                    if (!GetHeaderMagic(fileId, out string header))
                        continue;

                    bool isWmo = false;
                    string fileName = $"FILE{fileId:X8}.xxx";
                    if (header == "REVM")
                    {
                        isWmo = true;
                        result = ExtractSingleWmo(fileId);
                    }
                    else if (header == "MD20" || header == "MD21")
                        result = ExtractSingleModel(fileName);

                    if (result)
                    {
                        binaryWriter.Write(record.Id);
                        binaryWriter.Write(isWmo);
                        binaryWriter.Write(fileName.GetByteCount());
                        binaryWriter.WriteString(fileName);
                    }

                    Console.Write($"Extracting........................{(100 * (i++ + 1)) / gobDisplayInfo.Count}%\r");
                }
                Console.Write("\n");
            }

            Console.WriteLine("Done!");
        }

        public static void ParsMapFiles(CASCLib.CASCHandler cascHandler)
        {
            for (var i = 0; i < 64; ++i)
                adtCache[i] = new ChunkedFile[64];

            var mapStorage = DBReader.Read<MapRecord>(1349477);
            if (mapStorage == null)
            {
                Console.WriteLine("Fatal error: Invalid Map.db2 file format!\n");
                return;
            }

            Dictionary<uint, WDTFile> wdts = new();
            WDTFile getWDT(uint mapId)
            {
                WDTFile wdtFile = wdts.LookupByKey(mapId);
                if (wdtFile == null)
                {
                    MapRecord record = mapStorage.LookupByKey(mapId);
                    if (record == null)
                        return null;

                    wdtFile = new WDTFile();
                    if (!wdtFile.LoadFile(cascHandler, $"world/maps/{record.Directory}/{record.Directory}.wdt") || !wdtFile.Init(mapId))
                        return null;

                    wdts.Add(mapId, wdtFile);
                }

                return wdtFile;
            }

            foreach (var (id, record) in mapStorage)
            {
                WDTFile wdtFile = getWDT(id);
                if (wdtFile != null)
                {
                    Console.Write($"Processing Map {id}\n");

                    for (uint x = 0; x < 64; ++x)
                    {
                        for (uint y = 0; y < 64; ++y)
                        {
                            bool success = true;

                            if (GetMapFromWDT(cascHandler, wdtFile, x, y, record.Directory) is not ADTFile adt || !adt.Init(id, id))
                                success = false;

                            if (!success)
                            {
                                WDTFile parentWDT = record.ParentMapID >= 0 ? getWDT((uint)record.ParentMapID) : null;
                                MapRecord parentMap = mapStorage.LookupByKey((uint)record.ParentMapID);
                                if (parentMap != null && parentWDT != null)
                                {
                                    if (GetMapFromWDT(cascHandler, parentWDT, x, y, parentMap.Directory) is not ADTFile parentAdt || !parentAdt.Init(id, id))
                                    {
                                        Console.WriteLine($"Failed to process map {id} and {record.ParentMapID}");
                                        continue;
                                    }
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
            string fileName = $"FILE{fileId:X8}.xxx";
            if (File.Exists(Program.BuildingsDirectory + fileName))
                return true;

            bool fileOk = true;
            // Console.WriteLine($"Extracting {fileName}");
            WMORoot wmoRoot = new();
            if (!wmoRoot.Open(fileId))
            {
                Console.WriteLine("Couldn't open RootWmo!!!");
                return true;
            }

            using (BinaryWriter binaryWriter = new(File.Open(Program.BuildingsDirectory + fileName, FileMode.Create, FileAccess.Write)))
            {
                wmoRoot.ConvertToVMAPRootWmo(binaryWriter);

                WmoDoodads[fileName] = wmoRoot.DoodadData;
                WMODoodadData doodads = WmoDoodads[fileName];
                int wmoVertices = 0;
                for (int i = 0; i < wmoRoot.groupFileDataIDs.Count; ++i)
                {
                    WMOGroup wmoGroup = new();
                    if (!wmoGroup.Open(wmoRoot.groupFileDataIDs[i], wmoRoot))
                    {
                        Console.WriteLine($"Could not open all Group files for: {fileName}");
                        fileOk = false;
                        break;
                    }

                    wmoVertices += wmoGroup.ConvertToVMAPGroupWmo(binaryWriter, false);
                    foreach (ushort groupReference in wmoGroup.DoodadReferences)
                    {
                        if (groupReference >= doodads.Spawns.Count)
                            continue;

                        uint doodadNameIndex = doodads.Spawns[groupReference].NameIndex;
                        if (!wmoRoot.ValidDoodadNames.Contains(doodadNameIndex))
                            continue;

                        doodads.References.Add(groupReference);
                    }
                }

                binaryWriter.Seek(8, SeekOrigin.Begin); // store the correct no of vertices
                binaryWriter.Write(wmoVertices);

                // Delete the extracted file in the case of an error
                if (!fileOk)
                    File.Delete(Program.BuildingsDirectory + fileName);
            }

            return true;
        }

        public static bool ExtractSingleWmo(string fileName)
        {
            // Copy files from archive
            string plainName = fileName.GetPlainName();
            if (File.Exists(Program.BuildingsDirectory + plainName))
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

            bool fileOK = true;
            // Console.WriteLine($"Extracting {fileName}");

            WMORoot wmoRoot = new();
            if (!File.Exists($"{Program.BuildingsDirectory}/{fileName}") || !wmoRoot.Read(File.Open($"{Program.BuildingsDirectory}/{fileName}", FileMode.Open)))
            {
                if (!wmoRoot.Read(Program.CascHandler.OpenFile(fileName)))
                {
                    Console.WriteLine("Couldn't open RootWmo!!!");
                    return false;
                }
            }

            using (BinaryWriter binaryWriter = new(File.Open(Program.BuildingsDirectory + plainName, FileMode.Create, FileAccess.Write)))
            {
                wmoRoot.ConvertToVMAPRootWmo(binaryWriter);

                WmoDoodads[plainName] = wmoRoot.DoodadData;
                WMODoodadData doodads = WmoDoodads[plainName];
                int wmoVertices = 0;
                for (int i = 0; i < wmoRoot.groupFileDataIDs.Count; ++i)
                {
                    WMOGroup wmoGroup = new();
                    if (!wmoGroup.Open(wmoRoot.groupFileDataIDs[i], wmoRoot))
                    {
                        Console.WriteLine($"Could not open all Group file for: {plainName}");
                        fileOK = false;
                        break;
                    }

                    wmoVertices += wmoGroup.ConvertToVMAPGroupWmo(binaryWriter, false);
                    foreach (ushort groupReference in wmoGroup.DoodadReferences)
                    {
                        if (groupReference >= doodads.Spawns.Count)
                            continue;

                        uint doodadNameIndex = doodads.Spawns[groupReference].NameIndex;
                        if (!wmoRoot.ValidDoodadNames.Contains(doodadNameIndex))
                            continue;

                        doodads.References.Add(groupReference);
                    }
                }

                binaryWriter.Seek(8, SeekOrigin.Begin); // store the correct no of vertices
                binaryWriter.Write(wmoVertices);

                // Delete the extracted file in the case of an error
                if (!fileOK)
                    File.Delete(Program.BuildingsDirectory + fileName);
            }

            return true;
        }

        public static bool ExtractSingleModel(uint fileId)
        {
            string outputFile = Program.BuildingsDirectory + $"FILE{fileId:X8}.xxx";
            if (File.Exists(outputFile))
                return true;

            Model mdl = new();
            if (!mdl.Open(fileId))
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

            string outputFile = Program.BuildingsDirectory + fileName.GetPlainName();
            if (File.Exists(outputFile))
                return true;

            Model mdl = new();
            if (!mdl.Open(fileName))
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

        public static ChunkedFile GetMapFromWDT(CASCLib.CASCHandler cascHandler, ChunkedFile wdt, uint x, uint y, string mapName)
        {
            if (!(x >= 0 && y >= 0 && x < 64 && y < 64))
                return null;

            if (adtCache[x][y] != null)
                return adtCache[x][y];

            MPHD header = wdt.GetChunk("MPHD").As<MPHD>();
            if (header == null)
                return null;

            MAIN main = wdt.GetChunk("MAIN").As<MAIN>();
            if (main == null || (main.MapAreaInfo[y][x].Flag & 0x1) == 0)
                return null;

            MAID mapFileIDs = wdt.GetChunk("MAID").As<MAID>();
            if (mapFileIDs == null)
                return null;

            if (mapFileIDs.MapFileDataIDs[y][x].Obj0ADT == 0)
                return null;

            ADTFile adt = new(adtCache[x][y] != null);
            if ((header.Flags & 0x200) != 0)
                adt.LoadFile(cascHandler, mapFileIDs.MapFileDataIDs[y][x].Obj0ADT, $"Obj0ADT {x}_{y} for {mapName}");
            else
                adt.LoadFile(cascHandler, $"world/maps/{mapName}/{mapName}_{x}_{y}_obj0.adt");

            adtCache[x][y] = adt;
            return adt;
        }

        static ChunkedFile[][] adtCache = new ChunkedFile[64][];

        static Dictionary<Tuple<uint, ushort>, uint> uniqueObjectIds = new();
        public static Dictionary<string, WMODoodadData> WmoDoodads = new();
    }
}
