// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;
using NuGet.Configuration;

namespace Microsoft.NET.TestFramework
{
    public abstract class AspNetSdkTest : SdkTest
    {   
        private static readonly IEnumerable<System.Reflection.AssemblyMetadataAttribute> TestAssemblyMetadata = Assembly.GetCallingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>();	        public readonly string DefaultTfm  = "net6.0";
        public readonly string DefaultTfm = TestAssemblyMetadata.SingleOrDefault(a => a.Key == "AspNetTestTfm").Value;

        protected AspNetSdkTest(ITestOutputHelper log) : base(log) { }

        public TestAsset CreateAspNetSdkTestAsset(
            string testAsset,
            [CallerMemberName] string callerName = "",
            string subdirectory = "",
            string overrideTfm = null) 
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, callingMethod: callerName, testAssetSubdirectory: subdirectory)
                .WithSource()
                .WithProjectChanges(project => 
                {
                    var ns = project.Root.Name.Namespace;
                    var targetFramework = project.Descendants()
                       .Single(e => e.Name.LocalName == "TargetFramework");
                    if (targetFramework.Value == "$(AspNetTestTfm)")
                    {
                        targetFramework.Value = overrideTfm ?? DefaultTfm;
                    }
                });
            return projectDirectory;
        }
    }
}
