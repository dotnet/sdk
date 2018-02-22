using System.Collections.Generic;
using System.IO;

namespace BaselineComparer.Helpers
{
    public static class TemplateCommandReader
    {
        public static IReadOnlyList<string> ReadTemplateCommandFile(string filename)
        {
            List<string> commandList = new List<string>();

            using (StreamReader commandReader = new StreamReader(filename))
            {
                string line;
                while ((line = commandReader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    {
                        continue;
                    }

                    commandList.Add(line);
                }
            }

            return commandList;
        }
    }
}
