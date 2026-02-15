// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.New;

public sealed class FilterOptions
{
    public required Option<string> AuthorOption { get; init; }
    public required Option<string> BaselineOption { get; init; }
    public required Option<string> LanguageOption { get; init; }
    public required Option<string> TypeOption { get; init; }
    public required Option<string> TagOption { get; init; }
    public required Option<string>? PackageOption { get; init; }

    public IEnumerable<Option> AllOptions
    {
        get
        {
            yield return AuthorOption;
            yield return BaselineOption;
            yield return LanguageOption;
            yield return TypeOption;
            yield return TagOption;

            if (PackageOption != null)
            {
                yield return PackageOption;
            }
        }
    }

    public IEnumerable<string> AllNames
        => GetAllNames(hasPackageOption: PackageOption != null);

    public static IEnumerable<string> GetAllNames(bool hasPackageOption)
    {
        yield return SharedOptionsFactory.AuthorOptionName;
        yield return SharedOptionsFactory.BaselineOptionName;
        yield return SharedOptionsFactory.LanguageOptionName;
        yield return SharedOptionsFactory.TypeOptionName;
        yield return SharedOptionsFactory.TagOptionName;

        if (hasPackageOption)
        {
            yield return SharedOptionsFactory.PackageOptionName;
        }
    }

    public static FilterOptions CreateLegacy()
        => new()
        {
            AuthorOption = SharedOptionsFactory.CreateAuthorOption().AsHidden(),
            BaselineOption = SharedOptionsFactory.CreateBaselineOption().AsHidden(),
            LanguageOption = SharedOptionsFactory.CreateLanguageOption().AsHidden(),
            TypeOption = SharedOptionsFactory.CreateTypeOption().AsHidden(),
            TagOption = SharedOptionsFactory.CreateTagOption().AsHidden(),
            PackageOption = SharedOptionsFactory.CreatePackageOption().AsHidden(),
        };

    public static FilterOptions CreateSupported(bool hasPackageOption)
        => new()
        {
            AuthorOption = SharedOptionsFactory.CreateAuthorOption(),
            BaselineOption = SharedOptionsFactory.CreateBaselineOption(),
            LanguageOption = SharedOptionsFactory.CreateLanguageOption(),
            TypeOption = SharedOptionsFactory.CreateTypeOption(),
            TagOption = SharedOptionsFactory.CreateTagOption(),
            PackageOption = hasPackageOption ? SharedOptionsFactory.CreatePackageOption() : null,
        };
}
