// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit.Sdk;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests;

internal sealed class NullMessageSink : IMessageSink
{
    public static readonly IMessageSink Instance = new NullMessageSink();

    private NullMessageSink() { }

    public bool OnMessage(IMessageSinkMessage message) => true;
}
