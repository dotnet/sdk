// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IEnvironment
    {
        string ExpandEnvironmentVariables(string name);

        string GetEnvironmentVariable(string name);

        IReadOnlyDictionary<string, string> GetEnvironmentVariables();

        string NewLine { get; }

        /// <summary>
        /// The width of the console buffer. This is typically the value of <see cref="System.Console.BufferWidth" />.
        /// </summary>
        int ConsoleBufferWidth { get;  }
    }
}
