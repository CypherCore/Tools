using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientPatcher.Patterns
{
    class Common
    {
        public static char[] Portal = { '.', 'a', 'c', 't', 'u', 'a', 'l', '.', 'b', 'a', 't', 't', 'l', 'e', '.', 'n', 'e', 't', (char)0x00 };
        public static byte[] Modulus = { 0x91, 0xD5, 0x9B, 0xB7, 0xD4, 0xE1, 0x83, 0xA5 };
        public static byte[] BinaryVersion = { 0x3C, 0x56, 0x65, 0x72, 0x73, 0x69, 0x6F, 0x6E, 0x3E };
        public static char[] VersionsFile = { '%', 's', '.', 'p', 'a', 't', 'c', 'h', '.', 'b', 'a', 't', 't', 'l', 'e', '.', 'n', 'e', 't', ':', '1', '1', '1', '9', '/', '%', 's', '/', 'v', 'e', 'r', 's', 'i', 'o', 'n', 's' };
        public static char[] CertFileName = { 'c', 'a', '_', 'b', 'u', 'n', 'd', 'l', 'e', '.', 't', 'x', 't', '.', 's', 'i', 'g', 'n', 'e', 'd', (char)0x00 };
    }
}
