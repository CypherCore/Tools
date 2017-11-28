using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System;
using System.Threading;
using System.Numerics;

namespace System
{
    public static class Extensions
    {
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

        public static T ReadStruct<T>(this BinaryReader reader) where T : struct
        {
            return ReadStruct<T>(reader, Marshal.SizeOf(typeof(T)));
        }
        public static T ReadStruct<T>(this BinaryReader reader, int size) where T : struct
        {
            byte[] data = reader.ReadBytes(size);
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            T returnObject = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));

            handle.Free();
            return returnObject;
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

        public static string GetPlainName(this string fileName)
        {
            int index = fileName.LastIndexOf('\\');
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
}
