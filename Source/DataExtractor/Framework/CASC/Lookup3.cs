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
using System.Text;

namespace Framework.CASC
{
    public class  Lookup3
    {
        public ulong Hash(string data)
        {
            return Hash(Encoding.ASCII.GetBytes(data));
        }

        public ulong Hash(byte[] data)
        {
            var length = data.Length;
            uint a, b, c;

            a = b = c = 0xdeadbeef + (uint)length;

            if ((data.Length % 12) != 0)
                Array.Resize(ref data, data.Length + (12 - (data.Length % 12)));

            var k = 0;

            while (length > 12)
            {
                var i = (k >> 2) << 2;

                a += BitConverter.ToUInt32(data, i);
                b += BitConverter.ToUInt32(data, i + 4);
                c += BitConverter.ToUInt32(data, i + 8);

                Mix(ref a, ref b, ref c);

                length -= 12;
                k += 12;
            }

            var l = data.Length - 12;

            a += BitConverter.ToUInt32(data, l);
            b += BitConverter.ToUInt32(data, l + 4);
            c += BitConverter.ToUInt32(data, l + 8);

            Final(ref a, ref b, ref c);

            return ((ulong)c << 32) | b;
        }

        void Mix(ref uint a, ref uint b, ref uint c)
        {
            a -= c; a ^= Rot(c, 4); c += b;
            b -= a; b ^= Rot(a, 6); a += c;
            c -= b; c ^= Rot(b, 8); b += a;
            a -= c; a ^= Rot(c, 16); c += b;
            b -= a; b ^= Rot(a, 19); a += c;
            c -= b; c ^= Rot(b, 4); b += a;
        }

        void Final(ref uint a, ref uint b, ref uint c)
        {
            c ^= b; c -= Rot(b, 14);
            a ^= c; a -= Rot(c, 11);
            b ^= a; b -= Rot(a, 25);
            c ^= b; c -= Rot(b, 16);
            a ^= c; a -= Rot(c, 4);
            b ^= a; b -= Rot(a, 14);
            c ^= b; c -= Rot(b, 24);
        }

        uint Rot(uint x, int k)
        {
            return (x << k) | (x >> (32 - k));
        }
    }
}
