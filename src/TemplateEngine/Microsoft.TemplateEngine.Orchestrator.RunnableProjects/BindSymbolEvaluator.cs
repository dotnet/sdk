// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Components;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Utils;

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
            _logger = settings.Host.LoggerFactory.CreateLogger<BindSymbolEvaluator>();
        }

        /// <summary>
        /// Evaluates bind symbols from external sources.
        /// Sets the evaluated symbols to <paramref name="variableCollection"/>.
        /// </summary>
        /// <param name="symbols">The symbols to evaluate.</param>
        /// <param name="variableCollection">The variable collection to set results to.</param>
        /// <param name="cancellationToken">The cancellation token that allows cancelling the operation.</param>
        public async Task EvaluateBindSymbolsAsync(IEnumerable<BindSymbol> symbols, IVariableCollection variableCollection, CancellationToken cancellationToken)
        {
            if (!_bindSymbolSources.Any())
            {
                _logger.LogDebug("No sources for bind symbols are available");
                return;
            }
            _logger.LogDebug(
                "Configured bind sources are: {0}.",
                string.Join(", ", _bindSymbolSources.Select(s => $"{s.DisplayName}({s.GetType().Name})")));

            //set default values for symbols that have them defined
            foreach (BindSymbol bindSymbol in symbols)
            {
                if (!variableCollection.ContainsKey(bindSymbol.Name) && bindSymbol.DefaultValue != null)
                {
                    bool result = ParameterConverter.TryConvertLiteralToDatatype(bindSymbol.DefaultValue, bindSymbol.DataType, out object? value);
                    if (result && value != null)
                    {
                        variableCollection[bindSymbol.Name] = value;
                    }
                    else if (!result && !string.IsNullOrWhiteSpace(bindSymbol.DataType))
                    {
                        _logger.LogWarning(LocalizableStrings.BindSymbolEvaluator_Warning_DefaultValueConversionFailure, bindSymbol.Name, bindSymbol.DefaultValue, bindSymbol.DataType);
                    }
                }
            }

            IEnumerable<BindSymbol> bindSymbols = symbols.Where(bs => !string.IsNullOrWhiteSpace(bs.Binding));
            if (!bindSymbols.Any())
            {
                _logger.LogDebug("No bind symbols has '{0}' defined.", nameof(BindSymbol.Binding).ToLowerInvariant());
                return;
            }

            IReadOnlyList<(BindSymbol Symbol, Task<string?> Task)> tasksToRun = bindSymbols
                .Select(s => (s, GetBoundValueAsync(s.Binding, cancellationToken)))
                .ToList();

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
            ProcessEvaluationResults(variableCollection, tasksToRun);
        }

        private void ProcessEvaluationResults(IVariableCollection variableCollection, IReadOnlyList<(BindSymbol Symbol, Task<string?> Task)> completedTasks)
        {
            foreach ((BindSymbol currentSymbol, Task<string?> currentTask) in completedTasks)
            {
                if (!currentTask.IsCompleted)
                {
                    throw new InvalidOperationException("The method should be used only after the tasks are completed.");
                }
                if (currentTask.IsFaulted || currentTask.IsCanceled)
                {
                    _logger.LogWarning(LocalizableStrings.BindSymbolEvaluator_Warning_EvaluationError, currentSymbol.Name);
                    _logger.LogDebug(currentTask.Exception, "The evaluation task has failed: {0}", currentTask.Exception.Message);
                    continue;
                }

                if (currentTask.Result is null)
                {
                    if (currentSymbol.DefaultValue != null)
                    {
                        _logger.LogDebug(
                            "Failed to evaluate bind symbol '{0}', the returned value is null. The default value '{1}' is used instead.",
                            currentSymbol.Name,
                            currentSymbol.DefaultValue);
                    }
                    else
                    {
                        _logger.LogWarning(LocalizableStrings.BindSymbolEvaluator_Warning_EvaluationError, currentSymbol.Name);
                    }
                    continue;
                }

                string obtainedValue = currentTask.Result!;
                bool result = ParameterConverter.TryConvertLiteralToDatatype(obtainedValue, currentSymbol.DataType, out object? convertedValue);
                if (result && convertedValue != null)
                {
                    variableCollection[currentSymbol.Name] = convertedValue;
                    _logger.LogDebug("Variable '{0}' was set to '{1}'.", currentSymbol.Name, convertedValue);
                    continue;
                }
                if (!result)
                {
                    _logger.LogWarning(
                        LocalizableStrings.BindSymbolEvaluator_Warning_ConversionFailure,
                        currentSymbol.Name,
                        obtainedValue,
                        currentSymbol.DataType ?? "<null>");
                    continue;
                }
                _logger.LogDebug(
                    "Variable '{0}' was not set: the value '{1}' after conversion to datatype '{2}' is null.",
                    currentSymbol.Name,
                    obtainedValue,
                    currentSymbol.DataType ?? "<null>");
            }
        }

        private async Task<string?> GetBoundValueAsync(string configuredBinding, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(configuredBinding))
            {
                throw new ArgumentException($"'{nameof(configuredBinding)}' cannot be null or whitespace.", nameof(configuredBinding));
            }
            _logger.LogDebug("Evaluating binding '{0}'.", configuredBinding);

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
                _logger.LogDebug("Binding '{0}' does not define prefix. All the sources that do not require prefix will be queried for this binding.", configuredBinding);
                sourcesToSearch = _bindSymbolSources.Where(source => !source.RequiresPrefixMatch);
            }
            else
            {
                sourcesToSearch = _bindSymbolSources.Where(s => s.SourcePrefix?.Equals(prefix, StringComparison.OrdinalIgnoreCase) ?? false);
                _logger.LogDebug(
                    "The following sources match prefix '{0}': {1}.",
                    prefix,
                    string.Join(", ", sourcesToSearch.Select(s => s.DisplayName)));
                if (!sourcesToSearch.Any())
                {
                    _logger.LogDebug("No sources matches prefix '{0}' does not define prefix. All the sources that do not require prefix will be queried for the binding '{1}'.", prefix, configuredBinding);
                    sourcesToSearch = _bindSymbolSources.Where(source => !source.RequiresPrefixMatch);
                    binding = configuredBinding;
                }
            }

            var successfulTasks = await RunEvaluationTasks(sourcesToSearch, binding, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!successfulTasks.Any() && binding != configuredBinding)
            {
                _logger.LogDebug(
                    "No values were retrieved for '{0}' for the sources matching the prefix. All the sources will be queried for the binding '{1}' now.",
                    binding,
                    configuredBinding);
                binding = configuredBinding;
                //if nothing is found, try all sources with unparsed values from configuration
                sourcesToSearch = _bindSymbolSources.Where(source => !source.RequiresPrefixMatch);
                successfulTasks = await RunEvaluationTasks(sourcesToSearch, configuredBinding, cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();

            if (!successfulTasks.Any())
            {
                _logger.LogDebug("No values were retrieved for '{0}'.", binding);
                return null;
            }
            else if (successfulTasks.Count() == 1)
            {
                _logger.LogDebug("'{0}' was retrieved for '{1}'.", successfulTasks.Single().Value, binding);
                return successfulTasks.Single().Value;
            }
            else
            {
                //in case of multiple results, use highest priority source
                _logger.LogDebug(
                    "The following values were retrieved for binding '{0}': {1}.",
                    binding,
                    string.Join(", ", successfulTasks.Select(t => $"{t.Source.DisplayName} (priority: {t.Source.Priority}): '{t.Value}'")));
                var highestPriority = successfulTasks.Max(t => t.Source.Priority);
                var highestPriorityTasks = successfulTasks.Where(t => t.Source.Priority == highestPriority);
                if (highestPriorityTasks.Count() > 1)
                {
                    string sourcesList = string.Join(", ", highestPriorityTasks.Select(t => $"'{t.Source.DisplayName}'"));
                    string prefixesList = string.Join(", ", highestPriorityTasks.Select(t => $"'{t.Source.SourcePrefix}:'"));
                    _logger.LogWarning(LocalizableStrings.BindSymbolEvaluator_Warning_ValueAvailableFromMultipleSources, configuredBinding, sourcesList, prefixesList);
                    return null;
                }
                else
                {
                    _logger.LogDebug("'{0}' was selected for '{1}' as highest priority value.", highestPriorityTasks.Single().Value, binding);
                    return highestPriorityTasks.Single().Value;
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
                _logger.LogDebug("Failed to retrieve '{0}' from the source {1}: {2}", binding, nameof(task.Source), task.Task.Exception.Message);
            }

            return tasksToRun
                .Where(t => t.Task.IsCompleted && t.Task.Result != null)
                .Select(t => (t.Source, t.Task.Result!));
        }
    }
}
