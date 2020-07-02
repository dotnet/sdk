// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    using ValueContentAnalysisResult = DataFlowAnalysisResult<ValueContentBlockAnalysisResult, ValueContentAbstractValue>;

    public sealed partial class RuntimePlatformCheckAnalyzer
    {
        private struct RuntimeOSPlatformInfo : IAbstractAnalysisValue, IEquatable<RuntimeOSPlatformInfo>
        {
            private RuntimeOSPlatformInfo(string invokedPlatformCheckMethodName, string platformPropertyName, Version version, bool negated)
            {
                InvokedPlatformCheckMethodName = invokedPlatformCheckMethodName ?? throw new ArgumentNullException(nameof(invokedPlatformCheckMethodName));
                PlatformPropertyName = platformPropertyName ?? throw new ArgumentNullException(nameof(platformPropertyName));
                Version = version ?? throw new ArgumentNullException(nameof(version));
                Negated = negated;
            }

            public string InvokedPlatformCheckMethodName { get; }
            public string PlatformPropertyName { get; }
            public Version Version { get; }
            public bool Negated { get; }

            public IAbstractAnalysisValue GetNegatedValue()
                => new RuntimeOSPlatformInfo(InvokedPlatformCheckMethodName, PlatformPropertyName, Version, !Negated);

            public static bool TryDecode(
                IMethodSymbol invokedPlatformCheckMethod,
                ImmutableArray<IArgumentOperation> arguments,
                ValueContentAnalysisResult? valueContentAnalysisResult,
                INamedTypeSymbol osPlatformType,
                [NotNullWhen(returnValue: true)] out RuntimeOSPlatformInfo? info)
            {
                if (!TryDecodeOSPlatform(arguments, osPlatformType, out var osPlatformProperty) ||
                    !TryDecodeOSVersion(arguments, valueContentAnalysisResult, out var osVersion))
                {
                    // Bail out
                    info = default;
                    return false;
                }

                info = new RuntimeOSPlatformInfo(invokedPlatformCheckMethod.Name, osPlatformProperty.Name, osVersion, negated: false);
                return true;
            }

            private static bool TryDecodeOSPlatform(
                ImmutableArray<IArgumentOperation> arguments,
                INamedTypeSymbol osPlatformType,
                [NotNullWhen(returnValue: true)] out IPropertySymbol? osPlatformProperty)
            {
                Debug.Assert(!arguments.IsEmpty);
                return TryDecodeOSPlatform(arguments[0].Value, osPlatformType, out osPlatformProperty);
            }

            private static bool TryDecodeOSPlatform(
                IOperation argumentValue,
                INamedTypeSymbol osPlatformType,
                [NotNullWhen(returnValue: true)] out IPropertySymbol? osPlatformProperty)
            {
                if ((argumentValue is IPropertyReferenceOperation propertyReference) &&
                    propertyReference.Property.ContainingType.Equals(osPlatformType))
                {
                    osPlatformProperty = propertyReference.Property;
                    return true;
                }

                osPlatformProperty = null;
                return false;
            }

            private static bool TryDecodeOSVersion(
                ImmutableArray<IArgumentOperation> arguments,
                ValueContentAnalysisResult? valueContentAnalysisResult,
                [NotNullWhen(returnValue: true)] out Version? osVersion)
            {
                using var versionBuilder = ArrayBuilder<int>.GetInstance(4, fillWithValue: 0);
                var index = 0;
                foreach (var argument in arguments.Skip(1))
                {
                    if (!TryDecodeOSVersionPart(argument, valueContentAnalysisResult, out var osVersionPart))
                    {
                        osVersion = null;
                        return false;
                    }

                    versionBuilder[index++] = osVersionPart;
                }

                osVersion = new Version(versionBuilder[0], versionBuilder[1], versionBuilder[2], versionBuilder[3]);
                return true;

                static bool TryDecodeOSVersionPart(IArgumentOperation argument, ValueContentAnalysisResult? valueContentAnalysisResult, out int osVersionPart)
                {
                    if (argument.Value.ConstantValue.HasValue &&
                        argument.Value.ConstantValue.Value is int versionPart)
                    {
                        osVersionPart = versionPart;
                        return true;
                    }

                    if (valueContentAnalysisResult != null)
                    {
                        var valueContentValue = valueContentAnalysisResult[argument.Value];
                        if (valueContentValue.IsLiteralState &&
                            valueContentValue.LiteralValues.Count == 1 &&
                            valueContentValue.LiteralValues.Single() is int part)
                        {
                            osVersionPart = part;
                            return true;
                        }
                    }

                    osVersionPart = default;
                    return false;
                }
            }

            public override string ToString()
            {
                var versionStr = Version.ToString(fieldCount: GetVersionFieldCount(Version));
                var result = $"{InvokedPlatformCheckMethodName};{PlatformPropertyName};{versionStr}";
                if (Negated)
                {
                    result = $"!{result}";
                }

                return result;

                static int GetVersionFieldCount(Version version)
                {
                    if (version.Revision != 0)
                    {
                        return 4;
                    }

                    if (version.Build != 0)
                    {
                        return 3;
                    }

                    if (version.Minor != 0)
                    {
                        return 2;
                    }

                    return 1;
                }
            }

            public bool Equals(RuntimeOSPlatformInfo other)
                => InvokedPlatformCheckMethodName.Equals(other.InvokedPlatformCheckMethodName, StringComparison.OrdinalIgnoreCase) &&
                    PlatformPropertyName.Equals(other.PlatformPropertyName, StringComparison.OrdinalIgnoreCase) &&
                    Version.Equals(other.Version) &&
                    Negated == other.Negated;

            public override bool Equals(object obj)
                => obj is RuntimeOSPlatformInfo otherInfo && Equals(otherInfo);

            public override int GetHashCode()
                => HashUtilities.Combine(InvokedPlatformCheckMethodName.GetHashCode(), PlatformPropertyName.GetHashCode(), Version.GetHashCode(), Negated.GetHashCode());

            bool IEquatable<IAbstractAnalysisValue>.Equals(IAbstractAnalysisValue other)
                => other is RuntimeOSPlatformInfo otherInfo && Equals(otherInfo);

            public static bool operator ==(RuntimeOSPlatformInfo left, RuntimeOSPlatformInfo right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(RuntimeOSPlatformInfo left, RuntimeOSPlatformInfo right)
            {
                return !(left == right);
            }
        }
    }
}