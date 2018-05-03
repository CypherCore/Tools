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

using Framework.GameMath;
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
            byte[] result = reader.ReadBytes(FastStruct<T>.Size);

            return FastStruct<T>.ArrayToStructure(result);
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

        public static int ReadInt32(this BinaryReader br, int byteCount = 0)
        {
            if (byteCount == 0)
                return br.ReadInt32();

            byte[] b = new byte[sizeof(int)];
            for (int i = 0; i < byteCount; i++)
                b[i] = br.ReadByte();

            return BitConverter.ToInt32(b, 0);
        }

        public static uint ReadUInt32(this BinaryReader br, int byteCount = 0)
        {
            if (byteCount == 0)
                return br.ReadUInt32();

            byte[] b = new byte[sizeof(uint)];
            for (int i = 0; i < byteCount; i++)
                b[i] = br.ReadByte();

            return BitConverter.ToUInt32(b, 0);
        }

        public static long ReadInt64(this BinaryReader br, int byteCount = 0)
        {
            if (byteCount == 0)
                return br.ReadInt64();

            byte[] b = new byte[sizeof(long)];
            for (int i = 0; i < byteCount; i++)
                b[i] = br.ReadByte();

            return BitConverter.ToInt64(b, 0);
        }

        public static ulong ReadUInt64(this BinaryReader br, int byteCount = 0)
        {
            if (byteCount == 0)
                return br.ReadUInt64();

            byte[] b = new byte[sizeof(ulong)];
            for (int i = 0; i < byteCount; i++)
                b[i] = br.ReadByte();

            return BitConverter.ToUInt64(b, 0);
        }
        #endregion

        public static Func<object, object> CompileGetter(this FieldInfo field)
        {
            string methodName = field.ReflectedType.FullName + ".get_" + field.Name;
            DynamicMethod setterMethod = new DynamicMethod(methodName, typeof(object), new[] { typeof(object) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();
            if (field.IsStatic)
            {
                gen.Emit(OpCodes.Ldsfld, field);
                gen.Emit(field.FieldType.IsClass ? OpCodes.Castclass : OpCodes.Box, field.FieldType);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Castclass, field.DeclaringType);
                gen.Emit(OpCodes.Ldfld, field);
                gen.Emit(field.FieldType.IsClass ? OpCodes.Castclass : OpCodes.Box, field.FieldType);
            }
            gen.Emit(OpCodes.Ret);
            return (Func<object, object>)setterMethod.CreateDelegate(typeof(Func<object, object>));
        }
        public static Action<object, object> CompileSetter(this FieldInfo field)
        {
            string methodName = field.ReflectedType.FullName + ".set_" + field.Name;
            DynamicMethod setterMethod = new DynamicMethod(methodName, null, new[] { typeof(object), typeof(object) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();
            if (field.IsStatic)
            {
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(field.FieldType.IsClass ? OpCodes.Castclass : OpCodes.Unbox_Any, field.FieldType);
                gen.Emit(OpCodes.Stsfld, field);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Castclass, field.DeclaringType);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(field.FieldType.IsClass ? OpCodes.Castclass : OpCodes.Unbox_Any, field.FieldType);
                gen.Emit(OpCodes.Stfld, field);
            }
            gen.Emit(OpCodes.Ret);
            return (Action<object, object>)setterMethod.CreateDelegate(typeof(Action<object, object>));
        }

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
    }

    public static class FastStruct<T> where T : struct
    {
        private delegate T LoadFromByteRefDelegate(ref byte source);
        private delegate void CopyMemoryDelegate(ref T dest, ref byte src, int count);

        private readonly static LoadFromByteRefDelegate LoadFromByteRef = BuildLoadFromByteRefMethod();
        private readonly static CopyMemoryDelegate CopyMemory = BuildCopyMemoryMethod();

        public static readonly int Size = Marshal.SizeOf<T>();

        private static LoadFromByteRefDelegate BuildLoadFromByteRefMethod()
        {
            var methodLoadFromByteRef = new DynamicMethod("LoadFromByteRef<" + typeof(T).FullName + ">",
                typeof(T), new[] { typeof(byte).MakeByRefType() }, typeof(FastStruct<T>));

            ILGenerator generator = methodLoadFromByteRef.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldobj, typeof(T));
            generator.Emit(OpCodes.Ret);

            return (LoadFromByteRefDelegate)methodLoadFromByteRef.CreateDelegate(typeof(LoadFromByteRefDelegate));
        }

        private static CopyMemoryDelegate BuildCopyMemoryMethod()
        {
            var methodCopyMemory = new DynamicMethod("CopyMemory<" + typeof(T).FullName + ">",
                typeof(void), new[] { typeof(T).MakeByRefType(), typeof(byte).MakeByRefType(), typeof(int) }, typeof(FastStruct<T>));

            ILGenerator generator = methodCopyMemory.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldarg_2);
            generator.Emit(OpCodes.Cpblk);
            generator.Emit(OpCodes.Ret);

            return (CopyMemoryDelegate)methodCopyMemory.CreateDelegate(typeof(CopyMemoryDelegate));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ArrayToStructure(byte[] src)
        {
            return LoadFromByteRef(ref src[0]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ArrayToStructure(byte[] src, int offset)
        {
            return LoadFromByteRef(ref src[offset]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ArrayToStructure(ref byte src)
        {
            return LoadFromByteRef(ref src);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] ReadArray(byte[] source)
        {
            T[] buffer = new T[source.Length / Size];

            if (source.Length > 0)
                CopyMemory(ref buffer[0], ref source[0], source.Length);

            return buffer;
        }
    }
}
