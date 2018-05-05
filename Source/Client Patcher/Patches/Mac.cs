namespace ClientPatcher.Patches
{
    class Mac
    {
        public static char[] LauncherLoginParametersLocation = "org.trnity".ToCharArray(); // not a typo, length must match original
    }
}
