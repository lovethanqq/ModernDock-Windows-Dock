using System;
using System.IO;

namespace MyCustomDock
{
    internal static class StartupPathPolicy
    {
        public static string OfficialInstallPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    "ModernDock",
                    "ModernDock.exe");
            }
        }

        public static bool IsOfficialInstallPath(string executablePath)
        {
            string actual = ApplicationIdentityResolver.NormalizeExecutablePath(executablePath);
            string official = ApplicationIdentityResolver.NormalizeExecutablePath(OfficialInstallPath);
            return !string.IsNullOrEmpty(actual) && string.Equals(actual, official, StringComparison.OrdinalIgnoreCase);
        }
    }
}
