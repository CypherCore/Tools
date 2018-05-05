using System;
using System.IO;
using System.Text;

namespace ClientPatcher
{
    class Patcher : IDisposable
    {
        public Patcher(string file)
        {
            Initialized = false;
            success = true;

            using (var stream = new MemoryStream(File.ReadAllBytes(file)))
            {
                Binary = file;
                binary = stream.ToArray();

                if (binary != null)
                {
                    Type = GetBinaryType(binary);

                    switch (Type)
                    {
                        case BinaryTypes.Pe64:
                            Console.WriteLine("Win64 client...");
                            break;
                        case BinaryTypes.Mach64:
                            Console.WriteLine("Mac client...");
                            break;
                        case BinaryTypes.Pe32:
                        case BinaryTypes.Mach32:
                        default:
                            throw new NotSupportedException("Type: " + Type + " not supported!");
                    }

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
                        Console.WriteLine(ex.Message);
                        success = false;
                    }
                }
                else
                {
                    Console.WriteLine("Could not find offset");
                    success = false;
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("! Wrong patch (invalid pattern?)");
                success = false;
            }
        }

        public void Patch(char[] chars, char[] pattern, long address = 0)
        {
            Patch(Encoding.UTF8.GetBytes(chars), Encoding.UTF8.GetBytes(pattern), address);
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
            if (File.Exists(Binary))
                File.Delete(Binary);

            if (success)
                File.WriteAllBytes(Binary, binary);
        }

        public void Dispose()
        {
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

        public uint GetBuildNumber()
        {
            long offset = SearchOffset(Patterns.Common.BinaryVersion);

            if (offset != 0)
                return uint.Parse(Encoding.UTF8.GetString(binary, (int)offset + 16, 5));

            return 0;
        }

        public string Binary { get; set; }
        bool Initialized { get; set; }
        public BinaryTypes Type { get; private set; }

        byte[] binary;
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
