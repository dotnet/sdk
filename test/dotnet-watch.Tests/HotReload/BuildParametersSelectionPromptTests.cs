// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SpectreTestConsole = Spectre.Console.Testing.TestConsole;

namespace Microsoft.DotNet.Watch.UnitTests;

public class BuildParametersSelectionPromptTests
{
    private static DeviceInfo[] CreateTestDevices() =>
    [
        new("emulator-5554", "Pixel 7 - API 35", "Emulator", "Online", "android-x64"),
        new("emulator-5555", "Pixel 7 - API 36", "Emulator", "Online", "android-x64"),
        new("0A041FDD400327", "Pixel 7 Pro", "Device", "Online", "android-arm64"),
    ];

    [Theory]
    [CombinatorialData]
    public async Task SelectsFrameworkByArrowKeysAndEnter([CombinatorialRange(0, count: 3)] int index)
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        for (var i = 0; i < index; i++)
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
        }
        console.Input.PushKey(ConsoleKey.Enter);

        var frameworks = new[] { "net7.0", "net8.0", "net9.0" };
        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var result = await prompt.SelectTargetFrameworkAsync(frameworks, CancellationToken.None);
        Assert.Equal(frameworks[index], result);
        Assert.Equal(frameworks[index], prompt.PreviousFrameworkSelection);
    }

    [Theory]
    [CombinatorialData]
    public async Task PreviousFrameworkSelectionIsReusedWhenUnchanged([CombinatorialRange(0, count: 3)] int index)
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        for (var i = 0; i < index; i++)
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
        }
        console.Input.PushKey(ConsoleKey.Enter);

        var frameworks = new[] { "net7.0", "net8.0", "net9.0" };
        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var first = await prompt.SelectTargetFrameworkAsync(frameworks, CancellationToken.None);
        Assert.Equal(frameworks[index], first);

        // Same frameworks (reordered, different casing) should reuse previous selection without prompting
        var second = await prompt.SelectTargetFrameworkAsync(["NET9.0", "net7.0", "net8.0"], CancellationToken.None);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task PromptsAgainWhenFrameworksChange()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var first = await prompt.SelectTargetFrameworkAsync(["net7.0", "net8.0", "net9.0"], CancellationToken.None);
        Assert.Equal("net7.0", first);

        var second = await prompt.SelectTargetFrameworkAsync(["net9.0", "net10.0"], CancellationToken.None);
        Assert.Equal("net10.0", second);
    }

    [Fact]
    public async Task SelectsFrameworkBySearchText()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        console.Input.PushText("net9.0");
        console.Input.PushKey(ConsoleKey.Enter);

        var frameworks = new[] { "net7.0", "net8.0", "net9.0", "net10.0" };
        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var result = await prompt.SelectTargetFrameworkAsync(frameworks, CancellationToken.None);
        Assert.Equal("net9.0", result);
    }

    [Fact]
    public async Task SelectsFirstDeviceByEnter()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        console.Input.PushKey(ConsoleKey.Enter);

        var devices = CreateTestDevices();
        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var result = await prompt.SelectDeviceAsync(devices, CancellationToken.None);
        Assert.Equal(devices[0], result);
        Assert.Equal(devices[0], prompt.PreviousDeviceSelection);
    }

    [Fact]
    public async Task SelectsDeviceBySearchText()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        console.Input.PushText("Pixel 7 Pro");
        console.Input.PushKey(ConsoleKey.Enter);

        var devices = CreateTestDevices();
        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var result = await prompt.SelectDeviceAsync(devices, CancellationToken.None);
        Assert.Equal("0A041FDD400327", result.Id);
        Assert.Equal("android-arm64", result.RuntimeIdentifier);
    }

    [Fact]
    public async Task PreviousDeviceSelectionIsReusedWhenUnchanged()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        console.Input.PushKey(ConsoleKey.Enter);

        var devices = CreateTestDevices();
        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var first = await prompt.SelectDeviceAsync(devices, CancellationToken.None);
        Assert.Equal(devices[0], first);

        // Same devices should reuse previous selection without prompting
        var second = await prompt.SelectDeviceAsync(devices, CancellationToken.None);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task PromptsAgainWhenDevicesChange()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushText("iPhone 15");
        console.Input.PushKey(ConsoleKey.Enter);

        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var first = await prompt.SelectDeviceAsync(CreateTestDevices(), CancellationToken.None);
        Assert.Equal("emulator-5554", first.Id);

        DeviceInfo[] newDevices =
        [
            new("sim-1", "iPhone 14 - iOS 18.6", "Simulator", "Booted", "iossimulator-arm64"),
            new("sim-2", "iPhone 15 - iOS 26.0", "Simulator", "Shutdown", "iossimulator-arm64"),
        ];
        var second = await prompt.SelectDeviceAsync(newDevices, CancellationToken.None);
        Assert.Equal("sim-2", second.Id);
    }

    [Fact]
    public async Task SelectsDeviceBySearchingId()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        console.Input.PushText("emulator-5555");
        console.Input.PushKey(ConsoleKey.Enter);

        var devices = CreateTestDevices();
        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var result = await prompt.SelectDeviceAsync(devices, CancellationToken.None);
        Assert.Equal("emulator-5555", result.Id);
        Assert.Equal("android-x64", result.RuntimeIdentifier);
    }

    [Fact]
    public void FormatDevice_WithAllMetadata()
    {
        var device = new DeviceInfo("emulator-5554", "Pixel 7 - API 35", "Emulator", "Online", "android-x64");
        var formatted = SpectreBuildParametersSelectionPrompt.FormatDevice(device);
        Assert.Equal("emulator-5554 - Pixel 7 - API 35 (Emulator, Online)", formatted);
    }

    [Fact]
    public void FormatDevice_WithoutType()
    {
        var device = new DeviceInfo("device-1", "My Phone", null, "Connected", null);
        var formatted = SpectreBuildParametersSelectionPrompt.FormatDevice(device);
        Assert.Equal("device-1 - My Phone (Connected)", formatted);
    }

    [Fact]
    public void FormatDevice_WithoutStatus()
    {
        var device = new DeviceInfo("device-1", "My Phone", "Device", null, null);
        var formatted = SpectreBuildParametersSelectionPrompt.FormatDevice(device);
        Assert.Equal("device-1 - My Phone (Device)", formatted);
    }

    [Fact]
    public void FormatDevice_IdOnly()
    {
        var device = new DeviceInfo("device-1", null, null, null, null);
        var formatted = SpectreBuildParametersSelectionPrompt.FormatDevice(device);
        Assert.Equal("device-1", formatted);
    }
}
