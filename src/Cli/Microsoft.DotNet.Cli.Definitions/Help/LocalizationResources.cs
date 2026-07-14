// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.Parsing;
using System.Resources;

namespace Microsoft.DotNet.Cli.Help;

/// <summary>
/// Provides localizable strings for help and error messages.
/// </summary>
public static class LocalizationResources
{
    private static Lazy<ResourceManager> _resourceManager = new(
        () => new ResourceManager("System.CommandLine.Properties.Resources", typeof(System.CommandLine.Symbol).Assembly));

    /// <summary>
    ///   Interpolates values into a localized string similar to Usage:.
    /// </summary>
    public static string HelpUsageTitle() =>
        GetResourceString("HelpUsageTitle");

    /// <summary>
    ///   Interpolates values into a localized string similar to Description:.
    /// </summary>
    public static string HelpDescriptionTitle() =>
        GetResourceString("HelpDescriptionTitle");

    /// <summary>
    ///   Interpolates values into a localized string similar to [options].
    /// </summary>
    public static string HelpUsageOptions() =>
        GetResourceString("HelpUsageOptions");

    /// <summary>
    ///   Interpolates values into a localized string similar to [command].
    /// </summary>
    public static string HelpUsageCommand() =>
        GetResourceString("HelpUsageCommand");

    /// <summary>
    ///   Interpolates values into a localized string similar to [[--] &lt;additional arguments&gt;...]].
    /// </summary>
    public static string HelpUsageAdditionalArguments() =>
        GetResourceString("HelpUsageAdditionalArguments");

    /// <summary>
    ///   Interpolates values into a localized string similar to Arguments:.
    /// </summary>
    public static string HelpArgumentsTitle() =>
        GetResourceString("HelpArgumentsTitle");

    /// <summary>
    ///   Interpolates values into a localized string similar to Options:.
    /// </summary>
    public static string HelpOptionsTitle() =>
        GetResourceString("HelpOptionsTitle");

    /// <summary>
    ///   Interpolates values into a localized string similar to (REQUIRED).
    /// </summary>
    public static string HelpOptionsRequiredLabel() =>
        GetResourceString("HelpOptionsRequiredLabel");

    /// <summary>
    ///   Interpolates values into a localized string similar to default.
    /// </summary>
    public static string HelpArgumentDefaultValueLabel() =>
        GetResourceString("HelpArgumentDefaultValueLabel");

    /// <summary>
    ///   Interpolates values into a localized string similar to Commands:.
    /// </summary>
    public static string HelpCommandsTitle() =>
        GetResourceString("HelpCommandsTitle");

    /// <summary>
    ///   Interpolates values into a localized string similar to Additional Arguments:.
    /// </summary>
    public static string HelpAdditionalArgumentsTitle() =>
        GetResourceString("HelpAdditionalArgumentsTitle");

    /// <summary>
    ///   Interpolates values into a localized string similar to Arguments passed to the application that is being run..
    /// </summary>
    public static string HelpAdditionalArgumentsDescription() =>
        GetResourceString("HelpAdditionalArgumentsDescription");

    /// <summary>
    /// Interpolates values into a localized string.
    /// </summary>
    /// <param name="resourceName">The name of the resource.</param>
    /// <param name="formatArguments">The values to interpolate.</param>
    /// <returns>The final string after interpolation.</returns>
    private static string GetResourceString(string resourceName, params object[] formatArguments)
    {
        string? resourceString = _resourceManager.Value.GetString(resourceName);
        if (resourceString is null)
        {
            return string.Empty;
        }
        if (formatArguments.Length > 0)
        {
            return string.Format(resourceString, formatArguments);
        }
        return resourceString;
    }

    private static string GetOptionName(OptionResult optionResult) => optionResult.IdentifierToken?.Value ?? optionResult.Option.Name;
}
