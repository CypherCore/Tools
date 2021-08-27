using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace DataExtractor.CASCLib
{
    public class IndexEntry
    {
        public int Index;
        public int Offset;
        public int Size;
    }

    public class CDNIndexHandler
    {
        private static readonly MD5HashComparer comparer = new();
        private Dictionary<MD5Hash, IndexEntry> CDNIndexData = new(comparer);

        private CASCConfig config;

        public IReadOnlyDictionary<MD5Hash, IndexEntry> Data => CDNIndexData;
        public int Count => CDNIndexData.Count;

        private CDNIndexHandler(CASCConfig cascConfig)
        {
            config = cascConfig;
        }

        public static CDNIndexHandler Initialize(CASCConfig config)
        {
            var handler = new CDNIndexHandler(config);

            for (int i = 0; i < config.Archives.Count; i++)
            {
                string archive = config.Archives[i];

                if (config.OnlineMode)
                    handler.DownloadIndexFile(archive, i);
                else
                    handler.OpenIndexFile(archive, i);
            }

            return handler;
        }

        private void ParseIndex(Stream stream, int i)
        {
            using var br = new BinaryReader(stream);
            stream.Seek(-12, SeekOrigin.End);
            int count = br.ReadInt32();
            stream.Seek(0, SeekOrigin.Begin);

            if (count * (16 + 4 + 4) > stream.Length)
                throw new Exception("ParseIndex failed");

            for (int j = 0; j < count; ++j)
            {
                MD5Hash key = br.Read<MD5Hash>();

                if (key.IsZeroed()) // wtf?
                    key = br.Read<MD5Hash>();

                if (key.IsZeroed()) // wtf?
                    throw new Exception("key.IsZeroed()");

                IndexEntry entry = new()
                {
                    Index = i,
                    Size = br.ReadInt32BE(),
                    Offset = br.ReadInt32BE()
                };
                CDNIndexData.Add(key, entry);
            }
        }

        private void DownloadIndexFile(string archive, int i)
        {
            try
            {
                string file = config.CDNPath + "/data/" + archive.Substring(0, 2) + "/" + archive.Substring(2, 2) + "/" + archive + ".index";

                Stream stream = CDNCache.Instance.OpenFile(file, false);

                if (stream != null)
                {
                    ParseIndex(stream, i);
                }
                else
                {
                    string url = "http://" + config.CDNHost + "/" + file;

                    using var fs = OpenFile(url);
                    ParseIndex(fs, i);
                }
            }
            catch (Exception exc)
            {
                throw new Exception($"DownloadFile failed: {archive} - {exc}");
            }
        }

        private void OpenIndexFile(string archive, int i)
        {
            try
            {
                string dataFolder = CASCGame.GetDataFolder(config.GameType);

                string path = Path.Combine(config.BasePath, dataFolder, "indices", archive + ".index");

                if (File.Exists(path))
                {
                    using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    ParseIndex(fs, i);
                }
                else
                {
                    DownloadIndexFile(archive, i);
                }
            }
            catch (Exception exc)
            {
                throw new Exception($"OpenFile failed: {archive} - {exc}");
            }
        }

        public Stream OpenDataFile(IndexEntry entry, int numRetries = 0)
        {
            var archive = config.Archives[entry.Index];

            string file = config.CDNPath + "/data/" + archive.Substring(0, 2) + "/" + archive.Substring(2, 2) + "/" + archive;

            if (numRetries >= 5)
                return null;

            Stream stream = CDNCache.Instance.OpenFile(file, true);

            if (stream != null)
            {
                stream.Position = entry.Offset;
                MemoryStream ms = new(entry.Size);
                stream.CopyBytes(ms, entry.Size);
                ms.Position = 0;
                return ms;
            }

            //using (HttpClient client = new HttpClient())
            //{
            //    client.DefaultRequestHeaders.Range = new RangeHeaderValue(entry.Offset, entry.Offset + entry.Size - 1);

            //    var resp = client.GetStreamAsync(url).Result;

            //    MemoryStream ms = new MemoryStream(entry.Size);
            //    resp.CopyBytes(ms, entry.Size);
            //    ms.Position = 0;
            //    return ms;
            //}

            string url = "http://" + config.CDNHost + "/" + file;

            HttpWebRequest req = WebRequest.CreateHttp(url);
            req.ReadWriteTimeout = 15000;
            //req.Headers[HttpRequestHeader.Range] = string.Format("bytes={0}-{1}", entry.Offset, entry.Offset + entry.Size - 1);
            req.AddRange(entry.Offset, entry.Offset + entry.Size - 1);

            HttpWebResponse resp;

            try
            {
                using (resp = (HttpWebResponse)req.GetResponse())
                using (Stream rstream = resp.GetResponseStream())
                {
                    MemoryStream ms = new(entry.Size);
                    rstream.CopyBytes(ms, entry.Size);
                    ms.Position = 0;
                    return ms;
                }
            }
            catch (WebException exc)
            {
                resp = (HttpWebResponse)exc.Response;

                if (exc.Status == WebExceptionStatus.ProtocolError && (resp.StatusCode == HttpStatusCode.NotFound || resp.StatusCode == (HttpStatusCode)429))
                    return OpenDataFile(entry, numRetries + 1);
                else
                    return null;
            }
        }

        public Stream OpenDataFileDirect(MD5Hash key)
        {
            var keyStr = key.ToHexString().ToLower();

            string file = config.CDNPath + "/data/" + keyStr.Substring(0, 2) + "/" + keyStr.Substring(2, 2) + "/" + keyStr;

            Stream stream = CDNCache.Instance.OpenFile(file, false);

            if (stream != null)
            {
                stream.Position = 0;
                MemoryStream ms = new();
                stream.CopyTo(ms);
                ms.Position = 0;
                return ms;
            }

            string url = "http://" + config.CDNHost + "/" + file;

            return OpenFile(url);
        }

        public static Stream OpenConfigFileDirect(CASCConfig cfg, string key)
        {
            string file = cfg.CDNPath + "/config/" + key.Substring(0, 2) + "/" + key.Substring(2, 2) + "/" + key;

            Stream stream = CDNCache.Instance.OpenFile(file, false);

            if (stream != null)
                return stream;

            string url = "http://" + cfg.CDNHost + "/" + file;

            return OpenFileDirect(url);
        }

        public static Stream OpenFileDirect(string url)
        {
            //using (HttpClient client = new HttpClient())
            //{
            //    var resp = client.GetStreamAsync(url).Result;

            //    MemoryStream ms = new MemoryStream();
            //    resp.CopyTo(ms);
            //    ms.Position = 0;
            //    return ms;
            //}

            HttpWebRequest req = WebRequest.CreateHttp(url);
            //long fileSize = GetFileSize(url);
            //req.AddRange(0, fileSize - 1);
            using HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            using Stream stream = resp.GetResponseStream();
            MemoryStream ms = new();
            stream.CopyToStream(ms, resp.ContentLength);
            ms.Position = 0;
            return ms;
        }

        private Stream OpenFile(string url)
        {
            HttpWebRequest req = WebRequest.CreateHttp(url);
            req.ReadWriteTimeout = 15000;
            //long fileSize = GetFileSize(url);
            //req.AddRange(0, fileSize - 1);
            using HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            using Stream stream = resp.GetResponseStream();
            MemoryStream ms = new();
            stream.CopyToStream(ms, resp.ContentLength);
            ms.Position = 0;
            return ms;
        }

        public IndexEntry GetIndexInfo(MD5Hash key)
        {
            return CDNIndexData.GetValueOrDefault(key);
        }

        public void Clear()
        {
            CDNIndexData.Clear();
            CDNIndexData = null;

            config = null;
        }
    }
}
