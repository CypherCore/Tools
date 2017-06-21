using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientPatcher.Patterns
{
    class Windows
    {
        public static class x86
        {
            public static byte[] CertBundleCASCLocalFile = { 0x6A, 0x00, 0x50, 0x8D, 0x45, 0xF8, 0x50, 0x68 };
            public static byte[] CertBundleSignatureCheck = { 0x59, 0x59, 0x84, 0xC0, 0x75, 0x08, 0x46, 0x83, 0xFE, 0x02 };
        }

        public static class x64
        {
            public static byte[] CertBundleCASCLocalFile = { 0x45, 0x33, 0xC9, 0x48, 0x89, 0x9C, 0x24, 0x90, 0x02 };
            public static byte[] CertBundleSignatureCheck = { 0x75, 0x0B, 0x48, 0xFF, 0xC7, 0x48, 0x83, 0xFF, 0x02 };
        }
    }
}
