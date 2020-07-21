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
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DataExtractor.CASCLib;

namespace DataExtractor
{
    class ChunkedFile
    {
        public bool loadFile(CASCHandler cascHandler, string fileName)
        {
            var file = cascHandler.OpenFile(fileName);
            if (file == null)
                return false;

            var fileSize = file.Length;
            if (fileSize == 0xFFFFFFFF)
                return false;

            data_size = (uint)fileSize;
            _data = new BinaryReader(file).ReadBytes((int)data_size);

            parseChunks();
            if (prepareLoadedData())
                return true;

            Console.WriteLine($"Error loading {fileName}\n");

            return false;
        }

        public bool loadFile(CASCHandler cascHandler, uint fileDataId, string description)
        {
            var file = cascHandler.OpenFile((int)fileDataId);
            if (file == null)
                return false;

            var fileSize = file.Length;
            if (fileSize == 0xFFFFFFFF)
                return false;

            data_size = (uint)fileSize;
             _data = new BinaryReader(file).ReadBytes((int)data_size);

            parseChunks();
            if (prepareLoadedData())
                return true;

            Console.WriteLine($"Error loading {description}\n");

            return false;
        }

        bool prepareLoadedData()
        {
            FileChunk chunk = GetChunk("MVER");
            if (chunk == null)
                return false;

            // Check version
            file_MVER version = chunk.As<file_MVER>();
            if (version.fourcc != 0x4d564552)
                return false;

            if (version.ver != 18)
                return false;

            return true;
        }

        public static Dictionary<string, string> InterestingChunks = new Dictionary<string, string>()
        {
            {"REVM", "MVER"},
            {"NIAM", "MAIN"},
            {"O2HM", "MH2O"},
            {"KNCM", "MCNK"},
            {"TVCM", "MCVT"},
            {"OMWM", "MWMO"},
            {"QLCM", "MCLQ"},
            {"OBFM", "MFBO"},
            {"DHPM", "MPHD"},
            {"DIAM", "MAID"}
        };

        public static bool IsInterestingChunk(string fcc)
        {
            return InterestingChunks.ContainsKey(fcc);
        }

        void parseChunks()
        {
            int index = 0;
            while (index <= _data.Length - 8)
            {
                string header = Encoding.UTF8.GetString(_data, index, 4);
                if (IsInterestingChunk(header))
                {
                    uint size = BitConverter.ToUInt32(_data, index + 4);
                    if (size <= data_size)
                    {
                        header = InterestingChunks[header];
                        FileChunk chunk = new FileChunk(_data, index, size);
                        chunk.parseSubChunks();

                        chunks.Add(header, chunk);
                    }

                    index += (int)size + 8;
                }
                else
                    index++;
            }
        }

        public FileChunk GetChunk(string name)
        {
            var range = chunks.LookupByKey(name);
            if (range != null && range.Count == 1)
                return range[0];

            return null;
        }

        uint GetDataSize() { return data_size; }

        byte[] _data;
        uint data_size;

        public MultiMap<string, FileChunk> chunks = new MultiMap<string, FileChunk>();
    }

    class FileChunk
    {
        public FileChunk(byte[] data, int index, uint size)
        {
            _data = new byte[size + 8];
            Buffer.BlockCopy(data, index, _data, 0, _data.Length);
            _size = size;
        }

        public void parseSubChunks()
        {
            int index = 8; // skip self
            while (index > 0 && index + 8 <= _data.Length)
            {
                string header = Encoding.UTF8.GetString(_data, index, 4);
                if (ChunkedFile.IsInterestingChunk(header))
                {
                    uint subsize = BitConverter.ToUInt32(_data, index + 4);
                    if (subsize < _size)
                    {
                        header = ChunkedFile.InterestingChunks[header];
                        FileChunk chunk = new FileChunk(_data, index, subsize);
                        chunk.parseSubChunks();
                        if (!subchunks.ContainsKey(header))
                            subchunks[header] = new List<FileChunk>();

                        subchunks[header].Add(chunk);
                    }

                    index += (int)(subsize + 8);
                }
                else
                    index += 4;
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
            T obj = new T();
            obj.Read(_data);
            return obj;
        }

        byte[] _data;
        uint _size;

        Dictionary<string, List<FileChunk>> subchunks = new Dictionary<string, List<FileChunk>>();
    }
}
