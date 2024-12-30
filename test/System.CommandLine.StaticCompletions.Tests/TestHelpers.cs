// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions.Tests;

public class DynamicOption<T> : CliOption<T>, IDynamicOption
{
    public DynamicOption(string name) : base(name)
    {
    }
}

public class DynamicArgument<T> : CliArgument<T>, IDynamicArgument
{
    public DynamicArgument(string name) : base(name)
    {
    }
}
