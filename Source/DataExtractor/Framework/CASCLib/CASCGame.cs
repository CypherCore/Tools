using System;
using System.IO;

namespace DataExtractor.CASCLib
{
    public enum CASCGameType
    {
        Unknown,
        HotS,
        WoW,
        D3,
        S2,
        Agent,
        Hearthstone,
        Overwatch,
        Bna,
        Client,
        S1,
        WC3,
        Destiny2,
        D2R
    }

    public class CASCGame
    {
        public static CASCGameType DetectLocalGame(string path)
        {
            if (Directory.Exists(Path.Combine(path, "Data")))
            {
                string[] wowWinBins = new string[] { "Wow.exe", "WowT.exe", "WowB.exe", "WowClassic.exe", "WowClassicT.exe", "WowClassicB.exe" };

                for (int i = 0; i < wowWinBins.Length; i++)
                {
                    if (File.Exists(Path.Combine(path, wowWinBins[i])))
                        return CASCGameType.WoW;
                }

                string[] wowOsxBins = new string[] { "World of Warcraft.app", "World of Warcraft Test.app", "World of Warcraft Beta.app", "World of Warcraft Classic.app" };

                for (int i = 0; i < wowOsxBins.Length; i++)
                {
                    if (Directory.Exists(Path.Combine(path, wowOsxBins[i])))
                        return CASCGameType.WoW;
                }

                string[] subFolders = new string[] { "_retail_", "_ptr_", "_beta_", "_alpha_", "_event1_", "_classic_", "_classic_beta_", "_classic_ptr_", "_classic_era_", "_classic_era_beta_", "_classic_era_ptr_" };

                foreach (var subFolder in subFolders)
                {
                    foreach (var wowBin in wowWinBins)
                    {
                        if (File.Exists(Path.Combine(path, subFolder, wowBin)))
                            return CASCGameType.WoW;
                    }

                    foreach (var wowBin in wowOsxBins)
                    {
                        if (Directory.Exists(Path.Combine(path, subFolder, wowBin)))
                            return CASCGameType.WoW;
                    }
                }
            }

            throw new Exception("Unable to detect game type by path");
        }

        public static CASCGameType DetectGameByUid(string uid)
        {
            if (uid.StartsWith("wow"))
                return CASCGameType.WoW;

            throw new Exception("Unable to detect game type by uid");
        }

        public static string GetDataFolder(CASCGameType gameType)
        {
            if (gameType == CASCGameType.WoW)
                return "Data";

            throw new Exception("GetDataFolder called with unsupported gameType");
        }

        public static bool SupportsLocaleSelection(CASCGameType gameType)
        {
            return gameType == CASCGameType.D3 ||
                gameType == CASCGameType.WoW ||
                gameType == CASCGameType.HotS ||
                gameType == CASCGameType.S2 ||
                gameType == CASCGameType.S1 ||
                gameType == CASCGameType.WC3 ||
                gameType == CASCGameType.Overwatch;
        }
    }
}
