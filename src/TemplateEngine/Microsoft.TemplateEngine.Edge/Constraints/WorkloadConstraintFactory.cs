// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Components;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Constraints
{
    public sealed class WorkloadConstraintFactory : ITemplateConstraintFactory
    {
        private static readonly SemaphoreSlim Mutex = new(1);

        Guid IIdentifiedComponent.Id { get; } = Guid.Parse("{F8BA5B13-7BD6-47C8-838C-66626526817B}");

        string ITemplateConstraintFactory.Type => "workload";

        async Task<ITemplateConstraint> ITemplateConstraintFactory.CreateTemplateConstraintAsync(IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // need to await due to lack of covariance on Tasks
            return await WorkloadConstraint.CreateAsync(environmentSettings, this, cancellationToken).ConfigureAwait(false);
        }

        internal class WorkloadConstraint : ConstraintBase
        {
            private readonly HashSet<string> _installedWorkloads;
            private readonly string _installedWorkloadsString;
            private readonly Func<IReadOnlyList<string>, string> _remedySuggestionFactory;

            private WorkloadConstraint(
                IEngineEnvironmentSettings environmentSettings,
                ITemplateConstraintFactory factory,
                IReadOnlyList<WorkloadInfo> workloads,
                Func<IReadOnlyList<string>, string> remedySuggestionFactory)
                : base(environmentSettings, factory)
            {
                _installedWorkloads = new HashSet<string>(workloads.Select(w => w.Id), StringComparer.InvariantCultureIgnoreCase);
                _installedWorkloadsString = workloads.Select(w => $"{w.Id} \"{w.Description}\"").ToCsvString();
                _remedySuggestionFactory = remedySuggestionFactory;
            }

            public override string DisplayName => LocalizableStrings.WorkloadConstraint_Name;

            internal static async Task<WorkloadConstraint> CreateAsync(IEngineEnvironmentSettings environmentSettings, ITemplateConstraintFactory factory, CancellationToken cancellationToken)
            {
                (IReadOnlyList<WorkloadInfo> workloads, Func<IReadOnlyList<string>, string> remedySuggestionFactory) =
                    await ExtractWorkloadInfoAsync(
                        environmentSettings.Components.OfType<IWorkloadsInfoProvider>(),
                        environmentSettings.Host.Logger,
                        cancellationToken).ConfigureAwait(false);
                return new WorkloadConstraint(environmentSettings, factory, workloads, remedySuggestionFactory);
            }

            protected override TemplateConstraintResult EvaluateInternal(string? args)
            {
                IReadOnlyList<string> supportedWorkloads = ParseArgs(args).ToList();

                bool isSupportedWorkload = supportedWorkloads.Any(_installedWorkloads.Contains);

                if (isSupportedWorkload)
                {
                    return TemplateConstraintResult.CreateAllowed(this);
                }

                return TemplateConstraintResult.CreateRestricted(
                    this,
                    string.Format(
                        LocalizableStrings.WorkloadConstraint_Message_Restricted,
                        string.Join(", ", supportedWorkloads),
                        string.Join(", ", _installedWorkloadsString)),
                    _remedySuggestionFactory(supportedWorkloads));
            }

            //supported configuration:
            // "args": "maui-mobile"  // workload expected
            // "args": [ "maui-mobile", "maui-desktop" ] // any of workloads expected
            private static IEnumerable<string> ParseArgs(string? args)
            {
                return args.ParseArrayOfConstraintStrings();
            }

            private static async Task<(IReadOnlyList<WorkloadInfo> Workloads, Func<IReadOnlyList<string>, string> RemedySuggestionFactory)> ExtractWorkloadInfoAsync(IEnumerable<IWorkloadsInfoProvider> workloadsInfoProviders, ILogger logger, CancellationToken token)
            {
                List<IWorkloadsInfoProvider> providers = workloadsInfoProviders.ToList();
                List<WorkloadInfo>? workloads = null;

                if (providers.Count == 0)
                {
                    throw new ConfigurationException(LocalizableStrings.WorkloadConstraint_Error_MissingProvider);
                }

                if (providers.Count > 1)
                {
                    throw new ConfigurationException(
                        string.Format(LocalizableStrings.WorkloadConstraint_Error_MismatchedProviders, providers.Select(p => p.Id).ToCsvString()));
                }

                token.ThrowIfCancellationRequested();

                await Mutex.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    IEnumerable<WorkloadInfo> currentProviderWorkloads = await providers[0].GetInstalledWorkloadsAsync(token).ConfigureAwait(false);
                    workloads = currentProviderWorkloads.ToList();
                }
                finally
                {
                    Mutex.Release();
                }

                if (workloads.Select(w => w.Id).HasDuplicates(StringComparer.InvariantCultureIgnoreCase))
                {
                    logger.LogWarning(string.Format(
                        LocalizableStrings.WorkloadConstraint_Warning_DuplicateWorkloads,
                        workloads.Select(w => w.Id).GetDuplicates(StringComparer.InvariantCultureIgnoreCase).ToCsvString()));
                    workloads = workloads
                        .GroupBy(w => w.Id, StringComparer.InvariantCultureIgnoreCase)
                        .Select(g => g.First())
                        .ToList();
                }

                return (workloads, providers[0].ProvideConstraintRemedySuggestion);
            }
        }
    }
}
