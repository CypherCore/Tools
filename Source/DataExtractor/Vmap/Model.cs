using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using Framework.GameMath;
using Framework.Constants;

namespace DataExtractor.Vmap
{
    class Model
    {
        public bool open(string fileName)
        {
            using (var stream = Program.cascHandler.ReadFile(fileName))
            {
                if (stream == null)
                    return false;

                return Read(stream);
            }
        }

        public bool open(uint fileId)
        {
            using (var stream = Program.cascHandler.ReadFile((int)fileId))
            {
                if (stream == null)
                    return false;

                return Read(stream);
            }
        }

        bool Read(MemoryStream stream)
        {
            using (BinaryReader reader = new BinaryReader(stream))
            {
                _unload();

                uint m2start = 0;
                string b;
                while (reader.BaseStream.Position + 4 < reader.BaseStream.Length && (b = reader.ReadStringFromChars(4)) != "MD20")
                {
                    m2start += 4;
                    if (m2start + Marshal.SizeOf<ModelHeader>() > reader.BaseStream.Length)
                        return false;
                }

                reader.BaseStream.Seek(m2start, SeekOrigin.Begin);
                header = reader.ReadStruct<ModelHeader>();
                string gfdg = Encoding.UTF8.GetString(header.id);
                if (header.nBoundingTriangles > 0)
                {
                    reader.BaseStream.Seek(m2start + header.ofsBoundingVertices, SeekOrigin.Begin);
                    vertices = new Vector3[header.nBoundingVertices];
                    for (var i = 0; i < header.nBoundingVertices; ++i)
                        vertices[i] = reader.Read<Vector3>();

                    for (uint i = 0; i < header.nBoundingVertices; i++)
                        vertices[i] = fixCoordSystem(vertices[i]);

                    reader.BaseStream.Seek(m2start + header.ofsBoundingTriangles, SeekOrigin.Begin);
                    indices = new ushort[header.nBoundingTriangles];
                    for (var i = 0; i < header.nBoundingTriangles; ++i)
                        indices[i] = reader.ReadUInt16();
                }
                else
                    return false;
            }
            return true;
        }

        void _unload()
        {
            vertices = null;
            indices = null;
        }

        public bool ConvertToVMAPModel(string outfilename)
        {
            int[] N = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(outfilename, FileMode.Create, FileAccess.Write)))
            {
                binaryWriter.WriteCString(SharedConst.RAW_VMAP_MAGIC);

                uint nVertices = header.nBoundingVertices;
                binaryWriter.Write(nVertices);

                uint nofgroups = 1;
                binaryWriter.Write(nofgroups);
                for (var i = 0; i < 3; ++i)
                    binaryWriter.Write(N[i]);// rootwmoid, flags, groupid
                for (var i = 0; i < 3 * 2; ++i)
                    binaryWriter.Write(N[i]);//bbox, only needed for WMO currently
                binaryWriter.Write(N[0]);// liquidflags
                binaryWriter.WriteString("GRP ");

                uint branches = 1;
                int wsize;
                wsize = (int)(4 + 4 * branches);
                binaryWriter.Write(wsize);
                binaryWriter.Write(branches);

                uint nIndexes = header.nBoundingTriangles;
                binaryWriter.Write(nIndexes);
                binaryWriter.WriteString("INDX");
                wsize = (int)(sizeof(uint) + sizeof(ushort) * nIndexes);
                binaryWriter.Write(wsize);
                binaryWriter.Write(nIndexes);
                if (nIndexes > 0)
                {
                    for (uint i = 0; i < nIndexes; ++i)
                    {
                        if ((i % 3) - 1 == 0 && i + 1 < nIndexes)
                        {
                            ushort tmp = indices[i];
                            indices[i] = indices[i + 1];
                            indices[i + 1] = tmp;
                        }
                    }
                    for (var i = 0; i < nIndexes; ++i)
                        binaryWriter.Write(indices[i]);
                }

                binaryWriter.WriteString("VERT");
                wsize = (int)(sizeof(int) + sizeof(float) * 3 * nVertices);
                binaryWriter.Write(wsize);
                binaryWriter.Write(nVertices);
                if (nVertices > 0)
                {
                    for (uint vpos = 0; vpos < nVertices; ++vpos)
                    {
                        float tmp = vertices[vpos].Y;
                        vertices[vpos].Y = -vertices[vpos].Z;
                        vertices[vpos].Z = tmp;
                    }

                    for (var i = 0; i < nVertices; ++i)
                        binaryWriter.WriteVector3(vertices[i]);
                }
            }

