// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Reflection;

namespace Microsoft.DotNet.Cli.Utils.Extensions;

public static class OptionExtensions
{
    private static readonly PropertyInfo s_argumentProperty =
        typeof(Option).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic).First(pi => pi.Name == "Argument");

    public static Argument? GetArgument(this Option option) =>
        s_argumentProperty.GetValue(option) as Argument;

}
