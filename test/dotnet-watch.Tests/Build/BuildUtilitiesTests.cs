// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests.Build;

[TestClass]
public class BuildUtilitiesTests
{
    [TestMethod]
    [DataRow("-p:P=V", "P", "V")]
    [DataRow("-p:P==", "P", "=")]
    [DataRow("-p:P=A=B", "P", "A=B")]
    [DataRow("-p: P\t = V ", "P", " V ")]
    [DataRow("-p:P=", "P", "")]
    public void BuildProperties_Valid(string argValue, string name, string value)
    {
        var properties = BuildUtilities.ParseBuildProperties([argValue]);
        AssertEx.SequenceEqual([(name, value)], properties);
    }

    [TestMethod]
    [DataRow("P")]
    [DataRow("=P3")]
    [DataRow("=")]
    [DataRow("==")]
    public void BuildProperties_Invalid(string argValue)
    {
        var properties = BuildUtilities.ParseBuildProperties([argValue]);
        AssertEx.SequenceEqual([], properties);
    }
}
