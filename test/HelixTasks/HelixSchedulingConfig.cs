// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Xml.Linq;

namespace Microsoft.DotNet.SdkCustomHelix.Sdk;

/// <summary>
/// Loads per-assembly scheduling hints from the embedded HelixSchedulingConfig.xml resource.
/// </summary>
internal sealed class HelixSchedulingConfig
{
    private const double DefaultEstimatedSecondsPerMethod = 2.0;
    private const double DefaultMethodLimitMultiplier = 1.0;

    private readonly Dictionary<string, AssemblyConfig> _configs;

    private HelixSchedulingConfig(Dictionary<string, AssemblyConfig> configs)
    {
        _configs = configs;
    }

    internal readonly struct AssemblyConfig
    {
        internal readonly double MethodLimitMultiplier;
        internal readonly double EstimatedSecondsPerMethod;

        internal AssemblyConfig(double methodLimitMultiplier, double estimatedSecondsPerMethod)
        {
            MethodLimitMultiplier = methodLimitMultiplier;
            EstimatedSecondsPerMethod = estimatedSecondsPerMethod;
        }
    }

    /// <summary>
    /// Loads the config from the embedded XML resource.
    /// </summary>
    internal static HelixSchedulingConfig Load()
    {
        var configs = new Dictionary<string, AssemblyConfig>(StringComparer.OrdinalIgnoreCase);

        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("HelixSchedulingConfig.xml");

        if (stream is null)
        {
            return new HelixSchedulingConfig(configs);
        }

        var doc = XDocument.Load(stream);
        foreach (var element in doc.Root!.Elements("Assembly"))
        {
            var name = element.Attribute("Name")?.Value;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var multiplier = DefaultMethodLimitMultiplier;
            if (double.TryParse(element.Attribute("MethodLimitMultiplier")?.Value, out var m))
            {
                multiplier = m;
            }

            var secondsPerMethod = DefaultEstimatedSecondsPerMethod;
            if (double.TryParse(element.Attribute("EstimatedSecondsPerMethod")?.Value, out var s))
            {
                secondsPerMethod = s;
            }

            configs[name] = new AssemblyConfig(multiplier, secondsPerMethod);
        }

        return new HelixSchedulingConfig(configs);
    }

    /// <summary>
    /// Gets the method limit multiplier for an assembly (defaults to 1.0).
    /// </summary>
    internal double GetMethodLimitMultiplier(string assemblyName)
    {
        return _configs.TryGetValue(assemblyName, out var config)
            ? config.MethodLimitMultiplier
            : DefaultMethodLimitMultiplier;
    }

    /// <summary>
    /// Gets the estimated seconds per method for an assembly (defaults to 2.0).
    /// </summary>
    internal double GetEstimatedSecondsPerMethod(string assemblyName)
    {
        return _configs.TryGetValue(assemblyName, out var config)
            ? config.EstimatedSecondsPerMethod
            : DefaultEstimatedSecondsPerMethod;
    }

    /// <summary>
    /// Estimates the duration of a work item based on its method count and assembly.
    /// </summary>
    internal double EstimateDuration(string assemblyName, int methodCount)
    {
        return methodCount * GetEstimatedSecondsPerMethod(assemblyName);
    }
}
