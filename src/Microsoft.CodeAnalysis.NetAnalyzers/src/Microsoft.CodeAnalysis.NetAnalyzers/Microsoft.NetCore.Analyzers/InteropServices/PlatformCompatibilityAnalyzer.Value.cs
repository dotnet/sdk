// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    using ValueContentAnalysisResult = DataFlowAnalysisResult<ValueContentBlockAnalysisResult, ValueContentAbstractValue>;

    public sealed partial class PlatformCompatibilityAnalyzer
    {
        private readonly struct PlatformMethodValue : IAbstractAnalysisValue, IEquatable<PlatformMethodValue>
        {
            internal PlatformMethodValue(string platformPropertyName, Version version, bool negated)
            {
                PlatformName = platformPropertyName ?? throw new ArgumentNullException(nameof(platformPropertyName));
                Version = version ?? throw new ArgumentNullException(nameof(version));
                Negated = negated;
            }

            public string PlatformName { get; }
            public Version Version { get; }
            public bool Negated { get; }

            public IAbstractAnalysisValue GetNegatedValue()
                => new PlatformMethodValue(PlatformName, Version, !Negated);

            public static bool TryDecode(
                IMethodSymbol invokedPlatformCheckMethod,
                ImmutableArray<IArgumentOperation> arguments,
                ValueContentAnalysisResult? valueContentAnalysisResult,
                INamedTypeSymbol? osPlatformType,
                ArrayBuilder<PlatformMethodValue> infosBuilder)
            {
                // Accelerators like OperatingSystem.IsPlatformName()
                if (arguments.IsEmpty)
                {
                    if (TryExtractPlatformName(invokedPlatformCheckMethod.Name, out var platformName))
                    {
                        var info = new PlatformMethodValue(platformName, EmptyVersion, negated: false);
                        infosBuilder.Add(info);
                        return true;
                    }
                }
                else
                {
                    using var osPlatformNamesBuilder = ArrayBuilder<string>.GetInstance();
                    if (TryDecodeRuntimeInformationIsOSPlatform(arguments[0].Value, osPlatformType, valueContentAnalysisResult, osPlatformNamesBuilder))
                    {
                        Debug.Assert(osPlatformNamesBuilder.Count > 0);
                        for (var i = 0; i < osPlatformNamesBuilder.Count; i++)
                        {
                            var info = new PlatformMethodValue(osPlatformNamesBuilder[i], EmptyVersion, negated: false);
                            infosBuilder.Add(info);
                        }

                        return true;
                    }

                    if (arguments.GetArgumentForParameterAtIndex(0).Value is ILiteralOperation literal)
                    {
                        if (literal.Type?.SpecialType == SpecialType.System_String &&
                            literal.ConstantValue.HasValue)
                        {
                            // OperatingSystem.IsOSPlatform(string platform)
                            if (invokedPlatformCheckMethod.Name == IsOSPlatform &&
                                TryParsePlatformNameAndVersion(literal.ConstantValue.Value.ToString(), out string platformName, out Version? version))
                            {
                                var info = new PlatformMethodValue(platformName, version, negated: false);
                                infosBuilder.Add(info);
                                return true;
                            }
                            else if (TryDecodeOSVersion(arguments, valueContentAnalysisResult, out version, 1))
                            {
                                // OperatingSystem.IsOSPlatformVersionAtLeast(string platform, int major, int minor = 0, int build = 0, int revision = 0)
                                Debug.Assert(invokedPlatformCheckMethod.Name == "IsOSPlatformVersionAtLeast");
                                var info = new PlatformMethodValue(literal.ConstantValue.Value.ToString(), version, negated: false);
                                infosBuilder.Add(info);
                                return true;
                            }
                        }
                        else if (literal.Type?.SpecialType == SpecialType.System_Int32)
                        {
                            // Accelerators like OperatingSystem.IsPlatformNameVersionAtLeast(int major, int minor = 0, int build = 0, int revision = 0)
                            if (TryExtractPlatformName(invokedPlatformCheckMethod.Name, out var platformName) &&
                                TryDecodeOSVersion(arguments, valueContentAnalysisResult, out var version))
                            {
                                var info = new PlatformMethodValue(platformName, version, negated: false);
                                infosBuilder.Add(info);
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            private static bool TryDecodeRuntimeInformationIsOSPlatform(
                IOperation argumentValue,
                INamedTypeSymbol? osPlatformType,
                ValueContentAnalysisResult? valueContentAnalysisResult,
                ArrayBuilder<string> decodedOsPlatformNamesBuilder)
            {
                if (!argumentValue.Type.Equals(osPlatformType))
                {
                    return false;
                }

                if ((argumentValue is IPropertyReferenceOperation propertyReference) &&
                    propertyReference.Property.ContainingType.Equals(osPlatformType))
                {
                    decodedOsPlatformNamesBuilder.Add(propertyReference.Property.Name);
                    return true;
                }

                if (valueContentAnalysisResult != null)
                {
                    var valueContentValue = valueContentAnalysisResult[argumentValue];
                    if (valueContentValue.IsLiteralState)
                    {
                        decodedOsPlatformNamesBuilder.AddRange(valueContentValue.LiteralValues.OfType<string>());
                        return decodedOsPlatformNamesBuilder.Count > 0;
                    }
                }

                return false;
            }

            private static bool TryDecodeOSVersion(
                ImmutableArray<IArgumentOperation> arguments,
                ValueContentAnalysisResult? valueContentAnalysisResult,
                [NotNullWhen(returnValue: true)] out Version? osVersion,
                int skip = 0)
            {

                using var versionBuilder = ArrayBuilder<int>.GetInstance(4, fillWithValue: 0);
                var index = 0;

                foreach (var argument in arguments.GetArgumentsInParameterOrder().Skip(skip))
                {
                    if (!TryDecodeOSVersionPart(argument, valueContentAnalysisResult, out var osVersionPart))
                    {
                        osVersion = null;
                        return false;
                    }

                    versionBuilder[index++] = osVersionPart;
                }

                osVersion = CreateVersion(versionBuilder);
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

                static Version CreateVersion(ArrayBuilder<int> versionBuilder)
                {
                    if (versionBuilder[3] == 0)
                    {
                        if (versionBuilder[2] == 0)
                        {
                            return new Version(versionBuilder[0], versionBuilder[1]);
                        }

                        return new Version(versionBuilder[0], versionBuilder[1], versionBuilder[2]);
                    }

                    return new Version(versionBuilder[0], versionBuilder[1], versionBuilder[2], versionBuilder[3]);
                }
            }

            public override string ToString()
            {
                var result = $"{PlatformName};{Version}";
                if (Negated)
                {
                    result = $"!{result}";
                }

                return result;
            }

            public bool Equals(PlatformMethodValue other)
                => PlatformName.Equals(other.PlatformName, StringComparison.OrdinalIgnoreCase) &&
                   Version.Equals(other.Version) &&
                   Negated == other.Negated;

            public override bool Equals(object obj)
                => obj is PlatformMethodValue otherInfo && Equals(otherInfo);

            public override int GetHashCode()
            {
                return RoslynHashCode.Combine(
                    PlatformName.GetHashCode(),
                    Version.GetHashCode(),
                    Negated.GetHashCode());
            }

            bool IEquatable<IAbstractAnalysisValue>.Equals(IAbstractAnalysisValue other)
                => other is PlatformMethodValue otherInfo && Equals(otherInfo);

            public static bool operator ==(PlatformMethodValue left, PlatformMethodValue right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(PlatformMethodValue left, PlatformMethodValue right)
            {
                return !(left == right);
            }
        }
    }
}