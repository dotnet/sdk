// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    public abstract class CSharpFormatterTests : AbstractFormatterTest
    {
        protected override string DefaultFileExt => "cs";

        public override string Language => LanguageNames.CSharp;

        protected override CompilationOptions CreateCompilationOptions()
            => new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true);

        protected override ParseOptions CreateParseOptions()
            => new CSharpParseOptions(LanguageVersion.Default, DocumentationMode.Diagnose);
    }
}
