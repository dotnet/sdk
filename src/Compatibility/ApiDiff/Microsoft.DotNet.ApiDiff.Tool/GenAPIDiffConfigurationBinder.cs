// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Binding;

namespace Microsoft.DotNet.ApiDiff;

// Binds System.CommandLine options to a DiffConfiguration object.
internal class GenAPIDiffConfigurationBinder : BinderBase<DiffConfiguration>
{
    private readonly Option<bool> _optionAddPartialModifier;
    private readonly Option<string> _optionAfterAssembliesFolderPath;
    private readonly Option<string> _optionAfterAssemblyReferencesFolderPath;
    private readonly Option<string[]?> _optionAttributesToExclude;
    private readonly Option<string> _optionBeforeAssembliesFolderPath;
    private readonly Option<string> _optionBeforeAssemblyReferencesFolderPath;
    private readonly Option<bool> _optionDebug;
    private readonly Option<bool> _optionHideImplicitDefaultConstructors;
    private readonly Option<bool> _optionIncludeTableOfContents;
    private readonly Option<string> _optionOutputFolderPath;

    internal GenAPIDiffConfigurationBinder(Option<bool> optionAddPartialModifier,
                                           Option<string> optionAfterAssembliesFolderPath,
                                           Option<string> optionAfterAssemblyReferencesFolderPath,
                                           Option<string[]?> optionAttributesToExclude,
                                           Option<string> optionBeforeAssembliesFolderPath,
                                           Option<string> optionBeforeAssemblyReferencesFolderPath,
                                           Option<bool> optionDebug,
                                           Option<bool> optionHideImplicitDefaultConstructors,
                                           Option<bool> optionIncludeTableOfContents,
                                           Option<string> optionOutputFolderPath)
    {
        _optionAddPartialModifier = optionAddPartialModifier;
        _optionAfterAssembliesFolderPath = optionAfterAssembliesFolderPath;
        _optionAfterAssemblyReferencesFolderPath = optionAfterAssemblyReferencesFolderPath;
        _optionAttributesToExclude = optionAttributesToExclude;
        _optionBeforeAssembliesFolderPath = optionBeforeAssembliesFolderPath;
        _optionBeforeAssemblyReferencesFolderPath = optionBeforeAssemblyReferencesFolderPath;
        _optionDebug = optionDebug;
        _optionHideImplicitDefaultConstructors = optionHideImplicitDefaultConstructors;
        _optionIncludeTableOfContents = optionIncludeTableOfContents;
        _optionOutputFolderPath = optionOutputFolderPath;
    }

    protected override DiffConfiguration GetBoundValue(BindingContext bindingContext) =>
        new DiffConfiguration(
            AddPartialModifier: bindingContext.ParseResult.GetValueForOption(_optionAddPartialModifier),
            AfterAssembliesFolderPath: bindingContext.ParseResult.GetValueForOption(_optionAfterAssembliesFolderPath) ?? throw new NullReferenceException("Null after assemblies directory."),
            AfterAssemblyReferencesFolderPath: bindingContext.ParseResult.GetValueForOption(_optionAfterAssemblyReferencesFolderPath),
            AttributesToExclude: bindingContext.ParseResult.GetValueForOption(_optionAttributesToExclude),
            BeforeAssembliesFolderPath: bindingContext.ParseResult.GetValueForOption(_optionBeforeAssembliesFolderPath) ?? throw new NullReferenceException("Null before assemblies directory."),
            BeforeAssemblyReferencesFolderPath: bindingContext.ParseResult.GetValueForOption(_optionBeforeAssemblyReferencesFolderPath),
            Debug: bindingContext.ParseResult.GetValueForOption(_optionDebug),
            HideImplicitDefaultConstructors: bindingContext.ParseResult.GetValueForOption(_optionHideImplicitDefaultConstructors),
            IncludeTableOfContents: bindingContext.ParseResult.GetValueForOption(_optionIncludeTableOfContents),
            OutputFolderPath: bindingContext.ParseResult.GetValueForOption(_optionOutputFolderPath) ?? throw new NullReferenceException("Null output directory.")
        );
}
