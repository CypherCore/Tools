using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientPatcher.Patches
{
    class Windows
    {
        public static class x86
        {
            public static byte[] CertBundleCASCLocalFile = { 0x6A, 0x01 };
            public static byte[] CertBundleSignatureCheck = { 0x59, 0x59, 0x84, 0xC0, 0xEB };
        }

        public static class x64
        {
            public static byte[] CertBundleCASCLocalFile = { 0x41, 0xB1, 0x01 };
            public static byte[] CertBundleSignatureCheck = { 0xEB };
        }
    }
}
