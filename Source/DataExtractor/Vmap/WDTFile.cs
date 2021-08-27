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
    class WDTFile : ChunkedFile
    {
        public bool Init(uint mapId)
        {
            FilenameChunk mwmo = GetChunk("MWMO")?.As<FilenameChunk>();
            if (mwmo != null && mwmo.Filenames.Count > 0)
            {
                foreach (var filename in mwmo.Filenames)
                {
                    wmoInstanceNames.Add(filename);
                    VmapFile.ExtractSingleWmo(filename);
                }
            }

            MODF wmoChunk = GetChunk("MODF")?.As<MODF>();
            if (wmoChunk != null && wmoChunk.MapObjDefs.Length > 0)
            {
                foreach (var wmo in wmoChunk.MapObjDefs)
                {
                    if (wmo.Flags.HasAnyFlag(MODFFlags.EntryIsFileID))
                    {
                        string fileName = $"FILE{wmo.Id:X8}.xxx";
                        VmapFile.ExtractSingleWmo(fileName);
                        WMORoot.Extract(wmo, fileName, false, mapId, mapId, Program.DirBinWriter, null);

                        if (VmapFile.WmoDoodads.ContainsKey(fileName))
                            Model.ExtractSet(VmapFile.WmoDoodads[fileName], wmo, false, mapId, mapId, Program.DirBinWriter, null);
                    }
                    else
                    {
                        WMORoot.Extract(wmo, wmoInstanceNames[(int)wmo.Id], false, mapId, mapId, Program.DirBinWriter, null);
                        if (VmapFile.WmoDoodads.ContainsKey(wmoInstanceNames[(int)wmo.Id]))
                            Model.ExtractSet(VmapFile.WmoDoodads[wmoInstanceNames[(int)wmo.Id]], wmo, false, mapId, mapId, Program.DirBinWriter, null);
                    }
                }

                wmoInstanceNames.Clear();
            }

            return true;
        }

        List<string> wmoInstanceNames = new();
    }
}