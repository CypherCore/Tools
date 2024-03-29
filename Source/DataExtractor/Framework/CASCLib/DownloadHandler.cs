﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DataExtractor.CASCLib
{
    public class DownloadEntry
    {
        public int Index;
        //public byte[] Unk;

        public IEnumerable<KeyValuePair<string, DownloadTag>> Tags;
    }

    public class DownloadTag
    {
        public short Type;
        public BitArray Bits;
    }

    public class DownloadHandler
    {
        private static readonly MD5HashComparer comparer = new();
        private Dictionary<MD5Hash, DownloadEntry> DownloadData = new(comparer);
        private Dictionary<string, DownloadTag> Tags = new();

        public int Count => DownloadData.Count;

        public DownloadHandler(BinaryReader stream)
        {
            stream.Skip(2); // DL

            byte b1 = stream.ReadByte();
            byte b2 = stream.ReadByte();
            byte b3 = stream.ReadByte();

            int numFiles = stream.ReadInt32BE();

            short numTags = stream.ReadInt16BE();

            int numMaskBytes = (numFiles + 7) / 8;

            for (int i = 0; i < numFiles; i++)
            {
                MD5Hash key = stream.Read<MD5Hash>();

                //byte[] unk = stream.ReadBytes(0xA);
                stream.Skip(0xA);

                //var entry = new DownloadEntry() { Index = i, Unk = unk };
                var entry = new DownloadEntry() { Index = i };

                DownloadData.Add(key, entry);
            }

            for (int i = 0; i < numTags; i++)
            {
                DownloadTag tag = new();
                string name = stream.ReadCString();
                tag.Type = stream.ReadInt16BE();

                byte[] bits = stream.ReadBytes(numMaskBytes);

                for (int j = 0; j < numMaskBytes; j++)
                    bits[j] = (byte)((bits[j] * 0x0202020202 & 0x010884422010) % 1023);

                tag.Bits = new BitArray(bits);

                Tags.Add(name, tag);
            }
        }

        public void Dump()
        {
            foreach (var entry in DownloadData)
            {
                if (entry.Value.Tags == null)
                    entry.Value.Tags = Tags.Where(kv => kv.Value.Bits[entry.Value.Index]);
            }
        }

        public DownloadEntry GetEntry(MD5Hash key)
        {
            DownloadData.TryGetValue(key, out DownloadEntry entry);

            if (entry != null && entry.Tags == null)
                entry.Tags = Tags.Where(kv => kv.Value.Bits[entry.Index]);

            return entry;
        }

        public void Clear()
        {
            Tags.Clear();
            Tags = null;
            DownloadData.Clear();
            DownloadData = null;
        }
    }
}
