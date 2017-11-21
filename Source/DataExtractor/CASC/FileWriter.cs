using System.IO;

namespace CASC
{
    public class FileWriter
    {
        public static void WriteFile(MemoryStream data, string path, FileMode fileMode = FileMode.Create)
        {
            using (var fs = new FileStream(path, fileMode, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, true))
                fs.Write(data.ToArray(), 0, (int)data.Length);
        }
    }
}
