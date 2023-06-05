// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Util
{
    public class EngineConfig : IEngineConfig
    {
        public EngineConfig(ILogger logger, IVariableCollection variables)
             : this(logger, DefaultWhitespaces, DefaultLineEndings, variables)
        {
        }

        public EngineConfig(ILogger logger, IVariableCollection variables, string variableFormatString)
            : this(logger, DefaultWhitespaces, DefaultLineEndings, variables, variableFormatString)
        {
        }

        public EngineConfig(ILogger logger, IReadOnlyList<string> whitespaces, IReadOnlyList<string> lineEndings, IVariableCollection variables, string variableFormatString = "{0}")
        {
            Logger = logger;
            Whitespaces = whitespaces;
            LineEndings = lineEndings;
            Variables = variables;
            VariableFormatString = variableFormatString;
            Flags = new Dictionary<string, bool>();
        }

        public static IReadOnlyList<string> DefaultLineEndings { get; set; } = new[] { "\r", "\n", "\r\n" };

        public static IReadOnlyList<string> DefaultWhitespaces { get; set; } = new[] { " ", "\t" };

        public IReadOnlyList<string> LineEndings { get; }

        public string VariableFormatString { get; }

        public IVariableCollection Variables { get; }

        public IReadOnlyList<string> Whitespaces { get; }

        public IDictionary<string, bool> Flags { get; }

        public ILogger Logger { get; }
    }
}
