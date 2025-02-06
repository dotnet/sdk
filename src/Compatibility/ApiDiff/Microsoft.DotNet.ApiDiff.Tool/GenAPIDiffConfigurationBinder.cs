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
    private readonly Option<string> _optionTableOfContentsTitle;
    private readonly Option<string[]?> _optionAttributesToExclude;
    private readonly Option<bool> _optionAddPartialModifier;
    private readonly Option<bool> _optionHideImplicitDefaultConstructors;
    private readonly Option<bool> _optionDebug;

    internal GenAPIDiffConfigurationBinder(Option<string> optionBeforeAssembliesFolderPath,
                                           Option<string> optionBeforeAssemblyReferencesFolderPath,
                                           Option<string> optionAfterAssembliesFolderPath,
                                           Option<string> optionAfterAssemblyReferencesFolderPath,
                                           Option<string> optionOutputFolderPath,
                                           Option<string> optionTableOfContentsTitle,
                                           Option<string[]?> optionAttributesToExclude,
                                           Option<bool> optionAddPartialModifier,
                                           Option<bool> optionHideImplicitDefaultConstructors,
                                           Option<bool> optionDebug)
    {
        _optionBeforeAssembliesFolderPath = optionBeforeAssembliesFolderPath;
        _optionBeforeAssemblyReferencesFolderPath = optionBeforeAssemblyReferencesFolderPath;
        _optionAfterAssembliesFolderPath = optionAfterAssembliesFolderPath;
        _optionAfterAssemblyReferencesFolderPath = optionAfterAssemblyReferencesFolderPath;
        _optionOutputFolderPath = optionOutputFolderPath;
        _optionTableOfContentsTitle = optionTableOfContentsTitle;
        _optionAttributesToExclude = optionAttributesToExclude;
        _optionAddPartialModifier = optionAddPartialModifier;
        _optionHideImplicitDefaultConstructors = optionHideImplicitDefaultConstructors;
        _optionDebug = optionDebug;
    }

    protected override DiffConfiguration GetBoundValue(BindingContext bindingContext) =>
        new DiffConfiguration(
            BeforeAssembliesFolderPath: bindingContext.ParseResult.GetValueForOption(_optionBeforeAssembliesFolderPath) ?? throw new NullReferenceException("Null before assemblies directory."),
            BeforeAssemblyReferencesFolderPath: bindingContext.ParseResult.GetValueForOption(_optionBeforeAssemblyReferencesFolderPath),
            AfterAssembliesFolderPath: bindingContext.ParseResult.GetValueForOption(_optionAfterAssembliesFolderPath) ?? throw new NullReferenceException("Null after assemblies directory."),
            AfterAssemblyReferencesFolderPath: bindingContext.ParseResult.GetValueForOption(_optionAfterAssemblyReferencesFolderPath),
            OutputFolderPath: bindingContext.ParseResult.GetValueForOption(_optionOutputFolderPath) ?? throw new NullReferenceException("Null output directory."),
            TableOfContentsTitle: bindingContext.ParseResult.GetValueForOption(_optionTableOfContentsTitle) ?? throw new NullReferenceException("Null table of contents title."),
            AttributesToExclude: bindingContext.ParseResult.GetValueForOption(_optionAttributesToExclude),
            AddPartialModifier: bindingContext.ParseResult.GetValueForOption(_optionAddPartialModifier),
            HideImplicitDefaultConstructors: bindingContext.ParseResult.GetValueForOption(_optionHideImplicitDefaultConstructors),
            Debug: bindingContext.ParseResult.GetValueForOption(_optionDebug)
        );
}
