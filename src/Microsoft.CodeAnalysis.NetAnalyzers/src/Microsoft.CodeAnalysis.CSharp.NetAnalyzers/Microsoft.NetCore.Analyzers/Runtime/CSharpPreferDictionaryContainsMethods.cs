// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpPreferDictionaryContainsMethods : PreferDictionaryContainsMethods
    {
        private protected override bool TryGetPropertyReferenceOperation(IInvocationOperation containsInvocation, [NotNullWhen(true)] out IPropertySymbol? propertySymbol)
        {
            IMethodSymbol method = containsInvocation.TargetMethod;
            IOperation? receiver = null;

            //  In C#, the "this" argument is included in the argument list for extension methods.
            if (method.IsExtensionMethod && method.Parameters.Length == 2)
            {
                receiver = containsInvocation.Arguments[0].Value;
            }
            else if (method.Parameters.Length == 1)
            {
                receiver = containsInvocation.Instance;
            }

            //  The receiver may be a conversion operation if the invocation is an extension method.
            receiver = receiver is IConversionOperation conversion ? conversion.Operand : receiver;
            propertySymbol = (receiver as IPropertyReferenceOperation)?.Property;

            return propertySymbol is not null;
        }
    }
}
