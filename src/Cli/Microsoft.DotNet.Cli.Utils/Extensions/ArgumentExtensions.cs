// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Reflection;

namespace Microsoft.DotNet.Cli.Utils.Extensions;

public static class ArgumentExtensions
{
    private static readonly PropertyInfo[] s_nonPublicProperties = typeof(Argument).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic);

    public static bool? GetHasValidators(this Argument argument) =>
        s_nonPublicProperties.First(pi => pi.Name == "HasValidators").GetValue(argument) as bool?;
}

