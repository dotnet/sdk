// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    internal class SourceGeneratorProjectItem : RazorProjectItem
    {
        private static readonly ConcurrentDictionary<string, SourceText> _sourceTextCache = new();

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
            var text = GetSourceTextFromAdditionalFile(additionalText, context, relativePhysicalPath, filePath);
            if (text is not null)
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

        private static SourceText? GetSourceTextFromAdditionalFile(AdditionalText additionalText, GeneratorExecutionContext context, string relativePhysicalPath, string filePath)
        {
            SourceText? text = null;
            if (!_sourceTextCache.TryGetValue(additionalText.Path, out text))
            {
                text = additionalText.GetText();
                if (text is not null)
                {
                    _sourceTextCache[additionalText.Path] = text;
                }
                
            }

            if (text is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(RazorDiagnostics.SourceTextNotFoundDescriptor, Location.None, filePath));
            }
            
            return text;
        }
    }
}
