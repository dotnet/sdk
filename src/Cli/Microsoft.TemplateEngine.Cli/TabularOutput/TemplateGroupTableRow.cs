// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli.TabularOutput
{
    /// <summary>
    /// Represents a table row for template group display.
    /// </summary>
    public struct TemplateGroupTableRow
    {
        public string Author { get; set; }

        public string Classifications { get; set; }

        public string Languages { get; set; }

        public string Name { get; set; }

        public string ShortNames { get; set; }

        public string Type { get; set; }
    }
}
