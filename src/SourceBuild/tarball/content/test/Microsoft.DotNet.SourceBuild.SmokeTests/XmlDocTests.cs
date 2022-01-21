// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using System;
using System.Linq;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

public class XmlDocTests
{
    private ITestOutputHelper OutputHelper { get; }
    private DotNetHelper DotNetHelper { get; }

    public XmlDocTests(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;
        DotNetHelper = new DotNetHelper(outputHelper);
    }

    /// <Summary>
    /// Verifies every targeting pack assembly has a xml doc file.
    /// There are exceptions which are specified in baselines/XmlDocIgnore.*.txt.
    /// </Summary>
    [Fact]
    public void VerifyTargetingPacksHaveDoc()
    {
        List<string> missingXmlDoc = new();

        string targetingPacksDirectory = Path.Combine(DotNetHelper.DotNetInstallDirectory, "packs");
        foreach (string targetingPackAssembly in Directory.EnumerateFiles(targetingPacksDirectory, "*.dll", SearchOption.AllDirectories))
        {
            if (targetingPackAssembly.EndsWith("resources.dll"))
            {
                continue;
            }

            string xmlFile = Path.ChangeExtension(targetingPackAssembly, ".xml");
            if (!File.Exists(xmlFile))
            {
                string pathWithoutPacksPrefix = xmlFile.Substring(targetingPacksDirectory.Length + 1);
                String[] pathParts = pathWithoutPacksPrefix.Split(Path.DirectorySeparatorChar);
                string pathWithoutVersion = string.Join(Path.DirectorySeparatorChar, pathParts.Take(1).Concat(pathParts.Skip(2)));
                missingXmlDoc.Add(pathWithoutVersion);
            }
        }

        BaselineHelper.Compare("MissingXmlDoc.txt", missingXmlDoc.OrderBy(entry => entry));
    }
}
