using Microsoft.NET.TestFramework.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public abstract class RazorSdkTest : SdkTest
    {
        public readonly string DefaultTfm = "net6.0";

        protected RazorSdkTest(ITestOutputHelper log) : base(log) {}

        public TestAsset CreateRazorSdkTestAsset(string testAsset, [CallerMemberName] string callerName = "", string subdirectory = "") 
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, callingMethod: callerName, testAssetSubdirectory: subdirectory)
                .WithSource()
                .WithProjectChanges(project => 
                {
                    var ns = project.Root.Name.Namespace;
                    var targetFramework = project.Descendants()
                       .Single(e => e.Name.LocalName == "TargetFramework");
                    if (targetFramework.Value == "$(DefaultTfm)") {
                        targetFramework.Value = DefaultTfm;
                    }
                });
            return projectDirectory;
        }
    }
}
