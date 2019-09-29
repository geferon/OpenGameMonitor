using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGameMonitorLibraries
{
    class MonitorConfig
    {
        public static int Version = 1;
        public static string VersionString = "1.0";

        public string MySQLHost { get; set; }
        public string MySQLPort { get; set; }
        public string MySQLUser { get; set; }
        public string MySQLPassword { get; set; }
    }
}
