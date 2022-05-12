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
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Constraints
{
    internal class OSConstraintFactory : ITemplateConstraintFactory
    {
        public Guid Id { get; } = Guid.Parse("{73DE9788-264A-427B-A26F-2CA3911EE424}");

        public string Type => "os";

        public Task<ITemplateConstraint> CreateTemplateConstraintAsync(IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult((ITemplateConstraint)new OSConstraint(environmentSettings, this));
        }

        internal class OSConstraint : ITemplateConstraint
        {
            private readonly IEngineEnvironmentSettings _environmentSettings;
            private readonly ITemplateConstraintFactory _factory;

            internal OSConstraint(IEngineEnvironmentSettings environmentSettings, ITemplateConstraintFactory factory)
            {
                _environmentSettings = environmentSettings;
                _factory = factory;
            }

            public string Type => _factory.Type;

            public string DisplayName => "Operating System";

            public TemplateConstraintResult Evaluate(string? args)
            {
                IEnumerable<OSPlatform> supportedOS = ParseArgs(args);

                foreach (OSPlatform platform in supportedOS)
                {
                    if (RuntimeInformation.IsOSPlatform(platform))
                    {
                        return TemplateConstraintResult.CreateAllowed(Type);
                    }
                }
                //TODO: localize
                return TemplateConstraintResult.CreateRestricted(Type, $"Running template on {RuntimeInformation.OSDescription} is not supported, supported OS is/are: {string.Join(", ", supportedOS)}.");
            }

            private static IEnumerable<OSPlatform> ParseArgs(string? args)
            {
                throw new NotImplementedException();
            }
        }
    }
}
