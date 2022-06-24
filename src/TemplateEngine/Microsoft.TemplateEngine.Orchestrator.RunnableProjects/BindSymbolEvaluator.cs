// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Components;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class BindSymbolEvaluator
    {
        private readonly IEngineEnvironmentSettings _settings;
        private readonly IReadOnlyList<IBindSymbolSource> _bindSymbolSources;
        private readonly ILogger _logger;

        internal BindSymbolEvaluator(IEngineEnvironmentSettings settings)
        {
            _settings = settings;
            _bindSymbolSources = settings.Components.OfType<IBindSymbolSource>().ToList();
            _logger = settings.Host.Logger;
        }

        /// <summary>
        /// Evaluates bind symbols from external sources.
        /// Sets the evaluated symbols to <paramref name="variableCollection"/>.
        /// </summary>
        /// <param name="symbols">The symbols to evaluate.</param>
        /// <param name="variableCollection">The variable collection to set results to.</param>
        /// <param name="cancellationToken">The cancellation token that allows cancelling the operation.</param>
        public async Task EvaluateBindedSymbolsAsync(IEnumerable<BindSymbol> symbols, IVariableCollection variableCollection, CancellationToken cancellationToken)
        {
            if (!_bindSymbolSources.Any())
            {
                return;
            }

            var bindSymbols = symbols.Where(bs => !string.IsNullOrWhiteSpace(bs.Binding));
            if (!bindSymbols.Any())
            {
                return;
            }

            var tasksToRun = bindSymbols.Select(s => new { Symbol = s, Task = GetBoundValueAsync(s.Binding, cancellationToken) }).ToList();
            try
            {
                await Task.WhenAll(tasksToRun.Select(t => t.Task)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                //do nothing
                //errors are handled below
            }
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var task in tasksToRun.Where(t => t.Task.IsFaulted || t.Task.IsCompleted && t.Task.Result == null))
            {
                _logger.LogWarning(LocalizableStrings.BindSymbolEvaluator_Warning_EvaluationError, task.Symbol.Name);
            }

            var successfulTasks = tasksToRun.Where(t => t.Task.IsCompleted && t.Task.Result != null);
            foreach (var task in successfulTasks)
            {
                variableCollection[task.Symbol.Name] = RunnableProjectGenerator.InferTypeAndConvertLiteral(task.Task.Result!);
            }
        }

        private async Task<string?> GetBoundValueAsync(string configuredBinding, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(configuredBinding))
            {
                throw new ArgumentException($"'{nameof(configuredBinding)}' cannot be null or whitespace.", nameof(configuredBinding));
            }

            string? prefix = null;
            string binding = configuredBinding;
            if (configuredBinding.Contains(":"))
            {
                prefix = configuredBinding.Substring(0, configuredBinding.IndexOf(":")).Trim();
                binding = configuredBinding.Substring(configuredBinding.IndexOf(":") + 1).Trim();
            }
            if (string.IsNullOrWhiteSpace(binding))
            {
                binding = configuredBinding;
            }

            IEnumerable<IBindSymbolSource>? sourcesToSearch = null;
            if (string.IsNullOrWhiteSpace(prefix))
            {
                sourcesToSearch = _bindSymbolSources;
            }
            else
            {
                sourcesToSearch = _bindSymbolSources.Where(s => s.SourcePrefix?.Equals(prefix, StringComparison.OrdinalIgnoreCase) ?? false);
                if (!sourcesToSearch.Any())
                {
                    //if there is no matching sources, then use all the sources with full binding
                    sourcesToSearch = _bindSymbolSources;
                    binding = configuredBinding;
                }
            }

            var successfulTasks = await RunEvaluationTasks(sourcesToSearch, binding, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!successfulTasks.Any())
            {
                //if nothing is found, try all sources with unparsed values from configuration
                successfulTasks = await RunEvaluationTasks(_bindSymbolSources, configuredBinding, cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();

            if (!successfulTasks.Any())
            {
                return null;
            }
            else if (successfulTasks.Count() == 1)
            {
                return successfulTasks.Single().Value;
            }
            else
            {
                //in case of multiple results, use highest priority source
                var highestPriority = successfulTasks.Max(t => t.Source.Priority);
                var highestPrioTasks = successfulTasks.Where(t => t.Source.Priority == highestPriority);
                if (highestPrioTasks.Count() > 1)
                {
                    string sourcesList = string.Join(", ", highestPrioTasks.Select(t => $"'{t.Source.DisplayName}'"));
                    string prefixesList = string.Join(", ", highestPrioTasks.Select(t => $"'{t.Source.SourcePrefix}:'"));
                    _logger.LogWarning(LocalizableStrings.BindSymbolEvaluator_Warning_ValueAvailableFromMultipleSources, configuredBinding, sourcesList, prefixesList);
                    return null;
                }
                else
                {
                    return highestPrioTasks.Single().Value;
                }
            }
        }

        private async Task<IEnumerable<(IBindSymbolSource Source, string Value)>> RunEvaluationTasks(IEnumerable<IBindSymbolSource> sourcesToSearch, string binding, CancellationToken cancellationToken)
        {
            var tasksToRun = sourcesToSearch.Select(s => new { Source = s, Task = s.GetBoundValueAsync(_settings, binding, cancellationToken) }).ToList();
            try
            {
                await Task.WhenAll(tasksToRun.Select(t => t.Task)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                //do nothing, errors are handled below.
            }
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var task in tasksToRun.Where(t => t.Task.IsFaulted))
            {
                _logger.LogDebug($"Failed to retrieve '{binding}' from the source {nameof(task.Source)}: {task.Task.Exception.Message}");
            }

            return tasksToRun
                .Where(t => t.Task.IsCompleted && t.Task.Result != null)
                .Select(t => (t.Source, t.Task.Result!));
        }
    }
}
