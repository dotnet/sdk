// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SpectreTestConsole = Spectre.Console.Testing.TestConsole;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class BuildParametersSelectionPromptTests
{
    private static DeviceInfo[] CreateTestDevices() =>
    [
        new("emulator-5554", "Pixel 7 - API 35", "Emulator", "Online", "android-x64"),
        new("emulator-5555", "Pixel 7 - API 36", "Emulator", "Online", "android-x64"),
        new("0A041FDD400327", "Pixel 7 Pro", "Device", "Online", "android-arm64"),
    ];

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public async Task SelectsFrameworkByArrowKeysAndEnter(int index)
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
        Assert.AreEqual(frameworks[index], result);
        Assert.AreEqual(frameworks[index], prompt.PreviousFrameworkSelection);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public async Task PreviousFrameworkSelectionIsReusedWhenUnchanged(int index)
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
        Assert.AreEqual(frameworks[index], first);

        // Same frameworks (reordered, different casing) should reuse previous selection without prompting
        var second = await prompt.SelectTargetFrameworkAsync(["NET9.0", "net7.0", "net8.0"], CancellationToken.None);
        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public async Task PromptsAgainWhenFrameworksChange()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var first = await prompt.SelectTargetFrameworkAsync(["net7.0", "net8.0", "net9.0"], CancellationToken.None);
        Assert.AreEqual("net7.0", first);

        var second = await prompt.SelectTargetFrameworkAsync(["net9.0", "net10.0"], CancellationToken.None);
        Assert.AreEqual("net10.0", second);
    }

    [TestMethod]
    public async Task SelectsFrameworkBySearchText()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        console.Input.PushText("net9.0");
        console.Input.PushKey(ConsoleKey.Enter);

        var frameworks = new[] { "net7.0", "net8.0", "net9.0", "net10.0" };
        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var result = await prompt.SelectTargetFrameworkAsync(frameworks, CancellationToken.None);
        Assert.AreEqual("net9.0", result);
    }

    [TestMethod]
    public async Task SelectsFirstDeviceByEnter()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        console.Input.PushKey(ConsoleKey.Enter);

        var devices = CreateTestDevices();
        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var result = await prompt.SelectDeviceAsync(devices, CancellationToken.None);
        Assert.AreEqual(devices[0], result);
        Assert.AreEqual(devices[0], prompt.PreviousDeviceSelection);
    }

    [TestMethod]
    public async Task SelectsDeviceBySearchText()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        console.Input.PushText("Pixel 7 Pro");
        console.Input.PushKey(ConsoleKey.Enter);

        var devices = CreateTestDevices();
        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var result = await prompt.SelectDeviceAsync(devices, CancellationToken.None);
        Assert.AreEqual("0A041FDD400327", result.Id);
        Assert.AreEqual("android-arm64", result.RuntimeIdentifier);
    }

    [TestMethod]
    public async Task PreviousDeviceSelectionIsReusedWhenUnchanged()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        console.Input.PushKey(ConsoleKey.Enter);

        var devices = CreateTestDevices();
        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var first = await prompt.SelectDeviceAsync(devices, CancellationToken.None);
        Assert.AreEqual(devices[0], first);

        // Same devices should reuse previous selection without prompting
        var second = await prompt.SelectDeviceAsync(devices, CancellationToken.None);
        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public async Task PromptsAgainWhenDevicesChange()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushText("iPhone 15");
        console.Input.PushKey(ConsoleKey.Enter);

        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var first = await prompt.SelectDeviceAsync(CreateTestDevices(), CancellationToken.None);
        Assert.AreEqual("emulator-5554", first.Id);

        DeviceInfo[] newDevices =
        [
            new("sim-1", "iPhone 14 - iOS 18.6", "Simulator", "Booted", "iossimulator-arm64"),
            new("sim-2", "iPhone 15 - iOS 26.0", "Simulator", "Shutdown", "iossimulator-arm64"),
        ];
        var second = await prompt.SelectDeviceAsync(newDevices, CancellationToken.None);
        Assert.AreEqual("sim-2", second.Id);
    }

    [TestMethod]
    public async Task SelectsDeviceBySearchingId()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        console.Input.PushText("emulator-5555");
        console.Input.PushKey(ConsoleKey.Enter);

        var devices = CreateTestDevices();
        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var result = await prompt.SelectDeviceAsync(devices, CancellationToken.None);
        Assert.AreEqual("emulator-5555", result.Id);
        Assert.AreEqual("android-x64", result.RuntimeIdentifier);
    }

    [TestMethod]
    public void FormatDevice_WithAllMetadata()
    {
        var device = new DeviceInfo("emulator-5554", "Pixel 7 - API 35", "Emulator", "Online", "android-x64");
        var formatted = SpectreBuildParametersSelectionPrompt.FormatDevice(device);
        Assert.AreEqual("emulator-5554 - Pixel 7 - API 35 (Emulator, Online)", formatted);
    }

    [TestMethod]
    public void FormatDevice_WithoutType()
    {
        var device = new DeviceInfo("device-1", "My Phone", null, "Connected", null);
        var formatted = SpectreBuildParametersSelectionPrompt.FormatDevice(device);
        Assert.AreEqual("device-1 - My Phone (Connected)", formatted);
    }

    [TestMethod]
    public void FormatDevice_WithoutStatus()
    {
        var device = new DeviceInfo("device-1", "My Phone", "Device", null, null);
        var formatted = SpectreBuildParametersSelectionPrompt.FormatDevice(device);
        Assert.AreEqual("device-1 - My Phone (Device)", formatted);
    }

    [TestMethod]
    public void FormatDevice_IdOnly()
    {
        var device = new DeviceInfo("device-1", null, null, null, null);
        var formatted = SpectreBuildParametersSelectionPrompt.FormatDevice(device);
        Assert.AreEqual("device-1", formatted);
    }

    [TestMethod]
    public async Task SelectsFrameworkThenDevice()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        // Push keys for TFM selection (search + enter)
        console.Input.PushText("net9.0");
        console.Input.PushKey(ConsoleKey.Enter);

        // Push keys for device selection (search + enter)
        console.Input.PushText("Pixel 7 Pro");
        console.Input.PushKey(ConsoleKey.Enter);

        var frameworks = new[] { "net7.0", "net8.0", "net9.0" };
        var devices = CreateTestDevices();
        var prompt = new SpectreBuildParametersSelectionPrompt(console);

        var selectedFramework = await prompt.SelectTargetFrameworkAsync(frameworks, CancellationToken.None);
        Assert.AreEqual("net9.0", selectedFramework);

        var selectedDevice = await prompt.SelectDeviceAsync(devices, CancellationToken.None);
        Assert.AreEqual("0A041FDD400327", selectedDevice.Id);
    }
}
