// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Channels;
using Spectre.Console;

namespace Microsoft.DotNet.Watch;

internal sealed class SpectreWatchSelectionPrompt(IAnsiConsole console) : WatchSelectionPrompt
{
    private const string CyanMarkup = "[cyan]";
    private const string GrayMarkup = "[gray]";
    private const string EndMarkup = "[/]";

    public SpectreWatchSelectionPrompt(IConsole watchConsole)
        : this(CreateConsole(watchConsole))
    {
    }

    public override void Dispose()
        => (console as IDisposable)?.Dispose();

    protected override Task<string> PromptForTargetFrameworkAsync(IReadOnlyList<string> targetFrameworks, CancellationToken cancellationToken)
    {
        var prompt = new SelectionPrompt<string>()
            .Title($"{CyanMarkup}{Markup.Escape(Resources.SelectTargetFrameworkPrompt)}{EndMarkup}")
            .PageSize(10)
            .MoreChoicesText($"{GrayMarkup}({Markup.Escape(Resources.MoreFrameworksText)}){EndMarkup}")
            .AddChoices(targetFrameworks)
            .EnableSearch()
            .SearchPlaceholderText(Resources.SearchPlaceholderText);

        return prompt.ShowAsync(console, cancellationToken);
    }

    protected override Task<DeviceInfo> PromptForDeviceAsync(IReadOnlyList<DeviceInfo> devices, CancellationToken cancellationToken)
    {
        var prompt = new SelectionPrompt<DeviceInfo>()
            .Title($"{CyanMarkup}{Markup.Escape(Resources.SelectDevicePrompt)}{EndMarkup}")
            .PageSize(10)
            .MoreChoicesText($"{GrayMarkup}({Markup.Escape(Resources.MoreDevicesText)}){EndMarkup}")
            .AddChoices(devices)
            .UseConverter(FormatDevice)
            .EnableSearch()
            .SearchPlaceholderText(Resources.SearchPlaceholderText);

        return prompt.ShowAsync(console, cancellationToken);
    }

    internal static string FormatDevice(DeviceInfo device)
    {
        var display = device.Id;
        if (!string.IsNullOrWhiteSpace(device.Description))
        {
            display += $" - {device.Description}";
        }

        if (!string.IsNullOrWhiteSpace(device.Type))
        {
            display += $" ({device.Type}";
            if (!string.IsNullOrWhiteSpace(device.Status))
            {
                display += $", {device.Status}";
            }
            display += ")";
        }
        else if (!string.IsNullOrWhiteSpace(device.Status))
        {
            display += $" ({device.Status})";
        }

        return display;
    }

    private static IAnsiConsole CreateConsole(IConsole watchConsole)
    {
        if (!Console.IsInputRedirected)
        {
            return AnsiConsole.Console;
        }

        // When stdin is redirected (e.g. in integration tests), Spectre.Console detects
        // non-interactive mode and refuses to prompt. Create a console with forced
        // interactivity that reads keys from IConsole.KeyPressed (fed by
        // PhysicalConsole.ListenToStandardInputAsync).
        var ansiConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
        });
        ansiConsole.Profile.Capabilities.Interactive = true;
        ansiConsole.Profile.Capabilities.Ansi = true;
        return new KeyPressedAnsiConsole(ansiConsole, watchConsole);
    }

    /// <summary>
    /// Wraps an <see cref="IAnsiConsole"/> to read input from <see cref="IConsole.KeyPressed"/> events
    /// instead of <see cref="System.Console.ReadKey"/>.
    /// </summary>
    private sealed class KeyPressedAnsiConsole(IAnsiConsole inner, IConsole watchConsole) : IAnsiConsole, IDisposable
    {
        private readonly KeyPressedInput _input = new(watchConsole);

        public Profile Profile => inner.Profile;
        public IAnsiConsoleCursor Cursor => inner.Cursor;
        public IAnsiConsoleInput Input => _input;
        public IExclusivityMode ExclusivityMode => inner.ExclusivityMode;
        public Spectre.Console.Rendering.RenderPipeline Pipeline => inner.Pipeline;
        public void Clear(bool home) => inner.Clear(home);
        public void Write(Spectre.Console.Rendering.IRenderable renderable) => inner.Write(renderable);
        public void Dispose() => _input.Dispose();
    }

    /// <summary>
    /// Bridges <see cref="IConsole.KeyPressed"/> events to Spectre.Console's
    /// <see cref="IAnsiConsoleInput"/> using a channel.
    /// </summary>
    private sealed class KeyPressedInput : IAnsiConsoleInput, IDisposable
    {
        private readonly Channel<ConsoleKeyInfo> _channel = Channel.CreateUnbounded<ConsoleKeyInfo>();
        private readonly IConsole _console;

        public KeyPressedInput(IConsole console)
        {
            _console = console;
            _console.KeyPressed += OnKeyPressed;
        }

        private void OnKeyPressed(ConsoleKeyInfo key)
            => _channel.Writer.TryWrite(key);

        public bool IsKeyAvailable()
            => _channel.Reader.TryPeek(out _);

        public ConsoleKeyInfo? ReadKey(bool intercept)
            => ReadKeyAsync(intercept, CancellationToken.None).GetAwaiter().GetResult();

        public async Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
            => await _channel.Reader.ReadAsync(cancellationToken);

        public void Dispose()
            => _console.KeyPressed -= OnKeyPressed;
    }
}
