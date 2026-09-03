// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.DotNet.HotReload;
using Microsoft.DotNet.Test.MSTest.Utilities;

namespace Microsoft.DotNet.HotReload.UnitTests;

internal sealed class TestBrowserRefreshServer()
    : AbstractBrowserRefreshServer(new TestLogger(), _ => new TestLogger(), _ => new TestLogger())
{
    public List<string> SentMessages { get; } = [];

    internal override ValueTask<TResult?> SendAndReceiveAsync<TRequest, TResult>(
        Func<string?, TRequest>? request,
        ResponseFunc<TResult>? response,
        CancellationToken cancellationToken)
        where TResult : struct
    {
        if (request != null)
        {
            var requestValue = request(null);
            var requestBytes = requestValue is ReadOnlyMemory<byte> bytes ? bytes : SerializeJson(requestValue);
            SentMessages.Add(Encoding.UTF8.GetString(requestBytes.Span));
        }

        return ValueTask.FromResult<TResult?>(null);
    }

    protected override ValueTask<WebServerHost> CreateAndStartHostAsync(CancellationToken cancellationToken)
        => throw new NotImplementedException();

    protected override bool SuppressTimeouts => true;
}
