// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.CommandUtils.Assertions
{
    public static class CommandResultExtensions
    {
        public static CommandResultAssertions Should(this CommandResult commandResult)
        {
            return new CommandResultAssertions(commandResult);
        }
    }
}
