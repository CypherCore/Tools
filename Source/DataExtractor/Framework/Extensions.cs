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

using DataExtractor.Framework.GameMath;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace System
{
    public static class Extensions
    {
        public static bool Empty<TValue>(this ICollection<TValue> collection)
        {
            return collection.Count == 0;
        }

        public static bool Empty<Tkey, TValue>(this IDictionary<Tkey, TValue> dictionary)
        {
            return dictionary.Count == 0;
        }

        public static TValue LookupByKey<TKey, TValue>(this IDictionary<TKey, TValue> dict, object key)
        {
            TValue val;
            TKey newkey = (TKey)Convert.ChangeType(key, typeof(TKey));
            return dict.TryGetValue(newkey, out val) ? val : default(TValue);
        }
        public static TValue LookupByKey<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
        {
            TValue val;
            return dict.TryGetValue(key, out val) ? val : default(TValue);
        }

        #region BinaryReader
        public static string ReadCString(this BinaryReader reader)
        {
            byte num;
            List<byte> temp = new List<byte>();

            while ((num = reader.ReadByte()) != 0)
                temp.Add(num);

            return Encoding.UTF8.GetString(temp.ToArray());
        }
        public static string ReadString(this BinaryReader reader, int count)
        {
            return Encoding.UTF8.GetString(reader.ReadBytes(count));
        }
        public static string ReadStringFromChars(this BinaryReader reader, int count, bool reverseString = false)
        {
            byte[] values = new byte[count];
            if (reverseString)
            {
                for (var i = count - 1; i >= 0; --i)
                    values[i] = reader.ReadByte();
            }
            else
            {
                for (var i = 0; i < count; ++i)
                    values[i] = reader.ReadByte();
            }

            return Encoding.UTF8.GetString(values);
        }

        public static T[] ReadArray<T>(this BinaryReader reader, uint size) where T : struct
        {
            int numBytes = Unsafe.SizeOf<T>() * (int)size;

            byte[] source = reader.ReadBytes(numBytes);

            T[] result = new T[source.Length / Unsafe.SizeOf<T>()];

            if (source.Length > 0)
            {
                unsafe
                {
                    Unsafe.CopyBlockUnaligned(Unsafe.AsPointer(ref result[0]), Unsafe.AsPointer(ref source[0]), (uint)source.Length);
                }
            }

            return result;
        }
        public static T ReadStruct<T>(this BinaryReader reader, int size = 0) where T : struct
        {
            byte[] data = reader.ReadBytes(Marshal.SizeOf(typeof(T)));
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            T returnObject = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));

            handle.Free();
            return returnObject;
        }
        public static T Read<T>(this BinaryReader reader) where T : struct
        {
            byte[] result = reader.ReadBytes(Unsafe.SizeOf<T>());

            return Unsafe.ReadUnaligned<T>(ref result[0]);
        }
        public static Vector3 ReadVector3(this BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
        #endregion

        #region BinaryWriter
        public static void WriteString(this BinaryWriter writer, string str)
        {
            writer.Write(Encoding.UTF8.GetBytes(str));
        }
        public static void WriteCString(this BinaryWriter writer, string str)
        {
            writer.Write(Encoding.UTF8.GetBytes(str));
            writer.Write((byte)0);
        }
        public static void WriteStruct<T>(this BinaryWriter writer, T obj) where T : struct
        {
            int length = Marshal.SizeOf(obj);
            IntPtr ptr = Marshal.AllocHGlobal(length);
            byte[] myBuffer = new byte[length];

            Marshal.StructureToPtr(obj, ptr, true);
            Marshal.Copy(ptr, myBuffer, 0, length);
            Marshal.FreeHGlobal(ptr);

            writer.Write(myBuffer);
        }
        public static void WriteVector3(this BinaryWriter writer, Vector3 vector)
        {
            writer.Write(vector.X);
            writer.Write(vector.Y);
            writer.Write(vector.Z);
        }
        #endregion

        public static string GetPlainName(this string fileName, int index = -1)
        {
            if (index == -1)
                index = fileName.LastIndexOf('\\');

            if (index != -1)
                fileName = fileName.Substring(index + 1);

            fileName = fileName.FixNameCase();
            fileName = fileName.Replace(' ', '_');

            return fileName;
        }
        public static string FixNameCase(this string name)
        {
            char[] ptr = name.ToCharArray();

            int i = name.Length - 1;
            //extension in lowercase
            for (; ptr[i] != '.'; --i)
                ptr[i] = char.ToLower(ptr[i]);

            for (; i >= 0; --i)
            {
                if (i > 0 && ptr[i] >= 'A' && ptr[i] <= 'Z' && char.IsLetter(ptr[i - 1]))
                    ptr[i] = char.ToLower(ptr[i]);
                else if ((i == 0 || !Char.IsLetter(ptr[i - 1])) && ptr[i] >= 'a' && ptr[i] <= 'z')
                    ptr[i] = char.ToUpper(ptr[i]);
            }

            return new string(ptr);
        }
        public static bool Compare(this byte[] b, byte[] b2)
        {
            for (int i = 0; i < b2.Length; i++)
                if (b[i] != b2[i])
                    return false;

            return true;
        }
    }
}
