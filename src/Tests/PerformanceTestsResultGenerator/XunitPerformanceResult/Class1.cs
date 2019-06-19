using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace PerformanceTestsResultGenerator.XunitPerformanceResult
{
    [Serializable]
    [XmlRoot("ScenarioBenchmark")]
    public sealed class ScenarioBenchmark
    {
        [XmlAttribute("Name")]
        public string Name { get; set; }

        [XmlAttribute("Namespace")]
        public string Namespace { get; set; }

        [XmlArray("Tests")]
        public List<ScenarioTestModel> Tests { get; set; }

        private ScenarioBenchmark()
        {
            Namespace = "";
            Tests = new List<ScenarioTestModel>();
        }

        public ScenarioBenchmark(string name) : this()
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException($"{nameof(name)} cannot be null, empty or white space.");
            Name = name;
        }

        public void Serialize(string xmlFileName)
        {
            var namespaces = new XmlSerializerNamespaces();
            namespaces.Add("", "");
            using (var stream = File.Create(xmlFileName))
            {
                using (var sw = new StreamWriter(stream))
                {
                    new XmlSerializer(typeof(ScenarioBenchmark))
                        .Serialize(sw, this, namespaces);
                }
            }
        }

        [Serializable]
        [XmlType("Test")]
        public sealed class ScenarioTestModel
        {
            [XmlAttribute("Name")]
            public string Name { get; set; }

            [XmlAttribute("Namespace")]
            public string Namespace { get => _namespace; set => _namespace = value ?? ""; }

            public string Separator { get => _separator; set => _separator = value ?? "/"; }

            [XmlElement("Performance")]
            public PerformanceModel Performance { get; set; }

            private ScenarioTestModel()
            {
                _namespace = "";
                _separator = "/";
                Performance = new PerformanceModel
                {
                    Metrics = new List<MetricModel>(),
                    IterationModels = new List<IterationModel>()
                };
            }

            public ScenarioTestModel(string name) : this()
            {
                Name = name;
            }

            private string _namespace;
            private string _separator;
        }

        [Serializable]
        [XmlType("test")]
        public sealed class TestModel
        {
            [XmlAttribute("name")]
            public string Name { get; set; }

            [XmlAttribute("type")]
            public string ClassName { get; set; }

            [XmlAttribute("method")]
            public string Method { get; set; }

            [XmlElement("performance")]
            public PerformanceModel Performance { get; set; }
        }

        public sealed class PerformanceModel : IXmlSerializable
        {
            public List<MetricModel> Metrics { get; set; }

            public List<IterationModel> IterationModels { get; set; }

            public XmlSchema GetSchema()
            {
                return null;
            }

            public void ReadXml(XmlReader reader)
            {
                throw new NotImplementedException();
            }

            public void WriteXml(XmlWriter writer)
            {
                writer.WriteStartElement("metrics");
                foreach (var metric in Metrics)
                {
                    writer.WriteStartElement(metric.Name);
                    writer.WriteAttributeString("displayName", metric.DisplayName);
                    writer.WriteAttributeString("unit", metric.Unit);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();

                writer.WriteStartElement("iterations");
                var index = 0;
                foreach (var iterationModel in IterationModels)
                {
                    writer.WriteStartElement("iteration");
                    writer.WriteAttributeString("index", index.ToString());
                    ++index;
                    foreach (var kvp in iterationModel.Iteration)
                    {
                        writer.WriteAttributeString(kvp.Key, kvp.Value.ToString(CultureInfo.InvariantCulture));
                    }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
        }

        public sealed class MetricModel
        {
            public string Name
            {
                get => _name;
                set => _name = XmlConvert.EncodeName(value);
            }

            public string DisplayName { get; set; }

            public string Unit { get; set; }

            private string _name = null;
        }

        public sealed class IterationModel
        {
            public Dictionary<string, double> Iteration { get; set; }
        }
    }
}


