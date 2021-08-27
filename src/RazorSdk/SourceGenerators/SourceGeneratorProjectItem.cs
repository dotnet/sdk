// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    internal class SourceGeneratorProjectItem : RazorProjectItem, IEquatable<SourceGeneratorProjectItem>
    {
        private readonly string _fileKind;

        public SourceGeneratorProjectItem(string basePath, string filePath, string relativePhysicalPath, string fileKind, AdditionalText additionalText, string? cssScope)
        {
            BasePath = basePath;
            FilePath = filePath;
            RelativePhysicalPath = relativePhysicalPath;
            _fileKind = fileKind;
            AdditionalText = additionalText;
            CssScope = cssScope;
            var text = AdditionalText.GetText();
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

        public bool Equals(SourceGeneratorProjectItem other) => AdditionalText == other.AdditionalText;

        public override int GetHashCode() => AdditionalText.GetHashCode();

        public override bool Equals(object obj) => obj is SourceGeneratorProjectItem projectItem && Equals(projectItem);
    }
}
