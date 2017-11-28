using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace DataExtractor
{
    class WDTFile
    {
        public bool init(string fileName, uint mapID)
        {
            MemoryStream stream = Program.cascHandler.ReadFile(fileName);
            if (stream == null)
                return false;

            string dirname = Program.wmoDirectory + "dir_bin";
            using (var fs = new FileStream(dirname, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, true))
            {
                using (BinaryWriter dirfile = new BinaryWriter(fs))
                {
                    using (BinaryReader reader = new BinaryReader(stream))
                    {
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            string fourcc = reader.ReadStringFromChars(4, true);
                            uint size = reader.ReadUInt32();

                            long nextpos = reader.BaseStream.Position + size;

                            if (fourcc == "MAIN")
                            {
                            }
                            if (fourcc == "MWMO")
                            {
                                // global map objects
                                if (size != 0)
                                {
                                    while (size > 0)
                                    {
                                        string path = reader.ReadCString();

                                        gWmoInstansName.Add(path.GetPlainName());
                                        VmapFile.ExtractSingleWmo(path);

                                        size -= (uint)(path.Length + 1);
                                    }
                                }
                            }
                            else if (fourcc == "MODF")
                            {
                                // global wmo instance data
                                if (size != 0)
                                {
                                    gnWMO = (int)size / 64;

                                    for (int i = 0; i < gnWMO; ++i)
                                    {
                                        int id = reader.ReadInt32();
                                        WMOInstance inst = new WMOInstance(reader, gWmoInstansName[id], mapID, 65, 65, dirfile);
                                    }
                                }
                            }

                            reader.BaseStream.Seek(nextpos, SeekOrigin.Begin);
                        }
                    }

                }
            }
            return true;
        }

        List<string> gWmoInstansName = new List<string>();
        int gnWMO;
    }
}
