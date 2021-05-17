// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Provides access to environment settings, such as environment variables and constants.
    /// </summary>
    public interface IEnvironment
    {
        /// <summary>
        /// The newline character(s).
        /// </summary>
        string NewLine { get; }

        /// <summary>
        /// The width of the console buffer. This is typically the value of <see cref="System.Console.BufferWidth" />.
        /// </summary>
        int ConsoleBufferWidth { get; }

        /// <summary>
        /// Replaces the name of each environment variable embedded in the specified string with the string equivalent of the value of the variable, then returns the resulting string. Equivalent to <see cref="Enrionment.ExpandEnvironmentVariables(String)"/>.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        string ExpandEnvironmentVariables(string name);

        /// <summary>
        /// Gets the value of environment variable with the <paramref name="name"/>.
        /// </summary>
        /// <param name="name">Name of the environment variable to get.</param>
        /// <returns>The value of environment variable or null if environment variable doesn't exist.</returns>
        string? GetEnvironmentVariable(string name);

        /// <summary>
        /// Gets all environment variables and their values.
        /// </summary>
        /// <returns>The dictionary with environment variable names and their values.</returns>
        IReadOnlyDictionary<string, string> GetEnvironmentVariables();
    }
}
