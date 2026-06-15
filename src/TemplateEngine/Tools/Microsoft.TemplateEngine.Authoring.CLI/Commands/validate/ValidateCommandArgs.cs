// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Authoring.CLI.Commands
{
    internal class ValidateCommandArgs
    {
        public ValidateCommandArgs(string templateLocation)
        {
            TemplateLocation = templateLocation;
        }

        public string TemplateLocation { get; }
    }
}
