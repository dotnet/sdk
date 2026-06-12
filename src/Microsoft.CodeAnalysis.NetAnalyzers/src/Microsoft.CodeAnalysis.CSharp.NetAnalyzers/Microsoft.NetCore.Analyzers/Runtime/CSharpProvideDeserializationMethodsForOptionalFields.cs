// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    /// <summary>
    /// CA2239: Provide deserialization methods for optional fields
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpProvideDeserializationMethodsForOptionalFieldsAnalyzer : ProvideDeserializationMethodsForOptionalFieldsAnalyzer
    {
    }
}