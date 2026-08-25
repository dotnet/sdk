// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests.Build;

public class BuildUtilitiesTests
{
    [Theory]
    [InlineData("-p:P=V", "P", "V")]
    [InlineData("-p:P==", "P", "=")]
    [InlineData("-p:P=A=B", "P", "A=B")]
    [InlineData("-p: P\t = V ", "P", " V ")]
    [InlineData("-p:P=", "P", "")]
    public void BuildProperties_Valid(string argValue, string name, string value)
    {
        var properties = BuildUtilities.ParseBuildProperties([argValue]);
        AssertEx.SequenceEqual([(name, value)], properties);
    }

    [Theory]
    [InlineData("P")]
    [InlineData("=P3")]
    [InlineData("=")]
    [InlineData("==")]
    public void BuildProperties_Invalid(string argValue)
    {
        var properties = BuildUtilities.ParseBuildProperties([argValue]);
        AssertEx.SequenceEqual([], properties);
    }
}
