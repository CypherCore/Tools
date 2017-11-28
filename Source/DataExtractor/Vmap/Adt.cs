using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace DataExtractor
{
    class ADTFile
    {
        public bool init(string fileName, uint map_num, uint tileX, uint tileY)
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

                            if (fourcc == "MCIN")
                            {
                            }
                            else if (fourcc == "MTEX")
                            {
                            }
                            else if (fourcc == "MMDX")
                            {
                                if (size != 0)
                                {
                                    while (size > 0)
                                    {
                                        string path = reader.ReadCString();

                                        ModelInstanceNames.Add(path.GetPlainName());
                                        VmapFile.ExtractSingleModel(path);

                                        size -= (uint)(path.Length + 1);
                                    }
                                }
                            }
                            else if (fourcc == "MWMO")
                            {
                                if (size != 0)
                                {
                                    while (size > 0)
                                    {
                                        string path = reader.ReadCString();

                                        WmoInstanceNames.Add(path.GetPlainName());
                                        VmapFile.ExtractSingleWmo(path);

                                        size -= (uint)(path.Length + 1);
                                    }
                                }
                            }
                            //======================
                            else if (fourcc == "MDDF")
                            {
                                if (size != 0)
                                {
                                    nMDX = (int)size / 36;
                                    for (int i = 0; i < nMDX; ++i)
                                    {
                                        int id = reader.ReadInt32();
                                        ModelInstance inst = new ModelInstance(reader, ModelInstanceNames[id], map_num, tileX, tileY, dirfile);
                                    }

                                    if (ModelInstanceNames.Contains("6Du_Highmaulraid_Arena_Elevator.m2"))
                                    {

                                    }


                                    ModelInstanceNames.Clear();
                                }
                            }
                            else if (fourcc == "MODF")
                            {
                                if (size != 0)
                                {
                                    nWMO = (int)size / 64;
                                    for (int i = 0; i < nWMO; ++i)
                                    {
                                        int id = reader.ReadInt32();
                                        WMOInstance inst = new WMOInstance(reader, WmoInstanceNames[id], map_num, tileX, tileY, dirfile);
                                    }

                                    WmoInstanceNames.Clear();
                                }
                            }

                            //======================
                            reader.BaseStream.Seek(nextpos, SeekOrigin.Begin);
                        }
                    }
                }
            }

            return true;
        }

        string Adtfilename;
        int nWMO;
        int nMDX;
        List<string> WmoInstanceNames = new List<string>();
        List<string> ModelInstanceNames = new List<string>();
    }
}
