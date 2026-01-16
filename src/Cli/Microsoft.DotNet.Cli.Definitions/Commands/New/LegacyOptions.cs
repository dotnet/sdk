// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.TemplateEngine.Cli;

public sealed class LegacyOptions
{
    public readonly Option<bool> ColumnsAllOption = CreateColumnsAllOption();
    public readonly Option<string[]> ColumnsOption = CreateColumnsOption();
    public readonly Option<bool> InteractiveOption = CreateInteractiveOption();
    public readonly Option<string[]> AddSourceOption = CreateAddSourceOption();

    public IEnumerable<Option> AllOptions
    {
        get
        {
            yield return ColumnsAllOption;
            yield return ColumnsOption;
            yield return InteractiveOption;
            yield return AddSourceOption;
        }
    }

    public static IEnumerable<string> AllNames
    {
        get
        {
            yield return SharedOptionsFactory.ColumnsAllOptionName;
            yield return SharedOptionsFactory.ColumnsOptionName;
            yield return SharedOptionsFactory.InteractiveOptionName;
            yield return SharedOptionsFactory.AddSourceOptionName;
        }
    }

    public static Option<bool> CreateInteractiveOption()
        => SharedOptionsFactory.CreateInteractiveOption().AsHidden();

    public static Option<string[]> CreateAddSourceOption()
        => SharedOptionsFactory.CreateAddSourceOption().AsHidden().DisableAllowMultipleArgumentsPerToken();

    public static Option<bool> CreateColumnsAllOption()
        => SharedOptionsFactory.CreateColumnsAllOption().AsHidden();

    public static Option<string[]> CreateColumnsOption()
        => SharedOptionsFactory.CreateColumnsOption().AsHidden().DisableAllowMultipleArgumentsPerToken();
}
