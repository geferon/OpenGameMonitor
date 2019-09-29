using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Core.OpenGameMonitorWorker.Utils
{
    class SourceConfigParser
    {
        private Dictionary<string, string> configData = new Dictionary<string, string>();
        private Dictionary<string, string> commandLineData = new Dictionary<string, string>();

        public void ParseConfigFile(string file)
        {
            string[] fileData = File.ReadAllLines(file);

            foreach (string line in fileData) {
                string lineParse = line.Trim();

                if (lineParse.Length == 0 || lineParse.StartsWith("//"))
                {
                    continue;
                }

                List<string> lineParsedSplit = lineParse.Split(' ').ToList();

                string key = lineParsedSplit.First();
                lineParsedSplit.RemoveAt(0);
                string value = String.Join(" ", lineParsedSplit);
                if (value.StartsWith("\"") && value.EndsWith("\""))
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
    }
}
