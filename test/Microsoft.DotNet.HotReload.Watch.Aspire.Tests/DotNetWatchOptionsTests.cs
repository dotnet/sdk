// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

public class DotNetWatchOptionsTests
{
    [Fact]
    public void TryParse_ApplicationArguments()
    {
        var args = new[] { "--project", "proj", "--verbose", "a", "b" };
        Assert.True(DotNetWatchOptions.TryParse(args, out var options));
        AssertEx.SequenceEqual(["a", "b"], options.ApplicationArguments);
    }

    [Fact]
    public void TryParse_RequiredProjectOption()
    {
        // Project option is missing
        var args = new[] { "--verbose", "a", "b" };
        Assert.False(DotNetWatchOptions.TryParse(args, out var options));
        Assert.Null(options);
    }

    [Fact]
    public void TryParse_ProjectPath()
    {
        var args = new[] { "--project", "myproject.csproj" };
        Assert.True(DotNetWatchOptions.TryParse(args, out var options));
        Assert.Equal("myproject.csproj", options.ProjectPath);
        Assert.Empty(options.ApplicationArguments);
    }

    [Fact]
    public void TryParse_VerboseOption()
    {
        // With verbose flag
        var argsVerbose = new[] { "--project", "proj", "--verbose" };
        Assert.True(DotNetWatchOptions.TryParse(argsVerbose, out var optionsVerbose));
        Assert.True(optionsVerbose.IsVerbose);
        
        // Without verbose flag
        var argsNotVerbose = new[] { "--project", "proj" };
        Assert.True(DotNetWatchOptions.TryParse(argsNotVerbose, out var optionsNotVerbose));
        Assert.False(optionsNotVerbose.IsVerbose);
    }

    [Fact]
    public void TryParse_QuietOption()
    {
        // With quiet flag
        var argsQuiet = new[] { "--project", "proj", "--quiet" };
        Assert.True(DotNetWatchOptions.TryParse(argsQuiet, out var optionsQuiet));
        Assert.True(optionsQuiet.IsQuiet);
        
        // Without quiet flag
        var argsNotQuiet = new[] { "--project", "proj" };
        Assert.True(DotNetWatchOptions.TryParse(argsNotQuiet, out var optionsNotQuiet));
        Assert.False(optionsNotQuiet.IsQuiet);
    }
    
    [Fact]
    public void TryParse_NoLaunchProfileOption()
    {
        // With no-launch-profile flag
        var argsNoProfile = new[] { "--project", "proj", "--no-launch-profile" };
        Assert.True(DotNetWatchOptions.TryParse(argsNoProfile, out var optionsNoProfile));
        Assert.True(optionsNoProfile.NoLaunchProfile);
        
        // Without no-launch-profile flag
        var argsWithProfile = new[] { "--project", "proj" };
        Assert.True(DotNetWatchOptions.TryParse(argsWithProfile, out var optionsWithProfile));
        Assert.False(optionsWithProfile.NoLaunchProfile);
    }
        
    [Fact]
    public void TryParse_ConflictingOptions()
    {
        // Cannot specify both --quiet and --verbose
        var args = new[] { "--project", "proj", "--quiet", "--verbose" };
        Assert.False(DotNetWatchOptions.TryParse(args, out var options));
        Assert.Null(options);
    }
    
    [Fact]
    public void TryParse_MultipleOptionValues()
    {
        // Project option should only accept one value
        var args = new[] { "--project", "proj1", "proj2" };
        Assert.True(DotNetWatchOptions.TryParse(args, out var options));
        Assert.Equal("proj1", options.ProjectPath);
        AssertEx.SequenceEqual(["proj2"], options.ApplicationArguments);
    }
    
    [Fact]
    public void TryParse_AllOptionsSet()
    {
        var args = new[] { "--project", "myapp.csproj", "--verbose", "--no-launch-profile", "arg1", "arg2", "arg3" };
        Assert.True(DotNetWatchOptions.TryParse(args, out var options));
        
        Assert.Equal("myapp.csproj", options.ProjectPath);
        Assert.True(options.IsVerbose);
        Assert.False(options.IsQuiet);
        Assert.True(options.NoLaunchProfile);
        AssertEx.SequenceEqual(["arg1", "arg2", "arg3"], options.ApplicationArguments);
    }
}
