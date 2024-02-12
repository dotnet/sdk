// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Microsoft.TemplateEngine.Authoring.CLI.Commands
{
    /// <summary>
    /// Represents a <see cref="CliCommand"/> together with its action.
    /// </summary>
    internal abstract class ExecutableCommand<TModel> : CliCommand where TModel : class
    {
        internal ExecutableCommand(string name, string? description = null)
            : base(name, description)
        {
            Action = new CommandAction(this);
        }

        /// <summary>
        /// Parses the context from <see cref="ParseResult"/>.
        /// </summary>
        protected internal abstract TModel ParseContext(ParseResult parseResult);

        /// <summary>
        /// Executes the command on the parsed context.
        /// </summary>
        protected abstract Task<int> ExecuteAsync(TModel args, ILoggerFactory loggerFactory, CancellationToken cancellationToken);

        private sealed class CommandAction : AsynchronousCliAction
        {
            private readonly ExecutableCommand<TModel> _command;

            public CommandAction(ExecutableCommand<TModel> command) => _command = command;

            public int Invoke(ParseResult parseResult) => InvokeAsync(parseResult).GetAwaiter().GetResult();

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
            {
                using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(c => c.ColorBehavior = LoggerColorBehavior.Disabled));
                TModel arguments = _command.ParseContext(parseResult);

                //exceptions are handled by parser itself
                return await _command.ExecuteAsync(arguments, loggerFactory, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
