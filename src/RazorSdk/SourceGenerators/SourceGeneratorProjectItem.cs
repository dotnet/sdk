// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using System.IO;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    internal class SourceGeneratorProjectItem : RazorProjectItem
    {
        private readonly string _fileKind;

        private readonly GeneratorExecutionContext _context;

        public SourceGeneratorProjectItem(string basePath, string filePath, string relativePhysicalPath, string fileKind, AdditionalText additionalText, string? cssScope, GeneratorExecutionContext context)
        {
            BasePath = basePath;
            FilePath = filePath;
            RelativePhysicalPath = relativePhysicalPath;
            _fileKind = fileKind;
            AdditionalText = additionalText;
            CssScope = cssScope;
            _context = context;
            var text = AdditionalText.GetText();
            if (text is null)
            {
                _context.ReportDiagnostic(Diagnostic.Create(RazorDiagnostics.SourceTextNotFoundDescriptor, Location.None, filePath));
            }
            else 
            {
                RazorSourceDocument = new SourceTextRazorSourceDocument(AdditionalText.Path, relativePhysicalPath, text);
            }
        }

        public AdditionalText AdditionalText { get; }

        public override string BasePath { get; }

        public override string FilePath { get; }

        public override bool Exists => true;

        public override string PhysicalPath => AdditionalText.Path;

        public override string RelativePhysicalPath { get; }

        public override string FileKind => _fileKind ?? base.FileKind;

        public override string? CssScope { get; }

        public override Stream Read() 
            => throw new NotSupportedException("This API should not be invoked. We should instead be relying on " +
                "the RazorSourceDocument associated with this item instead.");
    }
}
