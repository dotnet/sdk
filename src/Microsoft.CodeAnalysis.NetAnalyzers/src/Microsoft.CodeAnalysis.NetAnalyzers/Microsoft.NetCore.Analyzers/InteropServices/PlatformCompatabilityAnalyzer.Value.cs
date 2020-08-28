// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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

    public sealed partial class PlatformCompatabilityAnalyzer
    {
        private readonly struct PlatformMethodValue : IAbstractAnalysisValue, IEquatable<PlatformMethodValue>
        {
            private PlatformMethodValue(string invokedPlatformCheckMethodName, string platformPropertyName, Version version, bool negated)
            {
                InvokedMethodName = invokedPlatformCheckMethodName ?? throw new ArgumentNullException(nameof(invokedPlatformCheckMethodName));
                PlatformName = platformPropertyName ?? throw new ArgumentNullException(nameof(platformPropertyName));
                Version = version ?? throw new ArgumentNullException(nameof(version));
                Negated = negated;
            }

            public string InvokedMethodName { get; }
            public string PlatformName { get; }
            public Version Version { get; }
            public bool Negated { get; }

            public IAbstractAnalysisValue GetNegatedValue()
                => new PlatformMethodValue(InvokedMethodName, PlatformName, Version, !Negated);

            public static bool TryDecode(
                IMethodSymbol invokedPlatformCheckMethod,
                ImmutableArray<IArgumentOperation> arguments,
                ValueContentAnalysisResult? valueContentAnalysisResult,
                INamedTypeSymbol osPlatformType,
                [NotNullWhen(returnValue: true)] out PlatformMethodValue? info)
            {
                // Accelerators like OperatingSystem.IsPlatformName()
                if (arguments.IsEmpty)
                {
                    if (TryExtractPlatformName(invokedPlatformCheckMethod.Name, out var platformName))
                    {
                        info = new PlatformMethodValue(invokedPlatformCheckMethod.Name, platformName, new Version(0, 0), negated: false);
                        return true;
                    }
                }
                else
                {
                    if (TryDecodeRuntimeInformationIsOSPlatform(arguments[0].Value, osPlatformType, out string? osPlatformName))
                    {
                        info = new PlatformMethodValue(invokedPlatformCheckMethod.Name, osPlatformName, new Version(0, 0), negated: false);
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
                                info = new PlatformMethodValue(invokedPlatformCheckMethod.Name, platformName, version, negated: false);
                                return true;
                            }
                            else if (TryDecodeOSVersion(arguments, valueContentAnalysisResult, out version, 1))
                            {
                                // OperatingSystem.IsOSPlatformVersionAtLeast(string platform, int major, int minor = 0, int build = 0, int revision = 0)
                                Debug.Assert(invokedPlatformCheckMethod.Name == "IsOSPlatformVersionAtLeast");
                                info = new PlatformMethodValue(invokedPlatformCheckMethod.Name, literal.ConstantValue.Value.ToString(), version, negated: false);
                                return true;
                            }
                        }
                        else if (literal.Type?.SpecialType == SpecialType.System_Int32)
                        {
                            // Accelerators like OperatingSystem.IsPlatformNameVersionAtLeast(int major, int minor = 0, int build = 0, int revision = 0)
                            if (TryExtractPlatformName(invokedPlatformCheckMethod.Name, out var platformName) &&
                                TryDecodeOSVersion(arguments, valueContentAnalysisResult, out var version))
                            {
                                info = new PlatformMethodValue(invokedPlatformCheckMethod.Name, platformName, version, negated: false);
                                return true;
                            }
                        }
                    }
                }

                info = default;
                return false;
            }

            private static bool TryExtractPlatformName(string methodName, [NotNullWhen(true)] out string? platformName)
            {
                if (!methodName.StartsWith(IsPrefix, StringComparison.Ordinal))
                {
                    platformName = null;
                    return false;
                }

                if (methodName.EndsWith(OptionalSuffix, StringComparison.Ordinal))
                {
                    platformName = methodName.Substring(2, methodName.Length - 2 - OptionalSuffix.Length);
                    return true;
                }

                platformName = methodName.Substring(2);
                return true;
            }

            private static bool TryDecodeRuntimeInformationIsOSPlatform(
                IOperation argumentValue,
                INamedTypeSymbol osPlatformType,
                [NotNullWhen(returnValue: true)] out string? osPlatformName)
            {
                if ((argumentValue is IPropertyReferenceOperation propertyReference) &&
                    propertyReference.Property.ContainingType.Equals(osPlatformType))
                {
                    osPlatformName = propertyReference.Property.Name;
                    return true;
                }

                osPlatformName = null;
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
                        else
                        {
                            return new Version(versionBuilder[0], versionBuilder[1], versionBuilder[2]);
                        }
                    }
                    else
                    {
                        return new Version(versionBuilder[0], versionBuilder[1], versionBuilder[2], versionBuilder[3]);
                    }
                }
            }

            public override string ToString()
            {
                var result = $"{InvokedMethodName};{PlatformName};{Version}";
                if (Negated)
                {
                    result = $"!{result}";
                }

                return result;
            }

            public bool Equals(PlatformMethodValue other)
                => InvokedMethodName.Equals(other.InvokedMethodName, StringComparison.OrdinalIgnoreCase) &&
                    PlatformName.Equals(other.PlatformName, StringComparison.OrdinalIgnoreCase) &&
                    Version.Equals(other.Version) &&
                    Negated == other.Negated;

            public override bool Equals(object obj)
                => obj is PlatformMethodValue otherInfo && Equals(otherInfo);

            public override int GetHashCode()
                => HashUtilities.Combine(InvokedMethodName.GetHashCode(), PlatformName.GetHashCode(), Version.GetHashCode(), Negated.GetHashCode());

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