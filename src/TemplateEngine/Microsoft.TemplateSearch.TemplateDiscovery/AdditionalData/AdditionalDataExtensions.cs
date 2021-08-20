// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common.Abstractions;

namespace Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData
{
    internal static class AdditionalDataExtensions
    {
        internal static IDictionary<string, object>? ProduceAdditionalData (this ITemplatePackageInfo package, IReadOnlyList<IAdditionalDataProducer> producers)
        {
            Dictionary<string, object>? additionalData = new Dictionary<string, object>();
            foreach (var producer in producers)
            {
                try
                {
                    var data = producer.CreateDataForTemplatePackage(package);
                    if (data != null)
                    {
                        additionalData[producer.DataUniqueName] = data;
                    }
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Verbose.WriteLine($"Failed to produce additional data for {package.Name}::{package.Version} with producer {producer.DataUniqueName}, details: {ex}");
                }
            }
            if (additionalData.Any())
            {
                return additionalData;
            }
            return null;
        }

        internal static IDictionary<string, object>? ProduceAdditionalData(this ITemplateInfo template, IReadOnlyList<IAdditionalDataProducer> producers, IEngineEnvironmentSettings environmentSettings)
        {
            Dictionary<string, object>? additionalData = new Dictionary<string, object>();
            foreach (var producer in producers)
            {
                try
                {
                    var data = producer.CreateDataForTemplate(template, environmentSettings);
                    if (data != null)
                    {
                        additionalData[producer.DataUniqueName] = data;
                    }
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Verbose.WriteLine($"Failed to produce additional data for {template.Name} with producer {producer.DataUniqueName}, details: {ex}");
                }
            }
            if (additionalData.Any())
            {
                return additionalData;
            }
            return null;
        }
    }
}
