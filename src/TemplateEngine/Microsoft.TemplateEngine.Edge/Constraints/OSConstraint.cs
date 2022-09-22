// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;

namespace Microsoft.TemplateEngine.Edge.Constraints
{
    public sealed class OSConstraintFactory : ITemplateConstraintFactory
    {
        private static readonly Dictionary<string, OSPlatform> PlatformMap = new Dictionary<string, OSPlatform>(StringComparer.OrdinalIgnoreCase)
        {
            { "Windows",  OSPlatform.Windows },
            { "Linux",  OSPlatform.Linux },
            { "OSX",  OSPlatform.OSX }
        };

        Guid IIdentifiedComponent.Id { get; } = Guid.Parse("{73DE9788-264A-427B-A26F-2CA3911EE424}");

        string ITemplateConstraintFactory.Type => "os";

        Task<ITemplateConstraint> ITemplateConstraintFactory.CreateTemplateConstraintAsync(IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult((ITemplateConstraint)new OSConstraint(environmentSettings, this));
        }

        internal class OSConstraint : ConstraintBase
        {
            internal OSConstraint(IEngineEnvironmentSettings environmentSettings, ITemplateConstraintFactory factory)
                : base(environmentSettings, factory)
            { }

            public override string DisplayName => LocalizableStrings.OSConstraint_Name;

            protected override TemplateConstraintResult EvaluateInternal(string? args)
            {
                IEnumerable<OSPlatform> supportedOS = ParseArgs(args);

                foreach (OSPlatform platform in supportedOS)
                {
                    if (RuntimeInformation.IsOSPlatform(platform))
                    {
                        return TemplateConstraintResult.CreateAllowed(this);
                    }
                }
                return TemplateConstraintResult.CreateRestricted(this, string.Format(LocalizableStrings.OSConstraint_Message_Restricted, RuntimeInformation.OSDescription, string.Join(", ", supportedOS)));
            }

            //supported configuration:
            // "args": "Windows"
            // "args": [ "Linux", "Windows" ]
            private static IEnumerable<OSPlatform> ParseArgs(string? args)
            {
                string supportedValues = string.Join(", ", PlatformMap.Keys.Select(e => $"'{e}'"));

                return args.ParseArrayOfConstraintStrings().Select(Parse);

                OSPlatform Parse(string arg)
                {
                    if (PlatformMap.TryGetValue(arg, out OSPlatform parsedValue))
                    {
                        return parsedValue;
                    }
                    throw new ConfigurationException(string.Format(LocalizableStrings.OSConstraint_Error_InvalidOSName, arg, supportedValues));
                }
            }
        }
    }
}
