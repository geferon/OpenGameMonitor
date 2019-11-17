using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenGameMonitorWorker.Utils
{
    class SourceConfigParser
    {
        private readonly Dictionary<string, string> configData = new Dictionary<string, string>();
        private readonly Dictionary<string, string> commandLineData = new Dictionary<string, string>();

        public SourceConfigParser(Server server)
        {
            string[] startParamsUnparsed = new string[]
            {
                server.StartParams,
                server.StartParamsHidden
            };
            string startParams = String.Join(" ", startParamsUnparsed.Where(s => !String.IsNullOrEmpty(s)));

            ParseCommandline(startParams);
            ParseConfigFile(Path.Combine(server.Path, server.Game.Id, "cfg", "server.cfg"));
        }

        public void ParseConfigFile(string file)
        {
            string[] fileData = File.ReadAllLines(file);

            foreach (string line in fileData) {
                string lineParse = Regex.Replace(line.Trim(), @"\s*\/\/.+",
                    "", RegexOptions.Singleline);

                if (lineParse.Length == 0 || lineParse.StartsWith("//", true, System.Globalization.CultureInfo.InvariantCulture))
                {
                    continue;
                }

                List<string> lineParsedSplit = lineParse.Split(' ').ToList();

                string key = lineParsedSplit.First();
                lineParsedSplit.RemoveAt(0);
                string value = String.Join(" ", lineParsedSplit);
                if (value.StartsWith("\"", true, System.Globalization.CultureInfo.InvariantCulture) &&
                    value.EndsWith("\"", true, System.Globalization.CultureInfo.InvariantCulture))
                {
                    value = value.Substring(1, value.Length - 1);
                }

                configData.Add(key, value);
            }
        }

        public void ParseCommandline(string commandline)
        {
            Regex rx = new Regex(@"((?:-|\+).+?)(?:\s(.+?)(?=\s(?:-|\+))|$)", RegexOptions.Multiline);

            MatchCollection matches = rx.Matches(commandline);

            foreach (Match match in matches)
            {
                GroupCollection groups = match.Groups;
                string key = groups[0].Value;
                string value = groups[1]?.Value ?? "";

                commandLineData.Add(key, value);
            }
        }

        public bool IsSet(string key)
        {
            bool isSet = false;
            if (key.StartsWith("-", true, System.Globalization.CultureInfo.InvariantCulture) ||
                key.StartsWith("+", true, System.Globalization.CultureInfo.InvariantCulture))
            {
                isSet = commandLineData.ContainsKey(key);
            }
            
            if (!isSet)
            {
                isSet = configData.ContainsKey(key);
            }
            
            if (!isSet)
            {
                isSet = commandLineData.ContainsKey("+" + key);
            }

            return isSet;
        }
        
        public string Get(string key)
        {
            string value = null;
            if (key.StartsWith("-", true, System.Globalization.CultureInfo.InvariantCulture) ||
                key.StartsWith("+", true, System.Globalization.CultureInfo.InvariantCulture))
            {
                commandLineData.TryGetValue(key, out value);
            }

            if (String.IsNullOrEmpty(value))
            {
                configData.TryGetValue(key, out value);
            }
            
            if (String.IsNullOrEmpty(value))
            {
                commandLineData.TryGetValue("+" + key, out value);
            }

            return value;
        }
    }
}
