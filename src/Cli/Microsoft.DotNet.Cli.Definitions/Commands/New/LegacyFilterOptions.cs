// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.TemplateEngine.Cli;

public sealed class LegacyFilterOptions
{
    public readonly Option<string> AuthorOption = SharedOptionsFactory.CreateAuthorOption().AsHidden();
    public readonly Option<string> BaselineOption = SharedOptionsFactory.CreateBaselineOption().AsHidden();
    public readonly Option<string> LanguageOption = SharedOptionsFactory.CreateLanguageOption().AsHidden();
    public readonly Option<string> TypeOption = SharedOptionsFactory.CreateTypeOption().AsHidden();
    public readonly Option<string> TagOption = SharedOptionsFactory.CreateTagOption().AsHidden();

    public readonly Option<string> PackageOption = SharedOptionsFactory.CreatePackageOption().AsHidden();

    public IEnumerable<Option> GetAllOptions()
    {
        yield return AuthorOption;
        yield return BaselineOption;
        yield return LanguageOption;
        yield return TypeOption;
        yield return TagOption;
        yield return PackageOption;
    }
}

public sealed class LegacyOptions
{
    public readonly Option<bool> ColumnsAllOption = CreateColumnsAllOption();
    public readonly Option<string[]> ColumnsOption = CreateColumnsOption();
    public readonly Option<bool> InteractiveOption = CreateInteractiveOption();
    public readonly Option<string[]> AddSourceOption = CreateAddSourceOption();

    public IEnumerable<Option> GetAllOptions()
    {
        yield return ColumnsAllOption;
        yield return ColumnsOption;
        yield return InteractiveOption;
        yield return AddSourceOption;
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
