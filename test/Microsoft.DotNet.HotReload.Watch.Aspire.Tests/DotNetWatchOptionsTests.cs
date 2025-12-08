// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

public class DotNetWatchOptionsTests
{
    [Fact]
    public void TryParse_RequiredProjectOption()
    {
        // Project option is missing
        var args = new[] { "--verbose", "a", "b" };
        Assert.False(DotNetWatchOptions.TryParse(args, out var options));
        Assert.Null(options);
    }

    [Fact]
    public void TryParse_RequiredSdkOption()
    {
        // Project option is missing
        var args = new[] { "--project", "proj", "a", "b" };
        Assert.False(DotNetWatchOptions.TryParse(args, out var options));
        Assert.Null(options);
    }

    [Fact]
    public void TryParse_ProjectAndSdkPaths()
    {
        var args = new[] { "--sdk", "sdk", "--project", "myproject.csproj" };
        Assert.True(DotNetWatchOptions.TryParse(args, out var options));
        Assert.Equal("sdk", options.SdkDirectory);
        Assert.Equal("myproject.csproj", options.ProjectPath);
        Assert.Empty(options.ApplicationArguments);
    }

    [Fact]
    public void TryParse_ApplicationArguments()
    {
        var args = new[] { "--sdk", "sdk", "--project", "proj", "--verbose", "a", "b" };
        Assert.True(DotNetWatchOptions.TryParse(args, out var options));
        AssertEx.SequenceEqual(["a", "b"], options.ApplicationArguments);
    }

    [Fact]
    public void TryParse_VerboseOption()
    {
        // With verbose flag
        var argsVerbose = new[] { "--sdk", "sdk", "--project", "proj", "--verbose" };
        Assert.True(DotNetWatchOptions.TryParse(argsVerbose, out var optionsVerbose));
        Assert.Equal(LogLevel.Debug, optionsVerbose.LogLevel);
        
        // Without verbose flag
        var argsNotVerbose = new[] { "--sdk", "sdk", "--project", "proj" };
        Assert.True(DotNetWatchOptions.TryParse(argsNotVerbose, out var optionsNotVerbose));
        Assert.Equal(LogLevel.Information, optionsNotVerbose.LogLevel);
    }

    [Fact]
    public void TryParse_QuietOption()
    {
        // With quiet flag
        var argsQuiet = new[] { "--sdk", "sdk", "--project", "proj", "--quiet" };
        Assert.True(DotNetWatchOptions.TryParse(argsQuiet, out var optionsQuiet));
        Assert.Equal(LogLevel.Warning, optionsQuiet.LogLevel);
        
        // Without quiet flag
        var argsNotQuiet = new[] { "--sdk", "sdk", "--project", "proj" };
        Assert.True(DotNetWatchOptions.TryParse(argsNotQuiet, out var optionsNotQuiet));
        Assert.Equal(LogLevel.Information, optionsNotQuiet.LogLevel);
    }
    
    [Fact]
    public void TryParse_NoLaunchProfileOption()
    {
        // With no-launch-profile flag
        var argsNoProfile = new[] { "--sdk", "sdk", "--project", "proj", "--no-launch-profile" };
        Assert.True(DotNetWatchOptions.TryParse(argsNoProfile, out var optionsNoProfile));
        Assert.True(optionsNoProfile.NoLaunchProfile);
        
        // Without no-launch-profile flag
        var argsWithProfile = new[] { "--sdk", "sdk", "--project", "proj" };
        Assert.True(DotNetWatchOptions.TryParse(argsWithProfile, out var optionsWithProfile));
        Assert.False(optionsWithProfile.NoLaunchProfile);
    }
        
    [Fact]
    public void TryParse_ConflictingOptions()
    {
        // Cannot specify both --quiet and --verbose
        var args = new[] { "--sdk", "sdk", "--project", "proj", "--quiet", "--verbose" };
        Assert.False(DotNetWatchOptions.TryParse(args, out var options));
        Assert.Null(options);
    }
    
    [Fact]
    public void TryParse_MultipleOptionValues()
    {
        // Project option should only accept one value
        var args = new[] { "--sdk", "sdk", "--project", "proj1", "proj2" };
        Assert.True(DotNetWatchOptions.TryParse(args, out var options));
        Assert.Equal("proj1", options.ProjectPath);
        AssertEx.SequenceEqual(["proj2"], options.ApplicationArguments);
    }
    
    [Fact]
    public void TryParse_AllOptionsSet()
    {
        var args = new[] { "--sdk", "sdk", "--project", "myapp.csproj", "--verbose", "--no-launch-profile", "arg1", "arg2", "arg3" };
        Assert.True(DotNetWatchOptions.TryParse(args, out var options));
        
        Assert.Equal("myapp.csproj", options.ProjectPath);
        Assert.Equal(LogLevel.Debug, options.LogLevel);
        Assert.True(options.NoLaunchProfile);
        AssertEx.SequenceEqual(["arg1", "arg2", "arg3"], options.ApplicationArguments);
    }
}
