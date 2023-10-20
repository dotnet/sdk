// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

public class XmlDocTests : SmokeTests
{
    public XmlDocTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    /// <Summary>
    /// Verifies every targeting pack assembly has a xml doc file.
    /// There are exceptions which are specified in baselines/XmlDocIgnore.*.txt.
    /// </Summary>
    // Re-enable when fixing https://github.com/dotnet/source-build/issues/3660
    //[Fact]
    public void VerifyTargetingPacksHaveDoc()
    {
        List<string> missingXmlDoc = new();

        string targetingPacksDirectory = Path.Combine(Config.DotNetDirectory, "packs");
        foreach (string targetingPackAssembly in Directory.EnumerateFiles(targetingPacksDirectory, "*.dll", SearchOption.AllDirectories))
        {
            if (targetingPackAssembly.EndsWith("resources.dll"))
            {
                continue;
            }

            string xmlFile = Path.ChangeExtension(targetingPackAssembly, ".xml");
            if (!File.Exists(xmlFile))
            {
                string pathWithoutPacksPrefix = xmlFile[(targetingPacksDirectory.Length + 1)..];
                string[] pathParts = pathWithoutPacksPrefix.Split(Path.DirectorySeparatorChar);
                string pathWithoutVersion = string.Join(Path.DirectorySeparatorChar, pathParts.Take(1).Concat(pathParts.Skip(2)));
                pathWithoutVersion = BaselineHelper.RemoveNetTfmPaths(pathWithoutVersion);
                missingXmlDoc.Add(pathWithoutVersion);
            }
        }

        BaselineHelper.CompareEntries("MissingXmlDoc.txt", missingXmlDoc.OrderBy(entry => entry));
    }
}
