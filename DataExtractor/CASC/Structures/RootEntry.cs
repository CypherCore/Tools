using CASC.Constants;

namespace CASC.Structures
{
    public struct RootEntry
    {
        public byte[] MD5 { get; set; }
        public ulong Hash { get; set; }
        public LocaleMask Locales { get; set; }
    }
}
