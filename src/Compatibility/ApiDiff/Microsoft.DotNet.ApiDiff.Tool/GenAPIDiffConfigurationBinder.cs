// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Binding;

namespace Microsoft.DotNet.ApiDiff;

// Binds System.CommandLine options to a DiffConfiguration object.
internal class GenAPIDiffConfigurationBinder : BinderBase<DiffConfiguration>
{
    private readonly Option<string> _optionBeforeAssembliesFolderPath;
    private readonly Option<string> _optionBeforeAssemblyReferencesFolderPath;
    private readonly Option<string> _optionAfterAssembliesFolderPath;
    private readonly Option<string> _optionAfterAssemblyReferencesFolderPath;
    private readonly Option<string> _optionOutputFolderPath;
    private readonly Option<string> _optionBeforeFriendlyName;
    private readonly Option<string> _optionAfterFriendlyName;
    private readonly Option<string> _optionTableOfContentsTitle;
    private readonly Option<string[]?> _optionAssembliesToExclude;
    private readonly Option<string[]?> _optionAttributesToExclude;
    private readonly Option<string[]?> _optionApisToExclude;
    private readonly Option<bool> _optionAddPartialModifier;
    private readonly Option<bool> _optionAttachDebugger;

    internal GenAPIDiffConfigurationBinder(Option<string> optionBeforeAssembliesFolderPath,
                                           Option<string> optionBeforeAssemblyReferencesFolderPath,
                                           Option<string> optionAfterAssembliesFolderPath,
                                           Option<string> optionAfterAssemblyReferencesFolderPath,
                                           Option<string> optionOutputFolderPath,
                                           Option<string> optionBeforeFriendlyName,
                                           Option<string> optionAfterFriendlyName,
                                           Option<string> optionTableOfContentsTitle,
                                           Option<string[]?> optionAssembliesToExclude,
                                           Option<string[]?> optionAttributesToExclude,
                                           Option<string[]?> optionApisToExclude,
                                           Option<bool> optionAddPartialModifier,
                                           Option<bool> optionAttachDebugger)
    {
        _optionBeforeAssembliesFolderPath = optionBeforeAssembliesFolderPath;
        _optionBeforeAssemblyReferencesFolderPath = optionBeforeAssemblyReferencesFolderPath;
        _optionAfterAssembliesFolderPath = optionAfterAssembliesFolderPath;
        _optionAfterAssemblyReferencesFolderPath = optionAfterAssemblyReferencesFolderPath;
        _optionOutputFolderPath = optionOutputFolderPath;
        _optionBeforeFriendlyName = optionBeforeFriendlyName;
        _optionAfterFriendlyName = optionAfterFriendlyName;
        _optionTableOfContentsTitle = optionTableOfContentsTitle;
        _optionAssembliesToExclude = optionAssembliesToExclude;
        _optionAttributesToExclude = optionAttributesToExclude;
        _optionApisToExclude = optionApisToExclude;
        _optionAddPartialModifier = optionAddPartialModifier;
        _optionAttachDebugger = optionAttachDebugger;
    }

    protected override DiffConfiguration GetBoundValue(BindingContext bindingContext) =>
        new DiffConfiguration(
            BeforeAssembliesFolderPath: bindingContext.ParseResult.GetValueForOption(_optionBeforeAssembliesFolderPath) ?? throw new NullReferenceException("Null before assemblies directory"),
            BeforeAssemblyReferencesFolderPath: bindingContext.ParseResult.GetValueForOption(_optionBeforeAssemblyReferencesFolderPath),
            AfterAssembliesFolderPath: bindingContext.ParseResult.GetValueForOption(_optionAfterAssembliesFolderPath) ?? throw new NullReferenceException("Null after assemblies directory"),
            AfterAssemblyReferencesFolderPath: bindingContext.ParseResult.GetValueForOption(_optionAfterAssemblyReferencesFolderPath),
            OutputFolderPath: bindingContext.ParseResult.GetValueForOption(_optionOutputFolderPath) ?? throw new NullReferenceException("Null output directory"),
            BeforeFriendlyName: bindingContext.ParseResult.GetValueForOption(_optionBeforeFriendlyName) ?? throw new NullReferenceException("Null before friendly name"),
            AfterFriendlyName: bindingContext.ParseResult.GetValueForOption(_optionAfterFriendlyName) ?? throw new NullReferenceException("Null after friendly name"),
            TableOfContentsTitle: bindingContext.ParseResult.GetValueForOption(_optionTableOfContentsTitle) ?? throw new NullReferenceException("Null table of contents title"),
            AssembliesToExclude: bindingContext.ParseResult.GetValueForOption(_optionAssembliesToExclude),
            AttributesToExclude: bindingContext.ParseResult.GetValueForOption(_optionAttributesToExclude),
            ApisToExclude: bindingContext.ParseResult.GetValueForOption(_optionApisToExclude),
            AddPartialModifier: bindingContext.ParseResult.GetValueForOption(_optionAddPartialModifier),
            AttachDebugger: bindingContext.ParseResult.GetValueForOption(_optionAttachDebugger)
        );
}
