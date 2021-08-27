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

//using DataExtractor.Framework.CASC.Handlers;
using DataExtractor.CASCLib;
using DataExtractor.Map;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DataExtractor
{
    class ChunkedFile
    {
        public bool LoadFile(CASCHandler cascHandler, string fileName)
        {
            var file = cascHandler.OpenFile(fileName);
            if (file == null)
                return false;

            var fileSize = file.Length;
            if (fileSize == 0xFFFFFFFF)
                return false;

            dataSize = (uint)fileSize;
            data = new BinaryReader(file).ReadBytes((int)dataSize);

            ParseChunks();
            if (PrepareLoadedData())
                return true;

            Console.WriteLine($"Error loading {fileName}\n");

            return false;
        }

        public bool LoadFile(CASCHandler cascHandler, uint fileDataId, string description)
        {
            var file = cascHandler.OpenFile((int)fileDataId);
            if (file == null)
                return false;

            var fileSize = file.Length;
            if (fileSize == 0xFFFFFFFF)
                return false;

            dataSize  = (uint)fileSize;
             data = new BinaryReader(file).ReadBytes((int)dataSize );

            ParseChunks();
            if (PrepareLoadedData())
                return true;

            Console.WriteLine($"Error loading {description}\n");

            return false;
        }

        bool PrepareLoadedData()
        {
            FileChunk chunk = GetChunk("MVER");
            if (chunk == null)
                return false;

            // Check version
            MVER version = chunk.As<MVER>();
            if (version.Version != 18)
                return false;

            return true;
        }

        public static Dictionary<string, string> InterestingChunks = new()
        {
            {"REVM", "MVER"},
            {"NIAM", "MAIN"},
            {"O2HM", "MH2O"},
            {"KNCM", "MCNK"},
            {"TVCM", "MCVT"},
            {"QLCM", "MCLQ"},
            {"OBFM", "MFBO"},
            {"DHPM", "MPHD"},
            {"DIAM", "MAID"},
            {"OMWM", "MWMO"},
            {"XDMM", "MMDX"},
            {"FDDM", "MDDF"},
            {"FDOM", "MODF"},
        };

        public static bool IsInterestingChunk(string fcc) => InterestingChunks.ContainsKey(fcc);

        void ParseChunks()
        {
            using (BinaryReader reader = new(new MemoryStream(data)))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    string header = new(reader.ReadChars(4));
                    int size = reader.ReadInt32();

                    if (!IsInterestingChunk(header))
                        reader.BaseStream.Position += size;
                    else if (size <= dataSize)
                    {
                        header = InterestingChunks[header];

                        FileChunk chunk = new(reader.ReadBytes(size));
                        chunk.parseSubChunks();
                        chunks.Add(header, chunk);
                    }
                }
            }
        }

        public FileChunk GetChunk(string name)
        {
            var range = chunks.LookupByKey(name);
            if (range != null && range.Count == 1)
                return range[0];

            return null;
        }

        byte[] data;
        uint dataSize;

        public MultiMap<string, FileChunk> chunks = new();
    }

    class FileChunk
    {
        public FileChunk(byte[] data)
        {
            this.data = data;
        }

        public void parseSubChunks()
        {
            using (BinaryReader reader = new(new MemoryStream(data)))
            {
                while (reader.BaseStream.Position + 4 < reader.BaseStream.Length)
                {
                    string header = reader.ReadStringFromChars(4);
                    if (ChunkedFile.IsInterestingChunk(header))
                    {
                        int subsize = reader.ReadInt32();
                        if (subsize < data.Length)
                        {
                            header = ChunkedFile.InterestingChunks[header];
                            FileChunk chunk = new FileChunk(reader.ReadBytes(subsize));
                            chunk.parseSubChunks();
                            if (!subchunks.ContainsKey(header))
                                subchunks[header] = new List<FileChunk>();

                            subchunks[header].Add(chunk);
                        }
                    }
                }
            }
        }

        public FileChunk GetSubChunk(string name)
        {
            var range = subchunks.LookupByKey(name);
            if (range != null)
                return range[0];

            return null;
        }

        public T As<T>() where T : IMapStruct, new()
        {
            T obj = new();
            obj.Read(data);
            return obj;
        }

        uint size;
        byte[] data;

        Dictionary<string, List<FileChunk>> subchunks = new();
    }
}
