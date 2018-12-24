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

using DataExtractor.Framework.Constants;
using DataExtractor.Framework.GameMath;
using System;
using System.IO;

namespace DataExtractor.Framework.Collision
{
    public class ModelSpawn
    {
        public ModelSpawn() { }
        public ModelSpawn(ModelSpawn spawn)
        {
            flags = spawn.flags;
            adtId = spawn.adtId;
            ID = spawn.ID;
            iPos = spawn.iPos;
            iRot = spawn.iRot;
            iScale = spawn.iScale;
            iBound = spawn.iBound;
            name = spawn.name;
        }

        public AxisAlignedBox GetBounds() { return iBound; }

        public static bool ReadFromFile(BinaryReader reader, out ModelSpawn spawn)
        {
            spawn = new ModelSpawn();
            spawn.flags = reader.ReadUInt32();
            spawn.adtId = reader.ReadUInt16();
            spawn.ID = reader.ReadUInt32();
            spawn.iPos = reader.ReadVector3();
            spawn.iRot = reader.ReadVector3();
            spawn.iScale = reader.ReadSingle();

            if ((spawn.flags & ModelFlags.HasBound) != 0) // only WMOs have bound in MPQ, only available after computation
            {
                Vector3 bLow = reader.ReadVector3();
                Vector3 bHigh = reader.ReadVector3();
                spawn.iBound = new AxisAlignedBox(bLow, bHigh);
            }

            int nameLen = reader.ReadInt32();
            spawn.name = reader.ReadString(nameLen);
            return true;
        }

        public static void WriteToFile(BinaryWriter writer, ModelSpawn spawn)
        {
            writer.Write(spawn.flags);
            writer.Write(spawn.adtId);
            writer.Write(spawn.ID);
            writer.WriteVector3(spawn.iPos);
            writer.WriteVector3(spawn.iRot);
            writer.Write(spawn.iScale);

            if ((spawn.flags & ModelFlags.HasBound) != 0) // only WMOs have bound in MPQ, only available after computation
            {
                writer.WriteVector3(spawn.iBound.Lo);
                writer.WriteVector3(spawn.iBound.Hi);
            }

            writer.Write(spawn.name.Length);
            writer.WriteString(spawn.name);
        }

        //mapID, tileX, tileY, Flags, ID, Pos, Rot, Scale, Bound_lo, Bound_hi, name
        public uint flags;
        public ushort adtId;
        public uint ID;
        public Vector3 iPos;
        public Vector3 iRot;
        public float iScale;
        public AxisAlignedBox iBound;
        public string name;
    }

    public class ModelInstance : ModelSpawn
    {
        public ModelInstance(ModelSpawn spawn, WorldModel model) : base(spawn)
        {
            iModel = model;
            iInvRot = Matrix3.fromEulerAnglesZYX(MathF.PI * iRot.Y / 180.0f, MathF.PI * iRot.X / 180.0f, MathF.PI * iRot.Z / 180.0f).inverse();
            iInvScale = 1.0f / iScale;
        }

        public void SetUnloaded() { iModel = null; }
        public WorldModel GetWorldModel() { return iModel; }

        Matrix3 iInvRot;
        float iInvScale;
        WorldModel iModel;
    }
}
