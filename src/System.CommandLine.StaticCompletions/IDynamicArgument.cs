// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions;

/// <summary>
/// Represents an Argument whose completions are dynamically generated and so should not be emitted in static completion scripts.
/// </summary>
public interface IDynamicArgument;
