// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class VSTestOptions
{
    private static string GetSemiColonEscapedstring(string arg)
    {
        if (arg.IndexOf(";") != -1)
        {
            return arg.Replace(";", "%3b");
        }

        return arg;
    }

    internal static string[] GetSemiColonEscapedArgs(IEnumerable<string> args)
    {
        int counter = 0;
        string[] array = new string[args.Count()];

        foreach (string arg in args)
        {
            array[counter++] = GetSemiColonEscapedstring(arg);
        }

        return array;
    }
}
