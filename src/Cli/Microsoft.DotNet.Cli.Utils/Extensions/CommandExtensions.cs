// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.DotNet.Cli.Utils.Extensions;

#pragma warning disable IDE0065 // Misplaced using directive
using Command = System.CommandLine.Command;
#pragma warning restore IDE0065 // Misplaced using directive

public static class CommandExtensions
{
    private static readonly PropertyInfo[] s_nonPublicProperties = typeof(Command).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic);

    public static bool? GetHasArguments(this Command command) =>
        s_nonPublicProperties.First(pi => pi.Name == "HasArguments").GetValue(command) as bool?;

    public static bool? GetHasOptions(this Command command) =>
        s_nonPublicProperties.First(pi => pi.Name == "HasOptions").GetValue(command) as bool?;

    public static bool? GetHasSubcommands(this Command command) =>
        s_nonPublicProperties.First(pi => pi.Name == "HasSubcommands").GetValue(command) as bool?;

    public static bool? GetHasValidators(this Command command) =>
        s_nonPublicProperties.First(pi => pi.Name == "HasValidators").GetValue(command) as bool?;
}
