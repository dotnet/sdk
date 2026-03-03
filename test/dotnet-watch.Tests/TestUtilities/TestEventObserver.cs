// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

internal class TestEventObserver
{
    private readonly Dictionary<EventId, Action> _actions = [];

    public SemaphoreSlim RegisterSemaphore(MessageDescriptor descriptor)
    {
        var semaphore = new SemaphoreSlim(initialCount: 0);
        RegisterAction(descriptor, () => semaphore.Release());
        return semaphore;
    }

    public void RegisterAction(MessageDescriptor eventId, Action action)
        => RegisterAction(eventId.Id, action);

    public void RegisterAction(EventId eventId, Action action)
    {
        if (_actions.TryGetValue(eventId, out var existing))
        {
            existing += action;
        }
        else
        {
            existing = action;
        }

        _actions[eventId] = existing;
    }

    public void Observe(EventId eventId)
    {
        if (_actions.TryGetValue(eventId, out var action))
        {
            action();
        }
    }
}
