// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.Invocation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Commands
{
    /// <summary>
    /// Represents an <see cref="ExecutableCommand"/> where the command line arguments are mapped to a model class.
    /// </summary>
    /// <typeparam name="TModel">Type of the model class whose properties represent the arguments.</typeparam>
    internal abstract class ModelBoundExecutableCommand<TModel> : ExecutableCommand where TModel : class
    {
        internal ModelBoundExecutableCommand(ILoggerFactory loggerFactory) : base(loggerFactory) { }

        // @inheritdoc
        public override Task<int> InvokeAsync(InvocationContext context)
        {
            return CommandHandler.Create<TModel, CancellationToken>(ExecuteAsync).InvokeAsync(context);
        }

        /// <summary>
        /// Executes the command with the given input.
        /// </summary>
        /// <param name="args">Arguments for the command.</param>
        protected abstract Task<int> ExecuteAsync(TModel args, CancellationToken cancellationToken = default);
    }
}
