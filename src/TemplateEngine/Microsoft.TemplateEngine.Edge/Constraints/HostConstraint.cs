// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;

namespace Microsoft.TemplateEngine.Edge.Constraints
{
    internal class HostConstraintFactory : ITemplateConstraintFactory
    {
        public Guid Id { get; } = Guid.Parse("{93721B30-6890-403F-BAE7-5925990865A2}");

        public string Type => "host";

        public Task<ITemplateConstraint> CreateTemplateConstraintAsync(IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult((ITemplateConstraint)new HostConstraint(environmentSettings, this));
        }

        internal class HostConstraint : ITemplateConstraint
        {
            private readonly IEngineEnvironmentSettings _environmentSettings;
            private readonly ITemplateConstraintFactory _factory;

            internal HostConstraint(IEngineEnvironmentSettings environmentSettings, ITemplateConstraintFactory factory)
            {
                _environmentSettings = environmentSettings;
                _factory = factory;
            }

            public string Type => _factory.Type;

            public string DisplayName => "Template engine host";

            public TemplateConstraintResult Evaluate(string? args)
            {
                try
                {
                    IEnumerable<HostInformation> supportedHosts = ParseArgs(args);

                    //check primary host name first
                    bool primaryHostNameMatch = false;
                    foreach (HostInformation hostInfo in supportedHosts.Where(h => h.HostName.Equals(_environmentSettings.Host.HostIdentifier, StringComparison.OrdinalIgnoreCase)))
                    {
                        primaryHostNameMatch = true;
                        if (hostInfo.Version == null || hostInfo.Version.CheckIfVersionIsValid(_environmentSettings.Host.Version))
                        {
                            return TemplateConstraintResult.CreateAllowed(Type);
                        }
                    }
                    if (!primaryHostNameMatch)
                    {
                        //if there is no primary host name, check fallback host names
                        foreach (HostInformation hostInfo in supportedHosts.Where(h => _environmentSettings.Host.FallbackHostTemplateConfigNames.Contains(h.HostName, StringComparer.OrdinalIgnoreCase)))
                        {
                            if (hostInfo.Version == null || hostInfo.Version.CheckIfVersionIsValid(_environmentSettings.Host.Version))
                            {
                                return TemplateConstraintResult.CreateAllowed(Type);
                            }
                        }
                    }
                    string errorMessage = string.Format(LocalizableStrings.HostConstraint_Message_Restricted, _environmentSettings.Host.HostIdentifier, _environmentSettings.Host.Version, string.Join(", ", supportedHosts));
                    return TemplateConstraintResult.CreateRestricted(Type, errorMessage);
                }
                catch (ConfigurationException ce)
                {
                    return TemplateConstraintResult.CreateFailure(Type, ce.Message, LocalizableStrings.Generic_Constraint_WrongConfigurationCTA);
                }
            }

            // configuration examples
            // "args": [
            //      {
            //          "hostName": "dotnetcli",
            //          "version": "5.0.100"
            //      },
            //      {
            //          "hostName": "ide",
            //          "version": "[16.0-*]"
            //      }]
            private static IEnumerable<HostInformation> ParseArgs(string? args)
            {
                if (string.IsNullOrWhiteSpace(args))
                {
                    throw new ConfigurationException(LocalizableStrings.HostConstraint_Error_ArgumentsNotSpecified);
                }

                JToken? token;
                try
                {
                    token = JToken.Parse(args!);
                }
                catch (Exception e)
                {
                    throw new ConfigurationException(string.Format(LocalizableStrings.HostConstraint_Error_InvalidJson, args), e);
                }

                if (token is not JArray array)
                {
                    throw new ConfigurationException(string.Format(LocalizableStrings.HostConstraint_Error_InvalidJsonArray, args));
                }

                List<HostInformation> hostInformation = new List<HostInformation>();

                foreach (JToken value in array)
                {
                    if (value is not JObject jobj)
                    {
                        throw new ConfigurationException(string.Format(LocalizableStrings.HostConstraint_Error_InvalidJsonArray_Objects, args));
                    }

                    string? hostName = jobj.ToString("hostname");
                    string? version = jobj.ToString("version");

                    if (string.IsNullOrWhiteSpace(hostName))
                    {
                        throw new ConfigurationException(string.Format(LocalizableStrings.HostConstraint_Error_MissingMandatoryProperty, jobj, "hostname"));
                    }
                    if (string.IsNullOrWhiteSpace(version))
                    {
                        hostInformation.Add(new HostInformation(hostName!));
                        continue;
                    }

                    //check version in the following order:
                    //NuGet exact verion
                    //NuGet floating version
                    //NuGet version range
                    //Legacy template engine exact version
                    //Legacy template engine version range

                    if (NuGetVersionSpecification.TryParse(version!, out NuGetVersionSpecification? exactNuGetVersion))
                    {
                        hostInformation.Add(new HostInformation(hostName!, exactNuGetVersion));
                        continue;
                    }
                    else if (NuGetFloatRangeSpecification.TryParse(version!, out NuGetFloatRangeSpecification? floatVersion))
                    {
                        hostInformation.Add(new HostInformation(hostName!, floatVersion));
                        continue;
                    }
                    else if (NuGetVersionRangeSpecification.TryParse(version!, out NuGetVersionRangeSpecification? rangeNuGetVersion))
                    {
                        hostInformation.Add(new HostInformation(hostName!, rangeNuGetVersion));
                        continue;
                    }
                    else if (ExactVersionSpecification.TryParse(version!, out IVersionSpecification? exactVersion))
                    {
                        hostInformation.Add(new HostInformation(hostName!, exactVersion));
                        continue;
                    }
                    else if (RangeVersionSpecification.TryParse(version!, out IVersionSpecification? rangeVersion))
                    {
                        hostInformation.Add(new HostInformation(hostName!, rangeVersion));
                        continue;
                    }
                    throw new ConfigurationException(string.Format(LocalizableStrings.HostConstraint_Error_InvalidVersion, version));
                }

                if (!hostInformation.Any())
                {
                    throw new ConfigurationException(string.Format(LocalizableStrings.HostConstraint_Error_ArrayHasNoObjects, args));
                }
                return hostInformation;
            }

            private class HostInformation
            {
                public HostInformation(string host, IVersionSpecification? version = null)
                {
                    if (string.IsNullOrWhiteSpace(host))
                    {
                        throw new ArgumentException($"'{nameof(host)}' cannot be null or whitespace.", nameof(host));
                    }

                    HostName = host;
                    Version = version;
                }

                public string HostName { get; }

                public IVersionSpecification? Version { get; }

                public override string ToString()
                {
                    return Version == null
                        ? HostName
                        : $"{HostName}({Version})";
                }
            }
        }
    }
}
