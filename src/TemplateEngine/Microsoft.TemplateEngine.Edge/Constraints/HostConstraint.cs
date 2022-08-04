// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Constraints
{
    public sealed class HostConstraintFactory : ITemplateConstraintFactory
    {
        Guid IIdentifiedComponent.Id { get; } = Guid.Parse("{93721B30-6890-403F-BAE7-5925990865A2}");

        string ITemplateConstraintFactory.Type => "host";

        Task<ITemplateConstraint> ITemplateConstraintFactory.CreateTemplateConstraintAsync(IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult((ITemplateConstraint)new HostConstraint(environmentSettings, this));
        }

        internal class HostConstraint : ConstraintBase
        {
            internal HostConstraint(IEngineEnvironmentSettings environmentSettings, ITemplateConstraintFactory factory)
                : base(environmentSettings, factory)
            { }

            public override string DisplayName => LocalizableStrings.HostConstraint_Name;

            protected override TemplateConstraintResult EvaluateInternal(string? args)
            {
                IReadOnlyList<HostInformation> supportedHosts = ParseArgs(args).ToList();

                //check primary host name first
                bool primaryHostNameMatch = false;
                foreach (HostInformation hostInfo in supportedHosts.Where(h => h.HostName.Equals(EnvironmentSettings.Host.HostIdentifier, StringComparison.OrdinalIgnoreCase)))
                {
                    primaryHostNameMatch = true;
                    if (hostInfo.Version == null || hostInfo.Version.CheckIfVersionIsValid(EnvironmentSettings.Host.Version))
                    {
                        return TemplateConstraintResult.CreateAllowed(this);
                    }
                }
                if (!primaryHostNameMatch)
                {
                    //if there is no primary host name, check fallback host names
                    foreach (HostInformation hostInfo in supportedHosts.Where(h => EnvironmentSettings.Host.FallbackHostTemplateConfigNames.Contains(h.HostName, StringComparer.OrdinalIgnoreCase)))
                    {
                        if (hostInfo.Version == null || hostInfo.Version.CheckIfVersionIsValid(EnvironmentSettings.Host.Version))
                        {
                            return TemplateConstraintResult.CreateAllowed(this);
                        }
                    }
                }
                string errorMessage = string.Format(LocalizableStrings.HostConstraint_Message_Restricted, EnvironmentSettings.Host.HostIdentifier, EnvironmentSettings.Host.Version, supportedHosts.ToCsvString());
                return TemplateConstraintResult.CreateRestricted(this, errorMessage);
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
                List<HostInformation> hostInformation = new List<HostInformation>();

                foreach (JObject jobj in args.ParseArrayOfConstraintJObjects())
                {
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

                    hostInformation.Add(new HostInformation(hostName!, version!.ParseVersionSpecification()));
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
