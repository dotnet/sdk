// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge
{
    /// <summary>
    /// Manages evaluation of constraints for the templates.
    /// </summary>
    public class TemplateConstraintManager : IDisposable
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Dictionary<string, Task<ITemplateConstraint>> _templateConstrains = new Dictionary<string, Task<ITemplateConstraint>>();

        public TemplateConstraintManager(IEngineEnvironmentSettings engineEnvironmentSettings)
        {
            var constraintFactories = engineEnvironmentSettings.Components.OfType<ITemplateConstraintFactory>();
            foreach (var constraintFactory in constraintFactories)
            {
                _templateConstrains[constraintFactory.Type] = Task.Run (() => constraintFactory.CreateTemplateConstraintAsync(engineEnvironmentSettings, _cancellationTokenSource.Token));
            }
            _engineEnvironmentSettings = engineEnvironmentSettings;
        }

        /// <summary>
        /// Returns the list of initialized <see cref="ITemplateConstraint"/>s.
        /// Only returns the list of <see cref="ITemplateConstraint"/> that were initialized successfully.
        /// The constraints which failed to be initialized are skipped and warning is logged.
        /// </summary>
        /// <param name="templates">if given, only returns the list of constraints defined in the templates.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The list of successfully initialized <see cref="ITemplateConstraint"/>s.</returns>
        public async Task<IReadOnlyList<ITemplateConstraint>> GetConstraintsAsync(IReadOnlyList<ITemplateInfo>? templates = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerable<(string Type, Task<ITemplateConstraint> Task)> constraintsToInitialize;
            if (templates?.Any() ?? false)
            {
                List<string> uniqueConstraints = templates.SelectMany(ti => ti.Constraints.Select(c => c.Type)).Distinct().ToList();
                constraintsToInitialize = _templateConstrains.Where(kvp => uniqueConstraints.Contains(kvp.Key)).Select(kvp => (kvp.Key, kvp.Value));
            }
            else
            {
                constraintsToInitialize = _templateConstrains.Select(kvp => (kvp.Key, kvp.Value));
            }

            try
            {
                await CancellableWhenAll(constraintsToInitialize.Select(c => c.Task), cancellationToken).ConfigureAwait(false);
                return constraintsToInitialize.Select(c => c.Task.Result).ToList();
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                foreach (var constraint in constraintsToInitialize)
                {
                    if (constraint.Task.IsFaulted || constraint.Task.IsCanceled)
                    {
                        //TODO: localize
                        _engineEnvironmentSettings.Host.Logger.LogWarning($"The constraint of type {constraint.Type} failed to initialize: {constraint.Task.Exception.Message}.");
                    }
                }
                return constraintsToInitialize
                    .Where(c => c.Task.Status == TaskStatus.RanToCompletion)
                    .Select(c => c.Task.Result)
                    .ToList();
            }

        }

        /// <summary>
        /// Evaluates the constraints with given <paramref name="type"/> for given args <paramref name="args"/>.
        /// </summary>
        /// <param name="type">constraint type to evaluate.</param>
        /// <param name="args">arguments to use for evaluation.</param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="TemplateConstraintResult"/> indicating if constraint is met, or details why the constraint is not met.</returns>
        /// <exception cref="Exception">when constraint is not found or cannot be evaluated.</exception>
        public async Task<TemplateConstraintResult> EvaluateConstraintAsync(string type, string args, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_templateConstrains.TryGetValue(type, out Task<ITemplateConstraint> task))
            {
                throw new UnknownConstraintException(type);
            }

            if (!task.IsCompleted)
            {
                try
                {
                    if (!task.IsCompleted)
                    {
                        await CancellableWhenAll(new[] { task }, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    //handled below
                }
            }
            if (task.IsFaulted || task.IsCanceled)
            {
                throw new ConstraintInitializationException(type, task.Exception);
            }

            try
            {
                return task.Result.Evaluate(args);
            }
            catch (Exception e)
            {
                throw new ConstraintEvaluationException(task.Result, e);
            }

        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
        }

        private async Task CancellableWhenAll(IEnumerable<Task> tasks, CancellationToken cancellationToken)
        {
            await Task.WhenAny(
                Task.WhenAll(tasks),
                Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }

            //throws exceptions
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        #region Exceptions

        public class UnknownConstraintException : Exception
        {
            public UnknownConstraintException(string type)
                : base($"The constraint of type '{type}' is unknown.")
            {
                Type = type;
            }

            public string Type { get; }
        }

        public class ConstraintEvaluationException : Exception
        {
            public ConstraintEvaluationException(ITemplateConstraint constraint, Exception? innerException = null)
                : base($"The constraint '{constraint.DisplayName}' failed to evaluate.", innerException)
            {
                Constraint = constraint;
            }

            public ITemplateConstraint Constraint { get; }
        }

        public class ConstraintInitializationException : Exception
        {
            public ConstraintInitializationException(string type, Exception? innerException = null)
                : base($"The constraint of type '{type}' failed to initialize.", innerException)
            {
                Type = type;
            }

            public string Type { get; }
        }

        #endregion
    }
}
