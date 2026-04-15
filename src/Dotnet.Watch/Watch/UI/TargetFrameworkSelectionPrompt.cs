// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch;

internal abstract class TargetFrameworkSelectionPrompt : IDisposable
{
    public IReadOnlyList<string>? PreviousTargetFrameworks { get; set; }
    public string? PreviousSelection { get; set; }

    public async ValueTask<string> SelectAsync(IReadOnlyList<string> targetFrameworks, CancellationToken cancellationToken)
    {
        var orderedTargetFrameworks = targetFrameworks.Order(StringComparer.OrdinalIgnoreCase).ToArray();

        if (PreviousSelection != null && PreviousTargetFrameworks?.SequenceEqual(orderedTargetFrameworks, StringComparer.OrdinalIgnoreCase) == true)
        {
            return PreviousSelection;
        }

        PreviousTargetFrameworks = orderedTargetFrameworks;
        PreviousSelection = await PromptAsync(targetFrameworks, cancellationToken);
        return PreviousSelection;
    }

    protected abstract Task<string> PromptAsync(IReadOnlyList<string> targetFrameworks, CancellationToken cancellationToken);

    public virtual void Dispose() { }
}
