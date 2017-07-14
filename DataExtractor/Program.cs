using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CASC.Handlers;
using DataExtractor.Constants;
using System.Text.RegularExpressions;
using CASC.Constants;
using System.IO;
using CASC;

namespace DataExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("CypherCore Data Extractor");
            Console.ForegroundColor = ConsoleColor.White;

            Console.WriteLine("Initializing CASC library...");
            cascHandler = new CASCHandler(Environment.CurrentDirectory);
            Console.WriteLine("Done.");

            if (Directory.Exists($"{Environment.CurrentDirectory}/dbc"))
                Directory.Delete($"{Environment.CurrentDirectory}/dbc", true);

            Directory.CreateDirectory($"{Environment.CurrentDirectory}/dbc");

            List<Locale> locales = new List<Locale>();
            var buildInfoLocales = Regex.Matches(cascHandler.buildInfo["Tags"], " ([A-Za-z]{4}) speech");
            foreach (Match m in buildInfoLocales)
            {
                var localFlag = (Locale)Enum.Parse(typeof(Locale), m.Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0]);

                if (!locales.Contains(localFlag))
                    locales.Add(localFlag);
            }

            Console.WriteLine("Extracting files...");
            uint count = 0;
            foreach (var file in FileList.DBFilesClientList)
            {
                for (int i = 0; i < locales.Count; ++i)
                {
                    if (locales[i] == Locale.None)
                        continue;

                    if (!Directory.Exists($"{Environment.CurrentDirectory}/dbc/{locales[i]}"))
                        Directory.CreateDirectory($"{Environment.CurrentDirectory}/dbc/{locales[i]}");

                    var stream = cascHandler.ReadFile(file, WowLocaleToCascLocaleFlags[(int)locales[i]]);
                    FileWriter.WriteFile(stream, $"{Environment.CurrentDirectory}/dbc/{locales[i]}/{ file.Replace(@"\\", "").Replace(@"DBFilesClient\", "")}");
                    count++;
                }
            }

            Console.WriteLine($"Extracted {count} files.");
        }

        static CASCHandler cascHandler;

        static LocaleMask[] WowLocaleToCascLocaleFlags =
        {
            LocaleMask.enUS | LocaleMask.enGB,
            LocaleMask.koKR,
            LocaleMask.frFR,
            LocaleMask.deDE,
            LocaleMask.zhCN,
            LocaleMask.zhTW,
            LocaleMask.esES,
            LocaleMask.esMX,
            LocaleMask.ruRU,
            0,
            LocaleMask.ptBR | LocaleMask.ptPT,
            LocaleMask.itIT
        };
    }


}
