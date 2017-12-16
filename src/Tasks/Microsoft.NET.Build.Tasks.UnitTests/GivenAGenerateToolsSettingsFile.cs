// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGenerateToolsSettingsFile
    {
        private DotNetCliTool _dotNetCliTool = null;
        public GivenAGenerateToolsSettingsFile()
        {
            XDocument result = GenerateToolsSettingsFile.GenerateDocument("tool.dll", "mytool");
            var serializer = new XmlSerializer(typeof(DotNetCliTool));

            using (TextReader sr = new StringReader(result.ToString()))
            {
                _dotNetCliTool = (DotNetCliTool)serializer.Deserialize(sr);

            }
        }

        [Fact]
        public void It_puts_command_name_in_correct_place_of_the_file()
        {
            _dotNetCliTool.Commands.Single().Name.Should().Be("mytool");
        }

        [Fact]
        public void It_puts_entryPoint_in_correct_place_of_the_file()
        {
            _dotNetCliTool.Commands.Single().EntryPoint.Should().Be("tool.dll");
        }

        [Fact]
        public void It_puts_runner_as_dotnet()
        {
            _dotNetCliTool.Commands.Single().Runner.Should().Be("dotnet");
        }

        [XmlRoot(Namespace = "", IsNullable = false)]
        public class DotNetCliTool
        {
            [XmlArrayItem("Command", IsNullable = false)]
            public DotNetCliToolCommand[] Commands { get; set; }
        }

        [Serializable]
        [XmlType(AnonymousType = true)]
        public class DotNetCliToolCommand
        {
            [XmlAttribute]
            public string Name { get; set; }

            [XmlAttribute]
            public string EntryPoint { get; set; }

            [XmlAttribute]
            public string Runner { get; set; }
        }
    }
}
