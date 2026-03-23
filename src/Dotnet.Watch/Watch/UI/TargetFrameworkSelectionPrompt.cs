// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch;

internal sealed class TargetFrameworkSelectionPrompt(ConsoleInputReader inputReader)
{
    public IReadOnlyList<string>? PreviousTargetFrameworks { get; set; }
    public string? PreviousSelection { get; set; }

    public async ValueTask<string> SelectAsync(IReadOnlyList<string> targetFrameworks, CancellationToken cancellationToken)
    {
        var orderedTargetFrameworks = targetFrameworks.Order().ToArray();

        if (PreviousSelection != null && PreviousTargetFrameworks?.SequenceEqual(orderedTargetFrameworks, StringComparer.OrdinalIgnoreCase) == true)
        {
            return PreviousSelection;
        }

        PreviousTargetFrameworks = orderedTargetFrameworks;

        var keyInfo = await inputReader.GetKeyAsync(
            $"Select target framework:{Environment.NewLine}{string.Join(Environment.NewLine, targetFrameworks.Select((tfm, i) => $"{i + 1}) {tfm}"))}",
            AcceptKey,
            cancellationToken);

        _ = TryGetIndex(keyInfo, out var index);
        return PreviousSelection = targetFrameworks[index];

        bool TryGetIndex(ConsoleKeyInfo info, out int index)
        {
            index = info.KeyChar - '1';
            return index >= 0 && index < targetFrameworks.Count;
        }

        bool AcceptKey(ConsoleKeyInfo info)
            => info is { Modifiers: ConsoleModifiers.None } && TryGetIndex(info, out _);
    }
}
