using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace ClientPatcher
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length >= 1)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Cypher Systems Connection Patcher");

                var patchCertBundleSignatureCheck = Patches.Windows.x64.CertBundleSignatureCheck;
                var patternCertBundleSignatureCheck = Patterns.Windows.x64.CertBundleSignatureCheck;
                var patchCertBundleCASCLocalFile = Patches.Windows.x64.CertBundleCASCLocalFile;
                var patternCertBundleCASCLocalFile = Patterns.Windows.x64.CertBundleCASCLocalFile;
                var fileName = "";

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Creating patched binaries for ");

                using (var patcher = new Patcher(args[0]))
                {
                    fileName = patcher.Binary.Replace(".exe", "") + "_Patched.exe";
                    switch (patcher.Type)
                    {
                        case BinaryTypes.Pe32:
                            Console.WriteLine("Win32 client...");
                            patchCertBundleCASCLocalFile = Patches.Windows.x86.CertBundleCASCLocalFile;
                            patternCertBundleCASCLocalFile = Patterns.Windows.x86.CertBundleCASCLocalFile;
                            patchCertBundleSignatureCheck = Patches.Windows.x86.CertBundleSignatureCheck;
                            patternCertBundleSignatureCheck = Patterns.Windows.x86.CertBundleSignatureCheck;
                            break;
                        case BinaryTypes.Pe64:
                            Console.WriteLine("Win64 client...");
                            break;
                        case BinaryTypes.Mach32:
                            throw new NotSupportedException("Type: " + patcher.Type + " not supported!");
                        case BinaryTypes.Mach64:
                            Console.WriteLine("Mac client...");
                            patchCertBundleSignatureCheck = Patches.Mac.x64.CertBundleSignatureCheck;
                            patternCertBundleSignatureCheck = Patterns.Mac.x64.CertBundleSignatureCheck;
                            fileName = patcher.Binary + " Patched";
                            break;
                        default:
                            throw new NotSupportedException("Type: " + patcher.Type + " not supported!");
                    }

                    Console.WriteLine("patching Portal");
                    patcher.Patch(Patches.Common.Portal, Encoding.UTF8.GetBytes(Patterns.Common.Portal));

                    Console.WriteLine("patching redirect RSA Modulus");
                    patcher.Patch(Patches.Common.Modulus, Patterns.Common.Modulus);

                    Console.WriteLine("patching BNet certificate file location");
                    patcher.Patch(Encoding.UTF8.GetBytes(Patches.Common.CertFileName), Encoding.UTF8.GetBytes(Patterns.Common.CertFileName));

                    Console.WriteLine("patching BNet certificate file to load from local path instead of CASC");
                    patcher.Patch(patchCertBundleCASCLocalFile, patternCertBundleCASCLocalFile);

                    Console.WriteLine("patching BNet certificate file signature check");
                    patcher.Patch(patchCertBundleSignatureCheck, patternCertBundleSignatureCheck);

                    //std::string verPatch(Patches::Common::VersionsFile());
                    //std::string buildPattern = "build";

                    //boost::algorithm::replace_all(verPatch, buildPattern, std::to_string(buildNumber));
                    //std::vector < unsigned char> verVec(verPatch.begin(), verPatch.end());
                    //patcher.Patch(verVec, Patterns::Common::VersionsFile());

                    patcher.Binary = fileName;
                    patcher.Finish();

                    var baseDirectory = Path.GetDirectoryName(args[0]);

                    File.WriteAllBytes($"{baseDirectory}/cypher_bundle.txt", Convert.FromBase64String(Patches.Common.CertificateBundle));

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Patching done.");
                    Console.WriteLine("Successfully created your patched binaries.");
                }

            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Wrong number of arguments: Missing client file.");
            }

            Console.ForegroundColor = ConsoleColor.Gray;
            Thread.Sleep(5000);

            Environment.Exit(0);
        }
    }
}
