// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Core.Matching
{
    public class TerminalLocation<T>
        where T : TerminalBase
    {
        public TerminalLocation(T terminal, int location)
        {
            Terminal = terminal;
            Location = location;
        }

        /// <summary>
        /// Start position of the terminal. Relative location of matching token is defined in <see cref="TerminalBase"/>.
        /// </summary>
        public int Location { get; set; }

        public T Terminal { get; }
    }
}
