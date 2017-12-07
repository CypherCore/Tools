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

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Framework.ClientReader
{
    public class DB6Reader
    {
        internal static Dictionary<uint, T> Read<T>(MemoryStream memoryStream, DB6Meta meta) where T : new()
        {
            Dictionary<uint, T> storage = new Dictionary<uint, T>();

            //First lets load field Info
            var fields = typeof(T).GetFields();
            DBClientHelper[] fieldsInfo = new DBClientHelper[fields.Length];
            for (var i = 0; i < fields.Length; ++i)
                fieldsInfo[i] = new DBClientHelper(fields[i]);

            using (var fileReader = new BinaryReader(memoryStream))
            {
                _header = ReadHeader(fileReader);
                var data = LoadData(fileReader);

                int commonDataFieldIndex = 0;
                foreach (var pair in data)
                {
                    var dataReader = new DB6BinaryReader(pair.Value);
                    var obj = new T();

                    int fieldIndex = 0;
                    for (var x = 0; x < _header.FieldCount; ++x)
                    {
                        int arrayLength = meta.ArraySizes[x];
                        if (arrayLength > 1)
                        {
                            for (var z = 0; z < arrayLength; ++z)
                            {
                                var fieldInfo = fieldsInfo[fieldIndex++];
                                if (fieldInfo.IsArray)
                                {
                                    //Field is Array
                                    Array array = (Array)fieldInfo.Getter(obj);
                                    for (var y = 0; y < array.Length; ++y)
                                        SetArrayValue(array, y, fieldInfo, dataReader, x);

                                    arrayLength -= array.Length;
                                }
                                else
                                {
                                    SetValue(obj, fieldInfo, dataReader, x);
                                }
                            }
                        }
                        else
                        {
                            var fieldInfo = fieldsInfo[fieldIndex++];
                            if (fieldInfo.IsArray)
                            {
                                Array array = (Array)fieldInfo.Getter(obj);
                                for (var y = 0; y < array.Length; ++y)
                                    SetArrayValue(array, y, fieldInfo, dataReader, x + y);

                                x += array.Length - 1;
                            }
                            else
                                SetValue(obj, fieldInfo, dataReader, x);
                        }
                    }

                    commonDataFieldIndex = fieldIndex;

                    storage.Add((uint)pair.Key, obj);
                }

                //Get DB field Index
                uint index = 0;
                for (uint i = 0; i < _header.FieldCount && i < _header.IndexField; ++i)
                    index += meta.ArraySizes[i];

                ReadCommonData(commonDataFieldIndex, storage, meta, fieldsInfo);
            }

            return storage;
        }

        static void ReadCommonData<T>(int fieldIndex, Dictionary<uint, T> storage, DB6Meta meta, DBClientHelper[] helper) where T : new()
        {
            for (int x = (int)_header.FieldCount; x < _header.TotalFieldCount; ++x)
            {
                var fieldInfo = helper[fieldIndex++];
                int arrayLength = meta.ArraySizes[x];

                foreach (var recordId in _commandData[x])
                {
                    var dataReader = new DB6BinaryReader(recordId.Value);
                    var record = storage.LookupByKey(recordId.Key);

                    if (arrayLength > 1)
                    {
                        for (var z = 0; z < arrayLength; ++z)
                        {
                            if (fieldInfo.IsArray)
                            {
                                //Field is Array
                                Array array = (Array)fieldInfo.Getter(record);
                                for (var y = 0; y < array.Length; ++y)
                                    SetArrayValue(array, y, fieldInfo, dataReader, x);

                                arrayLength -= array.Length;
                            }
                            else
                            {
                                SetValue(record, fieldInfo, dataReader, x);
                            }
                        }
                    }
                    else
                    {
                        if (fieldInfo.IsArray)
                        {
                            Array array = (Array)fieldInfo.Getter(record);
                            for (var y = 0; y < array.Length; ++y)
                                SetArrayValue(array, y, fieldInfo, dataReader, x + y);

                            x += array.Length - 1;
                        }
                        else
                            SetValue(record, fieldInfo, dataReader, x);
                    }                   
                }
            }
        }

        static void SetArrayValue(Array array, int arrayIndex, DBClientHelper helper, DB6BinaryReader reader, int field)
        {
            switch (Type.GetTypeCode(helper.RealType))
            {
                case TypeCode.SByte:
                    helper.SetValue(array, reader.ReadSByte(), arrayIndex);
                    break;
                case TypeCode.Byte:
                    helper.SetValue(array, reader.ReadByte(), arrayIndex);
                    break;
                case TypeCode.Int16:
                    helper.SetValue(array, reader.ReadInt16(), arrayIndex);
                    break;
                case TypeCode.UInt16:
                    helper.SetValue(array, reader.ReadUInt16(), arrayIndex);
                    break;
                case TypeCode.Int32:
                    helper.SetValue(array, reader.GetInt32(_header.GetFieldBytes(field)), arrayIndex);
                    break;
                case TypeCode.UInt32:
                    helper.SetValue(array, reader.GetUInt32(_header.GetFieldBytes(field)), arrayIndex);
                    break;
                case TypeCode.Single:
                    helper.SetValue(array, reader.ReadSingle(), arrayIndex);
                    break;
                case TypeCode.String:
                    helper.SetValue(array, GetString(reader, field), arrayIndex);
                    break;
            }
        }

        static void SetValue(object obj, DBClientHelper helper, DB6BinaryReader reader, int field)
        {
            switch (Type.GetTypeCode(helper.RealType))
            {
                case TypeCode.SByte:
                    helper.SetValue(obj, reader.ReadSByte());
                    break;
                case TypeCode.Byte:
                    helper.SetValue(obj, reader.ReadByte());
                    break;
                case TypeCode.Int16:
                    helper.SetValue(obj, reader.ReadInt16());
                    break;
                case TypeCode.UInt16:
                    helper.SetValue(obj, reader.ReadUInt16());
                    break;
                case TypeCode.Int32:
                    helper.SetValue(obj, reader.GetInt32(_header.GetFieldBytes(field)));
                    break;
                case TypeCode.UInt32:
                    helper.SetValue(obj, reader.GetUInt32(_header.GetFieldBytes(field)));
                    break;
                case TypeCode.Single:
                    helper.SetValue(obj, reader.ReadSingle());
                    break;
                case TypeCode.String:
                    string str = GetString(reader, field);
                    helper.SetValue(obj, str);
                    break;
            }
        }

        static string GetString(DB6BinaryReader reader, int field)
        {
            if (_stringTable != null)
                return _stringTable.LookupByKey(reader.GetUInt32(_header.GetFieldBytes(field)));

            return reader.ReadCString();
        }

        static DB6Header ReadHeader(BinaryReader reader)
        {
            DB6Header header = new DB6Header();
            header.Signature = reader.ReadStringFromChars(4);
            header.RecordCount = reader.ReadUInt32();
            header.FieldCount = reader.ReadUInt32();
            header.RecordSize = reader.ReadUInt32();
            header.StringTableSize = reader.ReadUInt32(); // also offset for sparse table

            header.TableHash = reader.ReadUInt32();
            header.LayoutHash = reader.ReadUInt32(); // 21737: changed from build number to layoutHash

            header.MinId = reader.ReadInt32();
            header.MaxId = reader.ReadInt32();
            header.Locale = reader.ReadInt32();
            header.CopyTableSize = reader.ReadInt32();
            header.Flags = reader.ReadUInt16();
            header.IndexField = reader.ReadUInt16();

            header.TotalFieldCount = reader.ReadUInt32();
            header.CommonDataSize = reader.ReadUInt32();

            for (int i = 0; i < header.FieldCount; i++)
            {
                header.columnMeta.Add(new DB6Header.FieldEntry() { UnusedBits = reader.ReadInt16(), Offset = (short)(reader.ReadInt16() + (header.HasIndexTable() ? 4 : 0)) });
            }

            if (header.HasIndexTable())
            {
                header.FieldCount++;
                header.columnMeta.Insert(0, new DB6Header.FieldEntry());
            }

            return header;
        }

        static Dictionary<int, byte[]> LoadData(BinaryReader reader)
        {
            Dictionary<int, byte[]> Data = new Dictionary<int, byte[]>();
            _stringTable = null;

            //                 headerSize
            long recordsOffset = 56 + (_header.HasIndexTable() ? _header.FieldCount - 1 : _header.FieldCount) * 4;
            long eof = reader.BaseStream.Length;

            long commonDataPos = eof - _header.CommonDataSize;
            long copyTablePos = commonDataPos - _header.CopyTableSize;
            long indexTablePos = copyTablePos - (_header.HasIndexTable() ? _header.RecordCount * 4 : 0);
            long stringTablePos = indexTablePos - (_header.IsSparseTable() ? 0 : _header.StringTableSize);

            // Index table
            int[] m_indexes = null;

            if (_header.HasIndexTable())
            {
                reader.BaseStream.Position = indexTablePos;

                m_indexes = new int[_header.RecordCount];

                for (int i = 0; i < _header.RecordCount; i++)
                    m_indexes[i] = reader.ReadInt32();
            }

            if (_header.IsSparseTable())
            {
                // Records table
                reader.BaseStream.Position = _header.StringTableSize;

                int ofsTableSize = _header.MaxId - _header.MinId + 1;

                for (int i = 0; i < ofsTableSize; i++)
                {
                    int offset = reader.ReadInt32();
                    int length = reader.ReadInt16();

                    if (offset == 0 || length == 0)
                        continue;

                    int id = _header.MinId + i;

                    long oldPos = reader.BaseStream.Position;

                    reader.BaseStream.Position = offset;

                    byte[] recordBytes = reader.ReadBytes(length);

                    byte[] newRecordBytes = new byte[recordBytes.Length + 4];

                    Array.Copy(BitConverter.GetBytes(id), newRecordBytes, 4);
                    Array.Copy(recordBytes, 0, newRecordBytes, 4, recordBytes.Length);

                    Data.Add(id, newRecordBytes);

                    reader.BaseStream.Position = oldPos;
                }
            }
            else
            {
                // Records table
                reader.BaseStream.Position = recordsOffset;

                for (int i = 0; i < _header.RecordCount; i++)
                {
                    reader.BaseStream.Position = recordsOffset + i * _header.RecordSize;

                    byte[] recordBytes = reader.ReadBytes((int)_header.RecordSize);

                    if (_header.HasIndexTable())
                    {
                        byte[] newRecordBytes = new byte[_header.RecordSize + 4];

                        Array.Copy(BitConverter.GetBytes(m_indexes[i]), newRecordBytes, 4);
                        Array.Copy(recordBytes, 0, newRecordBytes, 4, recordBytes.Length);

                        Data.Add(m_indexes[i], newRecordBytes);
                    }
                    else
                    {
                        int numBytes = (32 - _header.columnMeta[_header.IndexField].UnusedBits) >> 3;
                        int offset = _header.columnMeta[_header.IndexField].Offset;
                        int id = 0;

                        for (int j = 0; j < numBytes; j++)
                            id |= (recordBytes[offset + j] << (j * 8));

                        Data.Add(id, recordBytes);
                    }
                }

                // Strings table
                reader.BaseStream.Position = stringTablePos;

                _stringTable = new Dictionary<int, string>();
                while (reader.BaseStream.Position != stringTablePos + _header.StringTableSize)
                {
                    int index = (int)(reader.BaseStream.Position - stringTablePos);
                    _stringTable[index] = reader.ReadCString();
                }
            }

            // Copy index table
            if (copyTablePos != reader.BaseStream.Length && _header.CopyTableSize != 0)
            {
                reader.BaseStream.Position = copyTablePos;

                while (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    int id = reader.ReadInt32();
                    int idcopy = reader.ReadInt32();

                    byte[] copyRow = Data[idcopy];
                    byte[] newRow = new byte[copyRow.Length];

                    Array.Copy(copyRow, newRow, newRow.Length);
                    Array.Copy(BitConverter.GetBytes(id), newRow, 4);

                    Data.Add(id, newRow);
                }
            }
           
            if (_header.CommonDataSize != 0)
            {
                reader.BaseStream.Position = commonDataPos;

                int fieldsCount = reader.ReadInt32();

                _commandData = new Dictionary<int, byte[]>[fieldsCount];

                for (int i = 0; i < fieldsCount; i++)
                {
                    int count = reader.ReadInt32();
                    byte type = reader.ReadByte();

                    _commandData[i] = new Dictionary<int, byte[]>();

                    for (int j = 0; j < count; j++)
                    {
                        int id = reader.ReadInt32();

                        switch (type)
                        {
                            case 1: // 2 bytes
                                _commandData[i].Add(id, reader.ReadBytes(2));
                                break;
                            case 2: // 1 bytes
                                _commandData[i].Add(id, reader.ReadBytes(1));
                                break;
                            case 3: // 4 bytes
                            case 4:
                                _commandData[i].Add(id, reader.ReadBytes(4));
                                break;
                            default:
                                throw new Exception("Invalid data type " + type);
                        }
                    }
                }
            }

            return Data;
        }

        static DB6Header _header;
        static Dictionary<int, string> _stringTable;
        static Dictionary<int, byte[]>[] _commandData;
    }

    public struct DBClientHelper
    {
        public DBClientHelper(FieldInfo fieldInfo)
        {
            IsArray = false;
            FieldType = RealType = fieldInfo.FieldType;

            if (fieldInfo.FieldType.IsArray)
            {
                FieldType = RealType = fieldInfo.FieldType.GetElementType();
                IsArray = true;
            }

            IsEnum = FieldType.IsEnum;
            if (IsEnum)
            {
                IsEnum = FieldType.IsEnum;
                RealType = FieldType.GetEnumUnderlyingType();
            }

            Setter = fieldInfo.CompileSetter();
            Getter = fieldInfo.CompileGetter();
        }

        public void SetValue(Array array, object value, int arrayIndex)
        {
            if (!IsEnum)
                array.SetValue(Convert.ChangeType(value, FieldType), arrayIndex % array.Length);
            else
                array.SetValue(Enum.ToObject(FieldType, value), arrayIndex % array.Length);
        }

        public void SetValue(object obj, object value)
        {
            if (!IsEnum)
                Setter(obj, Convert.ChangeType(value, FieldType));
            else
                Setter(obj, Enum.ToObject(FieldType, value));
        }

        public Type FieldType;
        public Type RealType;
        public bool IsArray;
        bool IsEnum;
        Action<object, object> Setter;
        public Func<object, object> Getter;
    }

    class DB6Header
    {
        public bool IsValidDB6File()
        {
            return Signature == "WDB6";
        }

        public bool IsSparseTable()
        {
            return Convert.ToBoolean(Flags & 0x1);
        }

        public bool HasIndexTable()
        {
            return Convert.ToBoolean(Flags & 0x4);
        }

        public int GetFieldBytes(int field)
        {
            if (columnMeta.Count <= field)
                return 4;

            return 4 - columnMeta[field].UnusedBits / 8;
        }

        public string Signature;
        public uint RecordCount;
        public uint FieldCount;
        public uint RecordSize;
        public uint StringTableSize;

        public uint TableHash;
        public uint LayoutHash;
        public int MinId;
        public int MaxId;
        public int Locale;
        public int CopyTableSize;
        public uint Flags;
        public int IndexField;
        public uint TotalFieldCount;
        public uint CommonDataSize;

        public List<FieldEntry> columnMeta = new List<FieldEntry>();

        public struct FieldEntry
        {
            public short UnusedBits;
            public short Offset;
        }
    }

    class DB6BinaryReader : BinaryReader
    {
        public DB6BinaryReader(byte[] data) : base(new MemoryStream(data)) { }

        public int GetInt32(int fieldBytes)
        {
            switch (fieldBytes)
            {
                case 1:
                    return ReadSByte();
                case 2:
                    return ReadInt16();
                case 3:
                    byte[] bytes = ReadBytes(fieldBytes);
                    return bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
                default:
                    return ReadInt32();
            }
        }

        public uint GetUInt32(int fieldBytes)
        {
            switch (fieldBytes)
            {
                case 1:
                    return ReadByte();
                case 2:
                    return ReadUInt16();
                case 3:
                    byte[] bytes = ReadBytes(fieldBytes);
                    return bytes[0] | ((uint)bytes[1] << 8) | ((uint)bytes[2] << 16);
                default:
                    return ReadUInt32();
            }
        }

        public float GetSingle(int fieldBytes)
        {
            switch (fieldBytes)
            {
                case 1:
                    return ReadByte();
                case 2:
                    return ReadUInt16();
                case 3:
                    byte[] bytes = ReadBytes(fieldBytes);
                    return bytes[0] | ((uint)bytes[1] << 8) | ((uint)bytes[2] << 16);
                default:
                    return ReadSingle();
            }
        }
    }
}
