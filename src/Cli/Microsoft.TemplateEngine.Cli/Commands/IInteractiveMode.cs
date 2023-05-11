// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.Invocation;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal interface IInteractiveMode
    {
        public Task<Questionnaire> GetQuestionsAsync(InvocationContext context, CancellationToken cancellationToken);
    }

    internal class Questionnaire
    {
        internal static Questionnaire Empty { get; } = new Questionnaire();

        internal string? OpeningMessage { get; init; }

        internal IEnumerable<UserQuery> Questions { get; init; } = Array.Empty<UserQuery>();
    }

    internal class UserQuery
    {
        private string parameterName;
        private string parameterMessage;

        public UserQuery(string name, string message)
        {
            parameterName = name;
            parameterMessage = message;
        }

        public string GetQuery()
        {
            return parameterMessage;
        }

        public string GetParameterName()
        {
            return parameterName;
        }
    }
}
