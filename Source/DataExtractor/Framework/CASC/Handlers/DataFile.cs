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

using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Framework.CASC.Structures;
using System;
using System.Security.Cryptography;

namespace Framework.CASC.Handlers
{
    public class DataFile
    {
        public readonly BinaryReader readStream;

        static object readLock = new object();

        public DataFile(Stream data)
        {
            readStream = new BinaryReader(data);
        }

        public static MemoryStream LoadBLTEEntry(IndexEntry idxEntry, BinaryReader readStream = null, bool downloaded = false)
        {
            lock (readLock)
            {
                if (readStream == null)
                    return null;

                if (!downloaded)
                    readStream.BaseStream.Position = idxEntry.Offset + 30;

                if (readStream.ReadUInt32() != 0x45544C42)
                {
                    Trace.TraceError($"data.{idxEntry.Index:000}: Invalid BLTE signature at 0x{readStream.BaseStream.Position:X8}.");
                    return null;
                }

                var blte = new BLTEEntry();
                var frameHeaderLength = readStream.ReadBEInt32();
                var chunks = 0u;
                var size = 0L;

                if (frameHeaderLength == 0)
                {
                    chunks = 1;
                    size = idxEntry.Size - 38;
                }
                else
                {
                    readStream.BaseStream.Position += 1;
                    chunks = readStream.ReadUInt24();
                }

                blte.Chunks = new BLTEChunk[chunks];

                for (var i = 0; i < chunks; i++)
                {
                    if (frameHeaderLength == 0)
                    {
                        blte.Chunks[i].CompressedSize = size;
                        blte.Chunks[i].UncompressedSize = size - 1;
                    }
                    else
                    {
                        blte.Chunks[i].CompressedSize = readStream.ReadBEInt32();
                        blte.Chunks[i].UncompressedSize = readStream.ReadBEInt32();

                        // Skip MD5 hash
                        readStream.BaseStream.Position += 16;
                    }
                }

                var data = new MemoryStream();

                for (int i = 0; i < chunks; i++)
                {
                    var dataBytes = readStream.ReadBytes((int)blte.Chunks[i].CompressedSize);
                    HandleDataBlock(dataBytes, i, data);
                }

                data.Position = 0;
                return data;
            }
        }

        private static void HandleDataBlock(byte[] data, int index, MemoryStream stream)
        {
            switch (data[0])
            {
                case 0x45: // E (encrypted)
                    byte[] decrypted = Decrypt(data, index);
                    if (decrypted == null)
                        return;

                    HandleDataBlock(decrypted, index, stream);
                    break;
                case 0x46: // F (frame, recursive)
                    throw new Exception("DecoderFrame not implemented");
                case 0x4E: // N (not compressed)
                    stream.Write(data, 1, data.Length - 1);
                    break;
                case 0x5A: // Z (zlib compressed)
                    using (var decompressed = new MemoryStream())
                    {
                        using (var inflate = new DeflateStream(new MemoryStream(data, 3, data.Length - 3), CompressionMode.Decompress))
                            inflate.CopyTo(decompressed);

                        var inflateData = decompressed.ToArray();
                        stream.Write(inflateData, 0, inflateData.Length);
                    }
                    break;
                default:
                    throw new Exception(string.Format("unknown BLTE block type {0} (0x{1:X2})!", (char)data[0], data[0]));
            }
        }

        private static byte[] Decrypt(byte[] data, int index)
        {
            byte keyNameSize = data[1];

            if (keyNameSize == 0 || keyNameSize != 8)
                throw new Exception("keyNameSize == 0 || keyNameSize != 8");

            byte[] keyNameBytes = new byte[keyNameSize];
            Array.Copy(data, 2, keyNameBytes, 0, keyNameSize);

            ulong keyName = BitConverter.ToUInt64(keyNameBytes, 0);

            byte IVSize = data[keyNameSize + 2];

            if (IVSize != 4 || IVSize > 0x10)
                throw new Exception("IVSize != 4 || IVSize > 0x10");

            byte[] IVpart = new byte[IVSize];
            Array.Copy(data, keyNameSize + 3, IVpart, 0, IVSize);

            if (data.Length < IVSize + keyNameSize + 4)
                throw new Exception("data.Length < IVSize + keyNameSize + 4");

            int dataOffset = keyNameSize + IVSize + 3;

            byte encType = data[dataOffset];

            if (encType != 0x53 && encType != 0x41) // 'S' or 'A'
                throw new Exception("encType != ENCRYPTION_SALSA20 && encType != ENCRYPTION_ARC4");

            dataOffset++;

            // expand to 8 bytes
            byte[] IV = new byte[8];
            Array.Copy(IVpart, IV, IVpart.Length);

            // magic
            for (int shift = 0, i = 0; i < sizeof(int); shift += 8, i++)
            {
                IV[i] ^= (byte)((index >> shift) & 0xFF);
            }

            byte[] key = KeyService.GetKey(keyName);

            if (key == null)
                return null;

            if (encType == 0x53)
            {
                ICryptoTransform decryptor = KeyService.SalsaInstance.CreateDecryptor(key, IV);

                return decryptor.TransformFinalBlock(data, dataOffset, data.Length - dataOffset);
            }
            else
            {
                // ARC4 ?
                throw new Exception("encType ENCRYPTION_ARC4 not implemented");
            }
        }
    }
}
