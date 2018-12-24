using DataExtractor.Framework.Constants;
using DataExtractor.Framework.GameMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace DataExtractor.Vmap
{
    class Model
    {
        public bool open(string fileName)
        {
            using (var stream = Program.CascHandler.OpenFile(fileName))
            {
                if (stream == null)
                    return false;

                return Read(stream);
            }
        }

        public bool open(uint fileId)
        {
            using (var stream = Program.CascHandler.OpenFile((int)fileId))
            {
                if (stream == null)
                    return false;

                return Read(stream);
            }
        }

        bool Read(Stream stream)
        {
            using (BinaryReader reader = new BinaryReader(stream))
            {
                _unload();

                uint m2start = 0;
                string b;
                long fileLength = reader.BaseStream.Length;
                while (reader.BaseStream.Position + 4 < fileLength && (b = reader.ReadStringFromChars(4)) != "MD20")
                {
                    m2start += 4;
                    if (m2start + Marshal.SizeOf<ModelHeader>() > fileLength)
                        return false;
                }

                reader.BaseStream.Seek(m2start, SeekOrigin.Begin);
                header = reader.Read<ModelHeader>();

                bounds = header.collisionBox;
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
                binaryWriter.WriteVector3(bounds.Lo);//bbox, only needed for WMO currently
                binaryWriter.WriteVector3(bounds.Hi);
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

        public static void Extract(MDDF doodadDef, string ModelInstName, uint mapID, uint originalMapId, BinaryWriter writer, List<ADTOutputCache> dirfileCache)
        {
            if (!File.Exists(Program.WmoDirectory + ModelInstName))
                return;

            using (BinaryReader binaryReader = new BinaryReader(File.Open(Program.WmoDirectory + ModelInstName, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                binaryReader.BaseStream.Seek(8, SeekOrigin.Begin); // get the correct no of vertices
                int nVertices = binaryReader.ReadInt32();
                if (nVertices == 0)
                    return;
            }

            // scale factor - divide by 1024. blizzard devs must be on crack, why not just use a float?
            float sc = doodadDef.Scale / 1024.0f;

            Vector3 position = fixCoords(doodadDef.Position);

            ushort nameSet = 0;// not used for models
            uint uniqueId = VmapFile.GenerateUniqueObjectId(doodadDef.UniqueId, 0);
            uint flags = ModelFlags.M2;
            if (mapID != originalMapId)
                flags |= ModelFlags.ParentSpawn;


            //write mapID, Flags, NameSet, UniqueId, Pos, Rot, Scale, name
            writer.Write(mapID);
            writer.Write(flags);
            writer.Write(nameSet);
            writer.Write(uniqueId);
            writer.WriteVector3(position);
            writer.WriteVector3(doodadDef.Rotation);
            writer.Write(sc);
            writer.Write(ModelInstName.Length);
            writer.WriteString(ModelInstName);

            if (dirfileCache != null)
            {
                ADTOutputCache cacheModelData = new ADTOutputCache();
                cacheModelData.Flags = flags & ~ModelFlags.ParentSpawn;

                MemoryStream stream = new MemoryStream();
                BinaryWriter cacheData = new BinaryWriter(stream);
                cacheData.Write(nameSet);
                cacheData.Write(uniqueId);
                cacheData.WriteVector3(position);
                cacheData.WriteVector3(doodadDef.Rotation);
                cacheData.Write(sc);
                cacheData.Write(ModelInstName.Length);
                cacheData.WriteString(ModelInstName);

                cacheModelData.Data = stream.ToArray();
                dirfileCache.Add(cacheModelData);
            }
        }

        public static void ExtractSet(WMODoodadData doodadData, MODF wmo, bool isGlobalWmo, uint mapID, uint originalMapId, BinaryWriter writer, List<ADTOutputCache> dirfileCache)
        {
            if (wmo.DoodadSet >= doodadData.Sets.Count)
                return;

            Vector3 wmoPosition = new Vector3(wmo.Position.Z, wmo.Position.X, wmo.Position.Y);
            Matrix3 wmoRotation = Matrix3.fromEulerAnglesZYX(MathFunctions.toRadians(wmo.Rotation.Y), MathFunctions.toRadians(wmo.Rotation.X), MathFunctions.toRadians(wmo.Rotation.Z));

            if (isGlobalWmo)
                wmoPosition += new Vector3(533.33333f * 32, 533.33333f * 32, 0.0f);

            ushort doodadId = 0;
            MODS doodadSetData = doodadData.Sets[wmo.DoodadSet];
            using (BinaryReader reader = new BinaryReader(new MemoryStream(doodadData.Paths)))
            {
                foreach (ushort doodadIndex in doodadData.References)
                {
                    if (doodadIndex < doodadSetData.StartIndex ||
                        doodadIndex >= doodadSetData.StartIndex + doodadSetData.Count)
                        continue;

                    MODD doodad = doodadData.Spawns[doodadIndex];

                    reader.BaseStream.Position = doodad.NameIndex;
                    string ModelInstName = reader.ReadCString().GetPlainName();

                    if (ModelInstName.Length > 3)
                    {
                        string extension = ModelInstName.Substring(ModelInstName.Length - 4);
                        if (extension == ".mdx" || extension == ".mdl")
                        {
                            ModelInstName = ModelInstName.Remove(ModelInstName.Length - 2, 2);
                            ModelInstName += "2";
                        }
                    }

                    if (!File.Exists(Program.WmoDirectory + ModelInstName))
                        continue;

                    using (BinaryReader binaryReader = new BinaryReader(File.Open(Program.WmoDirectory + ModelInstName, FileMode.Open, FileAccess.Read, FileShare.Read)))
                    {
                        binaryReader.BaseStream.Seek(8, SeekOrigin.Begin); // get the correct no of vertices
                        int nVertices = binaryReader.ReadInt32();
                        if (nVertices == 0)
                            continue;
                    }
                    ++doodadId;

                    Vector3 position = wmoPosition + (wmoRotation * new Vector3(doodad.Position.X, doodad.Position.Y, doodad.Position.Z));

                    Vector3 rotation;
                    (new Quaternion(doodad.Rotation.X, doodad.Rotation.Y, doodad.Rotation.Z, doodad.Rotation.W).toRotationMatrix() * wmoRotation).toEulerAnglesXYZ(out rotation.Z, out rotation.X, out rotation.Y);

                    rotation.Z = MathFunctions.toDegrees(rotation.Z);
                    rotation.X = MathFunctions.toDegrees(rotation.X);
                    rotation.Y = MathFunctions.toDegrees(rotation.Y);

                    ushort nameSet = 0;     // not used for models
                    uint uniqueId = VmapFile.GenerateUniqueObjectId(wmo.UniqueId, doodadId);
                    uint flags = ModelFlags.M2;
                    if (mapID != originalMapId)
                        flags |= ModelFlags.ParentSpawn;

                    //write mapID, Flags, NameSet, UniqueId, Pos, Rot, Scale, name
                    writer.Write(mapID);
                    writer.Write(flags);
                    writer.Write(nameSet);
                    writer.Write(uniqueId);
                    writer.WriteVector3(position);
                    writer.WriteVector3(rotation);
                    writer.Write(doodad.Scale);
                    writer.Write(ModelInstName.Length);
                    writer.WriteString(ModelInstName);

                    if (dirfileCache != null)
                    {
                        ADTOutputCache cacheModelData = new ADTOutputCache();
                        cacheModelData.Flags = flags & ~ModelFlags.ParentSpawn;

                        MemoryStream stream = new MemoryStream();
                        BinaryWriter cacheData = new BinaryWriter(stream);
                        cacheData.Write(nameSet);
                        cacheData.Write(uniqueId);
                        cacheData.WriteVector3(position);
                        cacheData.WriteVector3(rotation);
                        cacheData.Write(doodad.Scale);
                        cacheData.Write(ModelInstName.Length);
                        cacheData.WriteString(ModelInstName);

                        cacheModelData.Data = stream.ToArray();
                        dirfileCache.Add(cacheModelData);
                    }
                }
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
        AxisAlignedBox bounds;
    }

    struct ModelHeader
    {
        public uint id { get; set; }
        public uint version { get; set; }
        public uint nameLength { get; set; }
        public uint nameOfs { get; set; }
        public uint type { get; set; }
        public uint nGlobalSequences { get; set; }
        public uint ofsGlobalSequences { get; set; }
        public uint nAnimations { get; set; }
        public uint ofsAnimations { get; set; }
        public uint nAnimationLookup { get; set; }
        public uint ofsAnimationLookup { get; set; }
        public uint nBones { get; set; }
        public uint ofsBones { get; set; }
        public uint nKeyBoneLookup { get; set; }
        public uint ofsKeyBoneLookup { get; set; }
        public uint nVertices { get; set; }
        public uint ofsVertices { get; set; }
        public uint nViews { get; set; }
        public uint nColors { get; set; }
        public uint ofsColors { get; set; }
        public uint nTextures { get; set; }
        public uint ofsTextures { get; set; }
        public uint nTransparency { get; set; }
        public uint ofsTransparency { get; set; }
        public uint nTextureanimations { get; set; }
        public uint ofsTextureanimations { get; set; }
        public uint nTexReplace { get; set; }
        public uint ofsTexReplace { get; set; }
        public uint nRenderFlags { get; set; }
        public uint ofsRenderFlags { get; set; }
        public uint nBoneLookupTable { get; set; }
        public uint ofsBoneLookupTable { get; set; }
        public uint nTexLookup { get; set; }
        public uint ofsTexLookup { get; set; }
        public uint nTexUnits { get; set; }
        public uint ofsTexUnits { get; set; }
        public uint nTransLookup { get; set; }
        public uint ofsTransLookup { get; set; }
        public uint nTexAnimLookup { get; set; }
        public uint ofsTexAnimLookup { get; set; }
        public AxisAlignedBox boundingBox { get; set; }
        public float boundingSphereRadius { get; set; }
        public AxisAlignedBox collisionBox { get; set; }
        public float collisionSphereRadius { get; set; }
        public uint nBoundingTriangles { get; set; }
        public uint ofsBoundingTriangles { get; set; }
        public uint nBoundingVertices { get; set; }
        public uint ofsBoundingVertices { get; set; }
        public uint nBoundingNormals { get; set; }
        public uint ofsBoundingNormals { get; set; }
        public uint nAttachments { get; set; }
        public uint ofsAttachments { get; set; }
        public uint nAttachLookup { get; set; }
        public uint ofsAttachLookup { get; set; }
        public uint nAttachments_2 { get; set; }
        public uint ofsAttachments_2 { get; set; }
        public uint nLights { get; set; }
        public uint ofsLights { get; set; }
        public uint nCameras { get; set; }
        public uint ofsCameras { get; set; }
        public uint nCameraLookup { get; set; }
        public uint ofsCameraLookup { get; set; }
        public uint nRibbonEmitters { get; set; }
        public uint ofsRibbonEmitters { get; set; }
        public uint nParticleEmitters { get; set; }
        public uint ofsParticleEmitters { get; set; }
    }
}
