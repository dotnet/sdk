// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Constraints
{
    internal class OSConstraintFactory : ITemplateConstraintFactory
    {
        private static readonly Dictionary<string, OSPlatform> _platformMap = new Dictionary<string, OSPlatform>(StringComparer.OrdinalIgnoreCase)
        {
            { "Windows",  OSPlatform.Windows },
            { "Linux",  OSPlatform.Linux },
            { "OSX",  OSPlatform.OSX }
        };

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

            public string DisplayName => LocalizableStrings.OSConstraint_Name;

            public TemplateConstraintResult Evaluate(string? args)
            {
                try
                {
                    IEnumerable<OSPlatform> supportedOS = ParseArgs(args);

                    foreach (OSPlatform platform in supportedOS)
                    {
                        if (RuntimeInformation.IsOSPlatform(platform))
                        {
                            return TemplateConstraintResult.CreateAllowed(Type);
                        }
                    }
                    return TemplateConstraintResult.CreateRestricted(Type, string.Format(LocalizableStrings.OSConstraint_Message_Restricted, RuntimeInformation.OSDescription, string.Join(", ", supportedOS)));
                }
                catch (ConfigurationException ce)
                {
                    return TemplateConstraintResult.CreateFailure(Type, ce.Message, LocalizableStrings.Constraint_WrongConfigurationCTA);
                }
            }

            //supported configuration:
            // "args": "Windows"
            // "args": [ "Linux", "Windows" ]
            private static IEnumerable<OSPlatform> ParseArgs(string? args)
            {
                string supportedValues = string.Join(", ", _platformMap.Keys.Select(e => $"'{e}'"));
                if (string.IsNullOrWhiteSpace(args))
                {
                    throw new ConfigurationException(LocalizableStrings.Constraint_Error_ArgumentsNotSpecified);
                }

                JToken? token;
                try
                {
                    token = JToken.Parse(args!);
                }
                catch (Exception e)
                {
                    throw new ConfigurationException(string.Format(LocalizableStrings.Constraint_Error_InvalidJson, args), e);
                }

                if (token!.Type == JTokenType.String)
                {
                    return new[] { Parse(token.Value<string>()) };
                }
                else if (token is JArray jArray)
                {
                    IEnumerable<string?> values = jArray.Values<string>();
                    List<OSPlatform> readValues = new List<OSPlatform>();
                    foreach (string? value in values)
                    {
                        readValues.Add(Parse(value));
                    }
                    if (!readValues.Any())
                    {
                        throw new ConfigurationException(string.Format(LocalizableStrings.Constraint_Error_ArrayHasNoObjects, args));
                    }
                    return readValues;
                }
                throw new ConfigurationException(string.Format(LocalizableStrings.OSConstraint_Error_InvalidJsonType, args));
                OSPlatform Parse(string? arg)
                {
                    string value = arg ?? throw new ConfigurationException(string.Format(LocalizableStrings.OSConstraint_Error_InvalidOSName, arg, supportedValues));
                    if (_platformMap.TryGetValue(value, out OSPlatform parsedValue))
                    {
                        return parsedValue;
                    }
                    throw new ConfigurationException(string.Format(LocalizableStrings.OSConstraint_Error_InvalidOSName, value, supportedValues));
                }
            }
        }
    }
}
