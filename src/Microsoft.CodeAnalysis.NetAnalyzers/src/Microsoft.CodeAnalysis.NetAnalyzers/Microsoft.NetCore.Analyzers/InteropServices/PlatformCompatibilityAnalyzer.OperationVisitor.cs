// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    public sealed partial class PlatformCompatibilityAnalyzer
    {
        private sealed class OperationVisitor : GlobalFlowStateValueSetFlowOperationVisitor
        {
            private readonly ImmutableArray<IMethodSymbol> _platformCheckMethods;
            private readonly INamedTypeSymbol? _osPlatformType;
            private readonly SmallDictionary<string, (string relatedPlatform, bool isSubset)> _relatedPlatforms;

            public OperationVisitor(
                ImmutableArray<IMethodSymbol> platformCheckMethods,
                INamedTypeSymbol? osPlatformType,
                SmallDictionary<string, (string relatedPlatform, bool isSubset)> relatedPlatforms,
                GlobalFlowStateAnalysisContext analysisContext)
                : base(analysisContext, hasPredicatedGlobalState: true)
            {
                _platformCheckMethods = platformCheckMethods;
                _osPlatformType = osPlatformType;
                _relatedPlatforms = relatedPlatforms;
            }

            /// <summary>
            /// If the <paramref name="symbol"/> provided annotated with any guard attribute update the <paramref name="value"/> accordingly.
            /// </summary>
            /// <param name="symbol">Symbol for which the attributes will be examined.</param>
            /// <param name="value">Resulting flow analysis value.</param>
            /// <param name="visitedArguments">Arguments passed to the method symbol.</param>
            /// <returns>True if any guard attribute found and <paramref name="value"/> changed accordingly, false otherwise</returns>
            internal bool TryParseGuardAttributes(ISymbol symbol, ref GlobalFlowStateAnalysisValueSet value, ImmutableArray<IArgumentOperation> visitedArguments)
            {
                var attributes = symbol.GetAttributes();

                if (symbol.GetMemberType()!.SpecialType != SpecialType.System_Boolean ||
                    !HasAnyGuardAttribute(attributes, visitedArguments, out var guardAttributes))
                {
                    return false;
                }

                using var infosBuilder = ArrayBuilder<PlatformMethodValue>.GetInstance();
                if (TryDecodeGuardAttributes(guardAttributes, infosBuilder))
                {
                    for (var i = 0; i < infosBuilder.Count; i++)
                    {
                        var newValue = GlobalFlowStateAnalysisValueSet.Create(infosBuilder[i]);
                        // if the incoming value is negated it should be merged with AND logic, else with OR. 
                        value = i == 0 ? newValue : infosBuilder[i].Negated ? value.WithAdditionalAnalysisValues(newValue, false) :
                            GlobalFlowStateAnalysis.GlobalFlowStateAnalysisValueSetDomain.Instance.Merge(value, newValue);
                    }

                    return true;
                }

                return false;
            }

            /// <summary>
            /// Checks if there is any guard attribute within <paramref name="attributes"/>, parse found attributes into platform to version map <paramref name="mappedAttributes"/>
            /// </summary>
            /// <param name="attributes">Attributes to check</param>
            /// <param name="methodArguments">If the symbol is method symbol provide its arguments</param>
            /// <param name="mappedAttributes">Guard attributes parsed into platform to version map</param>
            /// <returns>True if there were any guard attributes found and parsed successfully, false otherwise</returns>
            private static bool HasAnyGuardAttribute(ImmutableArray<AttributeData> attributes, ImmutableArray<IArgumentOperation> methodArguments, [NotNullWhen(true)] out SmallDictionary<string, Versions>? mappedAttributes)
            {
                mappedAttributes = null;

                foreach (var attribute in attributes)
                {
                    if (attribute.AttributeClass == null)
                        continue;

                    if (attribute.AttributeClass.Name is SupportedOSPlatformGuardAttribute or UnsupportedOSPlatformGuardAttribute &&
                        TryParsePlatformNameAndVersion(attribute, out var platformName, out var version))
                    {
                        if (version == EmptyVersion && !methodArguments.IsEmpty &&
                            TryDecodeOSVersion(methodArguments, null, out var apiVersion))
                        {
                            version = apiVersion;
                        }

                        mappedAttributes ??= new(StringComparer.OrdinalIgnoreCase);
                        if (!mappedAttributes.TryGetValue(platformName, out var versions))
                        {
                            versions = new Versions();
                            mappedAttributes.Add(platformName, versions);
                        }

                        if (attribute.AttributeClass.Name == SupportedOSPlatformGuardAttribute)
                        {
                            versions.SupportedFirst = version;
                        }
                        else if (versions.UnsupportedFirst == null)
                        {
                            versions.UnsupportedFirst = version;
                        }
                        else
                        {
                            versions.UnsupportedSecond = version;
                        }
                    }
                }

                return mappedAttributes != null;
            }

            /// <summary>
            /// Convert each platform and versions pair from <paramref name="mappedAttributes"/> map into <see cref="PlatformMethodValue"/>s and add into <paramref name="infosBuilder"/> array
            /// </summary>
            /// <param name="mappedAttributes">Map of platforms to versions populated from guard attributes</param>
            /// <param name="infosBuilder">Converted array of <see cref="PlatformMethodValue"/>s</param>
            /// <returns>True if any <see cref="PlatformMethodValue"/> added into the <paramref name="infosBuilder"/>, false otherwise</returns>
            public bool TryDecodeGuardAttributes(SmallDictionary<string, Versions> mappedAttributes, ArrayBuilder<PlatformMethodValue> infosBuilder)
            {
                foreach (var (name, versions) in mappedAttributes)
                {
                    AddValue(infosBuilder, name, versions);

                    if (_relatedPlatforms.TryGetValue(name, out var relation) && relation.isSubset)
                    {
                        if (mappedAttributes.TryGetValue(relation.relatedPlatform, out var v))
                        {
                            if (v.UnsupportedFirst != null && versions.SupportedFirst == v.UnsupportedFirst && AllowList(versions))
                            {
                                var index = infosBuilder.FindIndex(v => v.PlatformName == relation.relatedPlatform);
                                if (index > -1)
                                {
                                    infosBuilder.RemoveAt(index);
                                }

                                v.SupportedFirst = null;
                                v.UnsupportedFirst = null;
                            }
                        }
                        else
                        {
                            AddValue(infosBuilder, relation.relatedPlatform, versions);
                        }
                    }
                }

                return infosBuilder.Any();
            }

            private static void AddValue(ArrayBuilder<PlatformMethodValue> infosBuilder, string name, Versions versions)
            {
                if (versions.IsSet())
                {
                    if (versions.SupportedFirst != null)
                    {
                        infosBuilder.Add(new PlatformMethodValue(name, versions.SupportedFirst, negated: false));
                    }

                    if (versions.UnsupportedFirst != null)
                    {
                        infosBuilder.Add(new PlatformMethodValue(name, versions.UnsupportedFirst, negated: true));
                        if (versions.UnsupportedSecond != null)
                        {
                            infosBuilder.Add(new PlatformMethodValue(name, versions.UnsupportedSecond, negated: true));
                        }
                    }
                }
            }

            public override GlobalFlowStateAnalysisValueSet VisitInvocation_NonLambdaOrDelegateOrLocalFunction(
                IMethodSymbol method,
                IOperation? visitedInstance,
                ImmutableArray<IArgumentOperation> visitedArguments,
                bool invokedAsDelegate,
                IOperation originalOperation,
                GlobalFlowStateAnalysisValueSet defaultValue)
            {
                var value = base.VisitInvocation_NonLambdaOrDelegateOrLocalFunction(method, visitedInstance, visitedArguments, invokedAsDelegate, originalOperation, defaultValue);

                if (_platformCheckMethods.Contains(method.OriginalDefinition))
                {
                    using var infosBuilder = ArrayBuilder<PlatformMethodValue>.GetInstance();
                    if (PlatformMethodValue.TryDecode(method, visitedArguments, DataFlowAnalysisContext.ValueContentAnalysisResult, _osPlatformType, infosBuilder))
                    {
                        var version = EmptyVersion;
                        for (var i = 0; i < infosBuilder.Count; i++)
                        {
                            var newValue = GlobalFlowStateAnalysisValueSet.Create(infosBuilder[i]);
                            value = i == 0 ? newValue : GlobalFlowStateAnalysis.GlobalFlowStateAnalysisValueSetDomain.Instance.Merge(value, newValue);
                            version = infosBuilder[i].Version;
                        }

                        infosBuilder.Clear();
                        var attributes = method.GetAttributes();
                        if (HasAnyGuardAttribute(attributes, version, out var mappedAttributes) && TryDecodeGuardAttributes(mappedAttributes, infosBuilder))
                        {
                            for (var i = 0; i < infosBuilder.Count; i++)
                            {
                                var newValue = GlobalFlowStateAnalysisValueSet.Create(infosBuilder[i]);
                                // if the incoming value is negated it should be merged with AND logic, else with OR. 
                                value = infosBuilder[i].Negated ? value.WithAdditionalAnalysisValues(newValue, false) :
                                    GlobalFlowStateAnalysis.GlobalFlowStateAnalysisValueSetDomain.Instance.Merge(value, newValue);
                            }
                        }

                        return value;
                    }
                }
                else // Not a known guard method, check if annotated with guard attributes
                {
                    _ = TryParseGuardAttributes(method, ref value, visitedArguments);
                }

                return value;
            }

            private static bool HasAnyGuardAttribute(ImmutableArray<AttributeData> attributes, Version expectedVersion, [NotNullWhen(true)] out SmallDictionary<string, Versions>? mappedAttributes)
            {
                mappedAttributes = null;

                foreach (var attribute in attributes)
                {
                    if (attribute.AttributeClass == null)
                        continue;

                    if (attribute.AttributeClass.Name is SupportedOSPlatformGuardAttribute or UnsupportedOSPlatformGuardAttribute &&
                        TryParsePlatformNameAndVersion(attribute, out var platformName, out var _))
                    {
                        mappedAttributes ??= new(StringComparer.OrdinalIgnoreCase);
                        if (!mappedAttributes.TryGetValue(platformName, out var versions))
                        {
                            versions = new Versions();
                            mappedAttributes.Add(platformName, versions);
                        }

                        if (attribute.AttributeClass.Name == SupportedOSPlatformGuardAttribute)
                        {
                            versions.SupportedFirst = expectedVersion;
                        }
                        else if (versions.UnsupportedFirst == null)
                        {
                            versions.UnsupportedFirst = expectedVersion;
                        }
                        else
                        {
                            versions.UnsupportedSecond = expectedVersion;
                        }
                    }
                }

                return mappedAttributes != null;
            }

            public override GlobalFlowStateAnalysisValueSet VisitFieldReference(IFieldReferenceOperation operation, object? argument)
            {
                var value = base.VisitFieldReference(operation, argument);

                if (TryParseGuardAttributes(operation.Field, ref value, ImmutableArray<IArgumentOperation>.Empty))
                {
                    return value;
                }

                return ComputeAnalysisValueForReferenceOperation(operation, value);
            }

            public override GlobalFlowStateAnalysisValueSet VisitPropertyReference(IPropertyReferenceOperation operation, object? argument)
            {
                var value = base.VisitPropertyReference(operation, argument);

                if (TryParseGuardAttributes(operation.Property, ref value, ImmutableArray<IArgumentOperation>.Empty))
                {
                    return value;
                }

                return ComputeAnalysisValueForReferenceOperation(operation, value);
            }
        }
    }
}
