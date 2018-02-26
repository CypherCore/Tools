using System;
using System.Collections.Generic;
using System.Text;

namespace Framework.ClientReader
{
    public class BitReader
    {
        private byte[] m_array;
        private int m_readPos;
        private int m_readOffset;

        public int Position { get => m_readPos; set => m_readPos = value; }
        public int Offset { get => m_readOffset; set => m_readOffset = value; }
        public byte[] Data { get => m_array; set => m_array = value; }

        public BitReader(byte[] data)
        {
            m_array = data;
        }

        public BitReader(byte[] data, int offset)
        {
            m_array = data;
            m_readOffset = offset;
        }

        public uint ReadUInt32(int numBits)
        {
            uint result = FastStruct<uint>.ArrayToStructure(ref m_array[m_readOffset + (m_readPos >> 3)]) << (32 - numBits - (m_readPos & 7)) >> (32 - numBits);
            m_readPos += numBits;
            return result;
        }

        public ulong ReadUInt64(int numBits)
        {
            ulong result = FastStruct<ulong>.ArrayToStructure(ref m_array[m_readOffset + (m_readPos >> 3)]) << (64 - numBits - (m_readPos & 7)) >> (64 - numBits);
            m_readPos += numBits;
            return result;
        }

        public Value32 ReadValue32(int numBits)
        {
            unsafe
            {
                ulong result = ReadUInt32(numBits);
                return *(Value32*)&result;
            }
        }

        public Value64 ReadValue64(int numBits)
        {
            unsafe
            {
                ulong result = ReadUInt64(numBits);
                return *(Value64*)&result;
            }
        }

        // this will probably work in C# 7.3 once blittable generic constrain added, or not...
        //public unsafe T Read<T>(int numBits) where T : struct
        //{
        //    //fixed (byte* ptr = &m_array[m_readOffset + (m_readPos >> 3)])
        //    //{
        //    //    T val = *(T*)ptr << (sizeof(T) - numBits - (m_readPos & 7)) >> (sizeof(T) - numBits);
        //    //    m_readPos += numBits;
        //    //    return val;
        //    //}
        //    //T result = FastStruct<T>.ArrayToStructure(ref m_array[m_readOffset + (m_readPos >> 3)]) << (32 - numBits - (m_readPos & 7)) >> (32 - numBits);
        //    //m_readPos += numBits;
        //    //return result;
        //}
    }

    public struct Value32
    {
        unsafe fixed byte Value[4];

        public T GetValue<T>() where T : struct
        {
            unsafe
            {
                fixed (byte* ptr = Value)
                    return FastStruct<T>.ArrayToStructure(ref ptr[0]);
            }
        }

        public byte[] GetBytes(int bitSize)
        {
            byte[] data = new byte[NextPow2((int)(bitSize + 7) / 8)];
            unsafe
            {
                fixed (byte* ptr = Value)
                {
                    for (var i = 0; i < data.Length; ++i)
                        data[i] = ptr[i];
                }
            }

            return data;
        }

        private int NextPow2(int v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return Math.Max(v, 1);
        }
    }

    public struct Value64
    {
        unsafe fixed byte Value[8];

        public T GetValue<T>() where T : struct
        {
            unsafe
            {
                fixed (byte* ptr = Value)
                    return FastStruct<T>.ArrayToStructure(ref ptr[0]);
            }
        }

        public byte[] GetBytes(int bitSize)
        {
            byte[] data = new byte[NextPow2((int)(bitSize + 7) / 8)];
            unsafe
            {
                fixed (byte* ptr = Value)
                {
                    for (var i = 0; i < data.Length; ++i)
                        data[i] = ptr[i];
                }
            }

            return data;
        }

        private int NextPow2(int v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return Math.Max(v, 1);
        }
    }
}
