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
using DataExtractor.Map;
using System;
using System.Collections.Generic;
using System.IO;

namespace DataExtractor.Vmap
{
    class ADTFile : ChunkedFile
    {
        public ADTFile(bool cache)
        {
            cacheable = cache;
            dirFileCache = null;
        }

        public bool Init(uint mapNum, uint originalMapId)
        {
            if (dirFileCache != null)
                return InitFromCache(mapNum, originalMapId);

            if (cacheable)
                dirFileCache = new List<ADTOutputCache>();

            FilenameChunk mmdx = GetChunk("MMDX")?.As<FilenameChunk>();
            if (mmdx != null && mmdx.Filenames.Count > 0)
            {
                foreach (var filename in mmdx.Filenames)
                {
                    modelInstanceNames.Add(filename);
                    VmapFile.ExtractSingleModel(filename);
                }
            }

            FilenameChunk mwmo = GetChunk("MWMO")?.As<FilenameChunk>();
            if (mwmo != null && mwmo.Filenames.Count > 0)
            {
                foreach (var filename in mwmo.Filenames)
                {
                    wmoInstanceNames.Add(filename);
                    VmapFile.ExtractSingleWmo(filename);
                }
            }

            MDDF doodadChunk = GetChunk("MDDF")?.As<MDDF>();
            if (doodadChunk != null && doodadChunk.DoodadDefs.Length > 0)
            {
                foreach (var doodad in doodadChunk.DoodadDefs)
                {
                    if (doodad.Flags.HasAnyFlag(MDDFFlags.EntryIsFileID))
                    {
                        string fileName = $"FILE{doodad.Id:X8}.xxx";
                        VmapFile.ExtractSingleModel(fileName);
                        Model.Extract(doodad, fileName, mapNum, originalMapId, Program.DirBinWriter, dirFileCache);
                    }
                    else
                        Model.Extract(doodad, modelInstanceNames[(int)doodad.Id], mapNum, originalMapId, Program.DirBinWriter, dirFileCache);
                }

                modelInstanceNames.Clear();
            }

            MODF wmoChunk = GetChunk("MODF")?.As<MODF>();
            if (wmoChunk != null && wmoChunk.MapObjDefs.Length > 0)
            {
                foreach (var wmo in wmoChunk.MapObjDefs)
                {
                    if (wmo.Flags.HasAnyFlag(MODFFlags.EntryIsFileID))
                    {
                        string fileName = $"FILE{wmo.Id:X8}.xxx";
                        VmapFile.ExtractSingleWmo(wmo.Id);
                        WMORoot.Extract(wmo, fileName, false, mapNum, originalMapId, Program.DirBinWriter, dirFileCache);

                        if (VmapFile.WmoDoodads.ContainsKey(fileName))
                            Model.ExtractSet(VmapFile.WmoDoodads[fileName], wmo, false, mapNum, originalMapId, Program.DirBinWriter, dirFileCache);
                    }
                    else
                    {
                        WMORoot.Extract(wmo, wmoInstanceNames[(int)wmo.Id], false, mapNum, originalMapId, Program.DirBinWriter, dirFileCache);
                        if (VmapFile.WmoDoodads.ContainsKey(wmoInstanceNames[(int)wmo.Id]))
                            Model.ExtractSet(VmapFile.WmoDoodads[wmoInstanceNames[(int)wmo.Id]], wmo, false, mapNum, originalMapId, Program.DirBinWriter, dirFileCache);
                    }
                }

                wmoInstanceNames.Clear();
            }

            return true;
        }

        bool InitFromCache(uint MapNum, uint originalMapId)
        {
            if (dirFileCache.Empty())
                return true;

            string dirname = Program.BuildingsDirectory + "dir_bin";
            using (BinaryWriter binaryWriter = new(File.Open(dirname, FileMode.Append, FileAccess.Write)))
            {
                foreach (ADTOutputCache cached in dirFileCache)
                {
                    binaryWriter.Write(MapNum);
                    uint flags = cached.Flags;
                    if (MapNum != originalMapId)
                        flags |= ModelFlags.ParentSpawn;
                    binaryWriter.Write(flags);
                    binaryWriter.Write(cached.Data);
                }
            }

            return true;
        }

        bool cacheable;
        List<ADTOutputCache> dirFileCache = new();
        List<string> wmoInstanceNames = new();
        List<string> modelInstanceNames = new();
    }

    public struct ADTOutputCache
    {
        public uint Flags { get; set; }
        public byte[] Data { get; set; }
    }
}