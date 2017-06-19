using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;

namespace ClientPatcher
{
    class Patcher : IDisposable
    {
        public Patcher(string file)
        {
            Initialized = false;
            success = false;

            using (var stream = new MemoryStream(File.ReadAllBytes(file)))
            {
                Binary = file;
                binary = stream.ToArray();

                if (binary != null)
                {
                    Type = GetBinaryType(binary);

                    Initialized = true;
                }
            }
        }

        public void Patch(byte[] bytes, byte[] pattern, long address = 0)
        {
            if (Initialized && (address != 0 || binary.Length >= pattern.Length))
            {
                var offset = pattern == null ? address : SearchOffset(pattern);

                if (offset != 0 && binary.Length >= bytes.Length)
                {
                    try
                    {
                        for (int i = 0; i < bytes.Length; i++)
                            binary[offset + i] = bytes[i];
                    }
                    catch (Exception ex)
                    {
                        throw new NotSupportedException(ex.Message);
                    }
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("! Wrong patch (invalid pattern?)");
            }
        }

        long SearchOffset(byte[] pattern)
        {
            long matches;

            for (long i = 0; i < binary.Length; i++)
            {
                if (pattern.Length > (binary.Length - i))
                    return 0;

                for (matches = 0; matches < pattern.Length; matches++)
                {
                    if ((pattern[matches] != 0) && (binary[i + matches] != pattern[matches]))
                        break;
                }

                if (matches == pattern.Length)
                    return i;
            }

            return 0;
        }

        public void Finish()
        {
            success = true;
        }

        public void Dispose()
        {
            if (File.Exists(Binary))
                File.Delete(Binary);

            if (success)
                File.WriteAllBytes(Binary, binary);

            binary = null;
        }

        public static BinaryTypes GetBinaryType(byte[] data)
        {
            BinaryTypes type = 0u;

            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                var magic = (uint)reader.ReadUInt16();

                // Check MS-DOS magic
                if (magic == 0x5A4D)
                {
                    reader.BaseStream.Seek(0x3C, SeekOrigin.Begin);

                    // Read PE start offset
                    var peOffset = reader.ReadUInt32();

                    reader.BaseStream.Seek(peOffset, SeekOrigin.Begin);

                    var peMagic = reader.ReadUInt32();

                    // Check PE magic
                    if (peMagic != 0x4550)
                        throw new NotSupportedException("Not a PE file!");

                    type = (BinaryTypes)reader.ReadUInt16();
                }
                else
                {
                    reader.BaseStream.Seek(0, SeekOrigin.Begin);

                    type = (BinaryTypes)reader.ReadUInt32();
                }
            }

            return type;
        }

        public static string GetFileChecksum(byte[] data)
        {
            using (var stream = new BufferedStream(new MemoryStream(data), 1200000))
            {
                var sha256 = new SHA256Managed();
                var checksum = sha256.ComputeHash(stream);

                return BitConverter.ToString(checksum).Replace("-", "").ToLower();
            }
        }

        public string Binary { get; set; }
        public bool Initialized { get; private set; }
        public BinaryTypes Type { get; private set; }

        public byte[] binary;
        bool success;
    }

    enum BinaryTypes : uint
    {
        None = 0000000000,
        Pe32 = 0x0000014C,
        Pe64 = 0x00008664,
        Mach32 = 0xFEEDFACE,
        Mach64 = 0xFEEDFACF
    }
}
