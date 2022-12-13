// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge
{
    /// <summary>
    /// Default implementation of <see cref="IEnvironment"/>.
    /// Gets environment variables from <see cref="Environment"/>.
    /// </summary>
    public class DefaultEnvironment : IEnvironment
    {
        private const int DefaultBufferWidth = 160;
        private readonly IReadOnlyDictionary<string, string> _environmentVariables;

        public DefaultEnvironment() : this(FetchEnvironmentVariables())
        { }

        protected DefaultEnvironment(IReadOnlyDictionary<string, string> environmentVariables)
        {
            _environmentVariables = environmentVariables;
            NewLine = Environment.NewLine;
        }

        /// <inheritdoc/>
        public string NewLine { get; }

        /// <inheritdoc/>
        // Console.BufferWidth can throw if there's no console, such as when output is redirected, so
        // first check if it is redirected, and fall back to a default value if needed.
        public int ConsoleBufferWidth => Console.IsOutputRedirected ? DefaultBufferWidth : Console.BufferWidth;

        /// <inheritdoc/>
        public string ExpandEnvironmentVariables(string name)
        {
            return Environment.ExpandEnvironmentVariables(name);
        }

        /// <inheritdoc/>
        public string? GetEnvironmentVariable(string name)
        {
            _environmentVariables.TryGetValue(name, out string? result);
            return result;
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, string> GetEnvironmentVariables()
        {
            return _environmentVariables;
        }

        protected static IReadOnlyDictionary<string, string> FetchEnvironmentVariables()
        {
            Dictionary<string, string> variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            IDictionary env = Environment.GetEnvironmentVariables();

            foreach (string key in env.Keys.OfType<string>())
            {
                variables[key] = (env[key] as string) ?? string.Empty;
            }

            return variables;
        }
    }
}
