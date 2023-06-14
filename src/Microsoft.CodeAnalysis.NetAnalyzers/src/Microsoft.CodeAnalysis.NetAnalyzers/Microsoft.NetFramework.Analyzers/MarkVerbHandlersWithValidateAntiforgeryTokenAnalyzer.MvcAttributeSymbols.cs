// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;

namespace Microsoft.NetFramework.Analyzers
{
    public partial class MarkVerbHandlersWithValidateAntiforgeryTokenAnalyzer
    {
        /// <summary>
        /// Helper for examining System.Web.Mvc attributes on MVC controller methods.
        /// </summary>
        private sealed class MvcAttributeSymbols(Compilation compilation)
        {
            private INamedTypeSymbol? ValidateAntiforgeryTokenAttributeSymbol { get; set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcValidateAntiForgeryTokenAttribute);
            private INamedTypeSymbol? HttpGetAttributeSymbol { get; set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcHttpGetAttribute);
            private INamedTypeSymbol? HttpPostAttributeSymbol { get; set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcHttpPostAttribute);
            private INamedTypeSymbol? HttpPutAttributeSymbol { get; set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcHttpPutAttribute);
            private INamedTypeSymbol? HttpDeleteAttributeSymbol { get; set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcHttpDeleteAttribute);
            private INamedTypeSymbol? HttpPatchAttributeSymbol { get; set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcHttpPatchAttribute);
            private INamedTypeSymbol? AcceptVerbsAttributeSymbol { get; set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcAcceptVerbsAttribute);
            private INamedTypeSymbol? NonActionAttributeSymbol { get; set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcNonActionAttribute);
            private INamedTypeSymbol? ChildActionOnlyAttributeSymbol { get; set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcChildActionOnlyAttribute);
            private INamedTypeSymbol? HttpVerbsSymbol { get; set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcHttpVerbs);

            /// <summary>
            /// Gets relevant info from the attributes on the MVC controller action we're examining.
            /// </summary>
            /// <param name="attributeDatas">Attributes on the MVC controller action.</param>
            /// <param name="verbs">Information on which HTTP verbs are specified.</param>
            /// <param name="antiforgeryTokenDefined">Indicates that the ValidateAntiforgeryToken attribute was specified.</param>
            /// <param name="isAction">Indicates that the MVC controller method doesn't have an attribute saying it's not really an action.</param>
            public void ComputeAttributeInfo(
                ImmutableArray<AttributeData> attributeDatas,
                out MvcHttpVerbs verbs,
                out bool antiforgeryTokenDefined,
                out bool isAction)
            {
                verbs = default;
                antiforgeryTokenDefined = false;
                isAction = true;    // Presumed an MVC controller action until proven otherwise.

                foreach (AttributeData a in attributeDatas)
                {
                    if (IsAttributeClass(a, this.ValidateAntiforgeryTokenAttributeSymbol))
                    {
                        antiforgeryTokenDefined = true;
                    }
                    else if (IsAttributeClass(a, this.HttpGetAttributeSymbol))
                    {
                        verbs |= MvcHttpVerbs.Get;
                    }
                    else if (IsAttributeClass(a, this.HttpPostAttributeSymbol))
                    {
                        verbs |= MvcHttpVerbs.Post;
                    }
                    else if (IsAttributeClass(a, this.HttpPutAttributeSymbol))
                    {
                        verbs |= MvcHttpVerbs.Put;
                    }
                    else if (IsAttributeClass(a, this.HttpDeleteAttributeSymbol))
                    {
                        verbs |= MvcHttpVerbs.Delete;
                    }
                    else if (IsAttributeClass(a, this.HttpPatchAttributeSymbol))
                    {
                        verbs |= MvcHttpVerbs.Patch;
                    }
                    else if (IsAttributeClass(a, this.AcceptVerbsAttributeSymbol))
                    {
                        if (a.AttributeConstructor.Parameters.Length == 1)
                        {
                            ITypeSymbol parameterType = a.AttributeConstructor.Parameters[0].Type;
                            if (a.AttributeConstructor.Parameters[0].IsParams
                                && parameterType.TypeKind == TypeKind.Array)
                            {
                                IArrayTypeSymbol parameterArrayType = (IArrayTypeSymbol)parameterType;
                                if (parameterArrayType.Rank == 1
                                    && parameterArrayType.ElementType.SpecialType == SpecialType.System_String)
                                {
                                    // The [AcceptVerbs("Put", "Post")] case.

                                    foreach (TypedConstant tc in a.ConstructorArguments[0].Values)
                                    {
                                        if (tc.Value is string s && Enum.TryParse(s, true /* ignoreCase */, out MvcHttpVerbs v))
                                        {
                                            verbs |= v;
                                        }
                                    }

                                    continue;
                                }
                            }
                            else if (parameterType.TypeKind == TypeKind.Enum
                                && Equals(parameterType, this.HttpVerbsSymbol))
                            {
                                // The [AcceptVerbs(HttpVerbs.Delete)] case.

                                int i = (int)a.ConstructorArguments[0].Value;
                                verbs |= (MvcHttpVerbs)i;

                                continue;
                            }
                        }

                        // If we reach here, then we didn't handle the [AcceptVerbs] constructor overload.
                    }
                    else if (IsAttributeClass(a, this.NonActionAttributeSymbol)
                        || IsAttributeClass(a, this.ChildActionOnlyAttributeSymbol))
                    {
                        isAction = false;
                    }
                }
            }

            /// <summary>
            /// Determines if the .NET attribute is of the specified type.
            /// </summary>
            /// <param name="attributeData">The .NET attribute to check.</param>
            /// <param name="symbol">The type of .NET attribute to compare.</param>
            /// <returns>True if .NET attribute's type matches the specified type, false otherwise.</returns>
            private static bool IsAttributeClass(AttributeData attributeData, [NotNullWhen(returnValue: true)] INamedTypeSymbol? symbol)
            {
                return symbol != null && Equals(attributeData.AttributeClass, symbol);
            }
        }
    }
}
