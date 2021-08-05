// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
        private sealed class OperationVisitor : GlobalFlowStateDataFlowOperationVisitor
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

            internal bool TryParseGuardAttributes(ISymbol symbol, ref GlobalFlowStateAnalysisValueSet value)
            {
                var attributes = symbol.GetAttributes();

                if (symbol.GetMemberType()!.SpecialType != SpecialType.System_Boolean ||
                    !HasAnyGuardAttribute(attributes, out var guardAttributes))
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

                value = GlobalFlowStateAnalysisValueSet.Unknown;

                return false;
            }

            private static bool HasAnyGuardAttribute(ImmutableArray<AttributeData> attributes, [NotNullWhen(true)] out SmallDictionary<string, Versions>? mappedAttributes)
            {
                mappedAttributes = null;

                foreach (var attribute in attributes)
                {
                    if (attribute.AttributeClass.Name is SupportedOSPlatformGuardAttribute or UnsupportedOSPlatformGuardAttribute &&
                        TryParsePlatformNameAndVersion(attribute, out var platformName, out var version))
                    {
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
                        for (var i = 0; i < infosBuilder.Count; i++)
                        {
                            var newValue = GlobalFlowStateAnalysisValueSet.Create(infosBuilder[i]);
                            value = i == 0 ? newValue : GlobalFlowStateAnalysis.GlobalFlowStateAnalysisValueSetDomain.Instance.Merge(value, newValue);
                        }

                        infosBuilder.Clear();
                        var attributes = method.GetAttributes();
                        if (HasAnyGuardAttribute(attributes, out var mappedAttributes) && TryDecodeGuardAttributes(mappedAttributes, infosBuilder))
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

                    return GlobalFlowStateAnalysisValueSet.Unknown;
                }
                else if (TryParseGuardAttributes(method, ref value))
                {
                    return value;
                }

                return value;
            }

            public override GlobalFlowStateAnalysisValueSet VisitFieldReference(IFieldReferenceOperation operation, object? argument)
            {
                var value = base.VisitFieldReference(operation, argument);

                if (TryParseGuardAttributes(operation.Field, ref value))
                {
                    return value;
                }

                return ComputeAnalysisValueForReferenceOperation(operation, value);
            }

            public override GlobalFlowStateAnalysisValueSet VisitPropertyReference(IPropertyReferenceOperation operation, object? argument)
            {
                var value = base.VisitPropertyReference(operation, argument);

                if (TryParseGuardAttributes(operation.Property, ref value))
                {
                    return value;
                }

                return ComputeAnalysisValueForReferenceOperation(operation, value);
            }
        }
    }
}
