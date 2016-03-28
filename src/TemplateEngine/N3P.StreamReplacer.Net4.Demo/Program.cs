using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace N3P.StreamReplacer.Net4.Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            string defininitionsFile = args[0];
            string sourceFile = args[1];
            string targetFile = args[2];

            IOperationProvider[] operations = ParseOperations(defininitionsFile);

            IProcessor processor = Processor.Create(operations);

            using (Stream source = File.OpenRead(sourceFile))
            using (Stream target = File.Create(targetFile))
            {
                processor.Run(source, target);
            }
        }

        private static IOperationProvider[] ParseOperations(string defininitionsFile)
        {
            string definitionsText = File.ReadAllText(defininitionsFile);
            JArray definitionList = JArray.Parse(definitionsText);
            List<IOperationProvider> operations = new List<IOperationProvider>();

            foreach (JObject obj in definitionList.OfType<JObject>())
            {
                switch (obj["type"].Value<string>())
                {
                    case "replacement":
                        operations.Add(CreateReplacement(obj));
                        break;
                    case "region":
                        operations.Add(CreateRegion(obj));
                        break;
                    default:
                        Console.WriteLine($@"Unknown operation ""{obj["type"].Value<string>()}""");
                        break;
                }
            }

            return operations.ToArray();
        }

        private static IOperationProvider CreateRegion(JObject jObject)
        {
            bool include = jObject["include"].Value<bool>();
            string start = jObject["start"].Value<string>();
            string end = jObject["end"].Value<string>();
            return new Region(start, end, include);
        }

        private static IOperationProvider CreateReplacement(JObject jObject)
        {
            string find = jObject["find"].Value<string>();
            string replaceWith = jObject["replaceWith"].Value<string>();
            return new Replacment(find, replaceWith);
        }
    }
}
