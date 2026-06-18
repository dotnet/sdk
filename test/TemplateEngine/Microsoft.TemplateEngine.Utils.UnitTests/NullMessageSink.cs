// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Utils.UnitTests;

/// <summary>
/// A no-op <see cref="IMessageSink"/> used to satisfy
/// <see cref="TemplateEngine.TestHelper.EnvironmentSettingsHelper"/>'s constructor
/// in an MSTest context (no xUnit runner is present to provide one).
/// </summary>
internal sealed class NullMessageSink : IMessageSink
{
    public static readonly IMessageSink Instance = new NullMessageSink();

    private NullMessageSink()
    {
    }

    public bool OnMessage(IMessageSinkMessage message) => true;
}
