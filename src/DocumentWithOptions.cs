// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.Tools
{
    internal struct DocumentWithOptions
    {
        public Document Document { get; }
        public OptionSet OptionSet { get; }
        public AnalyzerConfigOptions? AnalyzerConfigOptions { get; }

        public DocumentWithOptions(Document document, OptionSet optionSet, AnalyzerConfigOptions? analyzerConfigOptions)
        {
            Document = document;
            OptionSet = optionSet;
            AnalyzerConfigOptions = analyzerConfigOptions;
        }

        public void Deconstruct(
            out Document document,
            out OptionSet optionSet,
            out AnalyzerConfigOptions? analyzerConfigOptions)
        {
            document = Document;
            optionSet = OptionSet;
            analyzerConfigOptions = AnalyzerConfigOptions;
        }
    }
}
