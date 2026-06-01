// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Decorates an <see cref="IProgressTask"/> with a cosmetic shimmer animation on the
/// description text. The shimmer sweeps a highlight across the first word (e.g.
/// "Installing") while the task is in progress, and stops when <see cref="Value"/>
/// reaches <see cref="MaxValue"/> or <see cref="StopShimmer"/> is called explicitly.
/// </summary>
internal sealed class ShimmerProgressTask : IProgressTask, IDisposable
{
    private readonly IProgressTask _inner;
    private string _baseDescription;
    private readonly string? _shimmerWord;
    private readonly string? _restEscaped;
    private readonly Timer? _shimmerTimer;
    private int _shimmerTick;
    private volatile bool _shimmerStopped;

    public ShimmerProgressTask(IProgressTask inner)
    {
        _inner = inner;
        _baseDescription = inner.Description;

        int spaceIndex = _baseDescription.IndexOf(' ', StringComparison.Ordinal);
        if (spaceIndex > 0 && _baseDescription.StartsWith("Installing", StringComparison.Ordinal))
        {
            _shimmerWord = _baseDescription[..spaceIndex];
            _restEscaped = _baseDescription[spaceIndex..].EscapeMarkup();
            _shimmerTimer = new Timer(OnShimmerTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(80));
        }
    }

    /// <summary>
    /// Timer callback that creates a "shimmer" wave effect on the status word (e.g. "Installing").
    /// A bright highlight sweeps left-to-right across the characters, fading from bold white at
    /// the center to grey at the edges, then briefly exits before re-entering.
    /// </summary>
    private void OnShimmerTick(object? state)
    {
        if (_shimmerStopped)
        {
            return;
        }

        try
        {
            int tick = Interlocked.Increment(ref _shimmerTick);
            int wordLen = _shimmerWord!.Length;
            // Wave sweeps across the word then briefly exits before re-entering.
            int totalPositions = wordLen + 6;
            // Offset by -3 so the shimmer wave starts off-screen (left of the word)
            // and sweeps across naturally before exiting on the right.
            int center = (tick % totalPositions) - 3;

            var sb = new StringBuilder();
            for (int i = 0; i < wordLen; i++)
            {
                int distance = Math.Abs(i - center);
                string ch = _shimmerWord[i].ToString().EscapeMarkup();

                sb.Append(distance switch
                {
                    0 => $"[white bold]{ch}[/]",
                    1 => $"[grey85]{ch}[/]",
                    _ => $"[grey]{ch}[/]",
                });
            }

            sb.Append(_restEscaped);
            _inner.Description = sb.ToString();
        }
        catch
        {
            // Shimmer is cosmetic — swallow any rendering errors silently.
        }
    }

    public void StopShimmer()
    {
        _shimmerStopped = true;
        _shimmerTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _shimmerTimer?.Dispose();
        _inner.Description = _baseDescription;
    }

    public void Dispose() => StopShimmer();

    public double Value
    {
        get => _inner.Value;
        set
        {
            _inner.Value = value;
            if (value >= _inner.MaxValue && _shimmerTimer is not null && !_shimmerStopped)
            {
                StopShimmer();
            }
        }
    }

    public string Description
    {
        get => _inner.Description;
        set
        {
            _inner.Description = value;
            // Keep _baseDescription in sync so that StopShimmer() restores the
            // latest externally-set description (e.g. "Installed ...") instead of
            // the original construction-time text (e.g. "Installing ...").
            _baseDescription = value;
        }
    }

    public double MaxValue
    {
        get => _inner.MaxValue;
        set => _inner.MaxValue = value;
    }
}