            return true;
        }

        public static void Extract(MDDF doodadDef, string ModelInstName, uint mapID, uint tileX, uint tileY, uint originalMapId, BinaryWriter writer, List<ADTOutputCache> dirfileCache)
        {
            // scale factor - divide by 1024. blizzard devs must be on crack, why not just use a float?
            float sc = doodadDef.Scale / 1024.0f;

            if (!File.Exists(Program.wmoDirectory + ModelInstName))
                return;

            using (BinaryReader binaryReader = new BinaryReader(File.Open(Program.wmoDirectory + ModelInstName, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                binaryReader.BaseStream.Seek(8, SeekOrigin.Begin); // get the correct no of vertices
                int nVertices = binaryReader.ReadInt32();
                if (nVertices == 0)
                    return;
            }

            Vector3 position = fixCoords(doodadDef.Position);

            ushort nameSet = 0;// not used for models
            uint tcflags = ModelFlags.M2;
            if (tileX == 65 && tileY == 65)
                tcflags |= ModelFlags.WorldSpawn;
            if (mapID != originalMapId)
                tcflags |= ModelFlags.ParentSpawn;


            //write mapID, tileX, tileY, Flags, NameSet, UniqueId, Pos, Rot, Scale, name
            writer.Write(mapID);
            writer.Write(tileX);
            writer.Write(tileY);
            writer.Write(tcflags);
            writer.Write(nameSet);
            writer.Write(doodadDef.UniqueId);
            writer.WriteVector3(position);
            writer.WriteVector3(doodadDef.Rotation);
            writer.Write(sc);
            writer.Write(ModelInstName.Length);
            writer.WriteString(ModelInstName);

            if (dirfileCache != null)
            {
                ADTOutputCache cacheModelData = new ADTOutputCache();
                cacheModelData.Flags = tcflags & ~ModelFlags.ParentSpawn;

                MemoryStream stream = new MemoryStream();
                BinaryWriter cacheData = new BinaryWriter(stream);
                cacheData.Write(nameSet);
                cacheData.Write(doodadDef.UniqueId);
                cacheData.WriteVector3(position);
                cacheData.WriteVector3(doodadDef.Rotation);
                cacheData.Write(sc);
                cacheData.Write(ModelInstName.Length);
                cacheData.WriteString(ModelInstName);

                cacheModelData.Data = stream.ToArray();
                dirfileCache.Add(cacheModelData);
            }
        }

        Vector3 fixCoordSystem(Vector3 v)
        {
            return new Vector3(v.X, v.Z, -v.Y);
        }

        static Vector3 fixCoords(Vector3 v) { return new Vector3(v.Z, v.X, v.Y); }

        ModelHeader header;
        Vector3[] vertices;
        ushort[] indices;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ModelHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] id;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] version;
        public uint nameLength;
        public uint nameOfs;
        public uint type;
        public uint nGlobalSequences;
        public uint ofsGlobalSequences;
        public uint nAnimations;
        public uint ofsAnimations;
        public uint nAnimationLookup;
        public uint ofsAnimationLookup;
        public uint nBones;
        public uint ofsBones;
        public uint nKeyBoneLookup;
        public uint ofsKeyBoneLookup;
        public uint nVertices;
        public uint ofsVertices;
        public uint nViews;
        public uint nColors;
        public uint ofsColors;
        public uint nTextures;
        public uint ofsTextures;
        public uint nTransparency;
        public uint ofsTransparency;
        public uint nTextureanimations;
        public uint ofsTextureanimations;
        public uint nTexReplace;
        public uint ofsTexReplace;
        public uint nRenderFlags;
        public uint ofsRenderFlags;
        public uint nBoneLookupTable;
        public uint ofsBoneLookupTable;
        public uint nTexLookup;
        public uint ofsTexLookup;
        public uint nTexUnits;
        public uint ofsTexUnits;
        public uint nTransLookup;
        public uint ofsTransLookup;
        public uint nTexAnimLookup;
        public uint ofsTexAnimLookup;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
        public float[] floats;
        public uint nBoundingTriangles;
        public uint ofsBoundingTriangles;
        public uint nBoundingVertices;
        public uint ofsBoundingVertices;
        public uint nBoundingNormals;
        public uint ofsBoundingNormals;
        public uint nAttachments;
        public uint ofsAttachments;
        public uint nAttachLookup;
        public uint ofsAttachLookup;
        public uint nAttachments_2;
        public uint ofsAttachments_2;
        public uint nLights;
        public uint ofsLights;
        public uint nCameras;
        public uint ofsCameras;
        public uint nCameraLookup;
        public uint ofsCameraLookup;
        public uint nRibbonEmitters;
        public uint ofsRibbonEmitters;
        public uint nParticleEmitters;
        public uint ofsParticleEmitters;
    }
}
