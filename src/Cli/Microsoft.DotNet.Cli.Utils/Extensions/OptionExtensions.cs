// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Reflection;

namespace Microsoft.DotNet.Cli.Utils.Extensions;

public static class OptionExtensions
{
    private static readonly PropertyInfo[] s_nonPublicProperties = typeof(Option).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic);

    public static Argument? GetArgument(this Option option) =>
        s_nonPublicProperties.First(pi => pi.Name == "Argument").GetValue(option) as Argument;

    public static bool? GetHasValidators(this Option option) =>
        s_nonPublicProperties.First(pi => pi.Name == "HasValidators").GetValue(option) as bool?;
}
