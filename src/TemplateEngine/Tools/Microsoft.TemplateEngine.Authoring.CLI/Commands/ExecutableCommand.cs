// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Logging;

namespace Microsoft.TemplateEngine.Authoring.CLI.Commands
{
    /// <summary>
    /// Represents a <see cref="Command"/> together with its handler.
    /// </summary>
    internal abstract class ExecutableCommand<TModel> : Command, ICommandHandler where TModel : class
    {
        internal ExecutableCommand(string name, string? description = null)
            : base(name, description)
        {
            Handler = this;
        }

        /// <inheritdoc/>
        public async Task<int> InvokeAsync(InvocationContext context)
        {
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            TModel arguments = ParseContext(context.ParseResult);

            //exceptions are handled by parser itself
            return await ExecuteAsync(arguments, loggerFactory, context.GetCancellationToken()).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public int Invoke(InvocationContext context) => InvokeAsync(context).GetAwaiter().GetResult();

        /// <summary>
        /// Parses the context from <see cref="ParseResult"/>.
        /// </summary>
        protected internal abstract TModel ParseContext(ParseResult parseResult);

        /// <summary>
        /// Executes the command on the parsed context.
        /// </summary>
        protected abstract Task<int> ExecuteAsync(TModel args, ILoggerFactory loggerFactory, CancellationToken cancellationToken);

    }
}
