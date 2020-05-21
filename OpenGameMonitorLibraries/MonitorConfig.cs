using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenGameMonitorLibraries
{
    class MonitorConfig
    {
        public static int Version = 1;
        public static string VersionString = "1.0";

        public static Dictionary<string, object> DefaultConfig = new Dictionary<string, object>()
        {
            { "DefaultInstallDir", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:/Servers/" : "/opt/servers/" },
            //{ "InstallSeparateGameDirs", true }, // Replaced by DefaultServerDir
            { "DefaultServerDir", "{GameID}/{ServerID}" }
            //{ "", true }
        };
    }
}
