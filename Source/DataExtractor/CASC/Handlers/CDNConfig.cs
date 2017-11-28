using System;
using System.Collections.Generic;
using System.IO;
using CASC.Structures;
using CASC.FileSystem.Structures;
using System.Net;

namespace CASC.Handlers
{
    class CDNConfig
    {
        public string[] this[string name]
        {
            get
            {
                string[] entry;

                if (entries.TryGetValue(name, out entry))
                    return entry;

                return null;
            }
        }

        public string Host { get; set; }
        public string Path { get; set; }
        public string DownloadUrl => Host + Path;

        Dictionary<string, string[]> entries = new Dictionary<string, string[]>();

        public CDNConfig(string wowPath, string buildKey)
        {
            using (var sr = new StreamReader($"{wowPath}/Data/config/{buildKey.GetHexAt(0)}/{buildKey.GetHexAt(2)}/{buildKey}"))
            {
                while (!sr.EndOfStream)
                {
                    var data = sr.ReadLine().Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                    if (data.Length < 2)
                        continue;

                    var key = data[0].Trim();
                    var value = data[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    entries.Add(key, value);
                }
            }
        }

        public BinaryReader DownloadFile(string archive, IndexEntry indexEntry)
        {
            var url = $"http://{Host}/{Path}/data/{archive.Substring(0, 2)}/{archive.Substring(2, 2)}/{archive}";

            HttpWebRequest req = WebRequest.CreateHttp(url);
            req.AddRange(indexEntry.Offset, indexEntry.Offset + indexEntry.Size - 1);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponseAsync().Result)
            using (Stream s = resp.GetResponseStream())
            {
                MemoryStream ms = new MemoryStream();
                s.CopyTo(ms);
                ms.Position = 0;
                return new BinaryReader(ms);
            }
        }
    }
}
