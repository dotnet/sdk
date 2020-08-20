using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;

namespace EndToEnd
{
    class TestProjectCreator
    {
        public const string NETCorePackageName = "Microsoft.NETCore.App";
        public const string AspNetCoreAppPackageName = "Microsoft.AspNetCore.App";
        public const string AspNetCoreAllPackageName = "Microsoft.AspNetCore.All";


        public string TestName { get; set; }
        public string Identifier { get; set; }

        public string PackageName { get; set; } = NETCorePackageName;

        public string MinorVersion { get; set; }

        public string RuntimeIdentifier { get; set; }

        public Dictionary<string, string> AdditionalProperties { get; } = new Dictionary<string, string>();

        public TestProjectCreator([CallerMemberName] string testName = null, string identifier = "")
        {
            TestName = testName;
            Identifier = identifier;
        }

        public TestAssetInstance Create()
        {
            var testInstance = TestBase.TestAssets.Get("TestAppSimple")
                .CreateInstance(callingMethod: TestName, identifier: Identifier + PackageName + "_" + MinorVersion)
                .WithSourceFiles();

            string projectDirectory = testInstance.Root.FullName;

            string projectPath = Path.Combine(projectDirectory, "TestAppSimple.csproj");

            var project = XDocument.Load(projectPath);
            var ns = project.Root.Name.Namespace;

            //  Update TargetFramework to the right version of .NET Core
            project.Root.Element(ns + "PropertyGroup")
                .Element(ns + "TargetFramework")
                .Value = "netcoreapp" + MinorVersion;

            if (!string.IsNullOrEmpty(RuntimeIdentifier))
            {
                project.Root.Element(ns + "PropertyGroup")
                    .Add(new XElement(ns + "RuntimeIdentifier", RuntimeIdentifier));
            }

            foreach (var additionalProperty in AdditionalProperties)
            {
                project.Root.Element(ns + "PropertyGroup").Add(new XElement(ns + additionalProperty.Key, additionalProperty.Value));
            }

            if (PackageName != NETCorePackageName)
            {
                if (new Version(MinorVersion).Major < 3)
                {
                    //  Add ASP.NET PackageReference with implicit version for target framework versions prior to 3.0
                    project.Root.Add(new XElement(ns + "ItemGroup",
                        new XElement(ns + "PackageReference", new XAttribute("Include", PackageName))));
                }
                else
                {
                    project.Root.Add(new XElement(ns + "ItemGroup",
                        new XElement(ns + "FrameworkReference", new XAttribute("Include", PackageName))));
                }
            }

            project.Save(projectPath);

            return testInstance;

        }
    }
}
