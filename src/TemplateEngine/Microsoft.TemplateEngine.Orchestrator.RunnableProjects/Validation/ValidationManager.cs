// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation
{
    internal class ValidationManager
    {
        private SpinLock _spinLock;
        private IEngineEnvironmentSettings? _cachedSettings;
        private Dictionary<Guid, ITemplateValidator>? _cachedValidators;

        private ValidationManager() { }

        internal static ValidationManager Instance { get; } = new ValidationManager();

        internal async Task ValidateTemplateAsync(IEngineEnvironmentSettings settings, ITemplateValidationInfo template, ValidationScope scope, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (scope == ValidationScope.None)
            {
                throw new ArgumentException($"{nameof(scope)} cannot be '{ValidationScope.None}'.", nameof(scope));
            }
            if (!Enum.IsDefined(typeof(ValidationScope), scope))
            {
                throw new ArgumentException($"{nameof(scope)} should be one of {string.Join(", ", Enum.GetNames(typeof(ValidationScope)).Skip(1))}.", nameof(scope));
            }

            IEnumerable<ITemplateValidator> validators = await InitializeValidatorsAsync(settings, scope, cancellationToken).ConfigureAwait(false);
            validators.ForEach(v => v.ValidateTemplate(template));
        }

        private async Task<IEnumerable<ITemplateValidator>> InitializeValidatorsAsync(IEngineEnvironmentSettings settings, ValidationScope scope, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);
                if (_cachedSettings == null || _cachedSettings != settings || _cachedValidators == null)
                {
                    _cachedSettings = settings;

                    IEnumerable<ITemplateValidatorFactory> factories = settings.Components.OfType<ITemplateValidatorFactory>();
                    IEnumerable<Task<ITemplateValidator>> tasks = factories
                        .Where(f => f.Scope.HasFlag(scope))
                        .Select(f => f.CreateValidatorAsync(settings, cancellationToken));
                    ITemplateValidator[] validators = await Task.WhenAll(tasks).ConfigureAwait(false);
                    _cachedValidators = validators.ToDictionary(v => v.Factory.Id, v => v);
                }
                else
                {
                    IEnumerable<ITemplateValidatorFactory> factories = settings.Components.OfType<ITemplateValidatorFactory>();
                    List<Task<ITemplateValidator>> validatorsToCreate = new();
                    foreach (ITemplateValidatorFactory factory in factories.Where(f => f.Scope.HasFlag(scope)))
                    {
                        if (_cachedValidators.TryGetValue(factory.Id, out _))
                        {
                            continue;
                        }
                        validatorsToCreate.Add(factory.CreateValidatorAsync(settings, cancellationToken));
                    }
                    ITemplateValidator[] validators = await Task.WhenAll(validatorsToCreate).ConfigureAwait(false);
                    foreach (ITemplateValidator templateValidator in validators)
                    {
                        _cachedValidators[templateValidator.Factory.Id] = templateValidator;
                    }
                }
                return _cachedValidators
                    .Values
                    .Where(v => v.Factory.Scope.HasFlag(scope));
            }
            finally
            {
                if (lockTaken)
                {
                    _spinLock.Exit();
                }
            }
        }
    }
}
