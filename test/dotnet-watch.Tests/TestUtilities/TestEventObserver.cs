// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

/// <summary>
/// Used to observe events logged by the watcher.
///
/// Usage pattern: register all actions and semaphores first, then start the watcher.
/// </summary>
internal class TestEventObserver
{
    private readonly Dictionary<EventId, Action> _actions = [];
    private bool _frozen;

    public void Freeze()
        => _frozen = true;

    private void RequireNotFrozen()
    {
        if (_frozen)
        {
            throw new InvalidOperationException("Cannot register actions after the observer is frozen.");
        }
    }

    public SemaphoreSlim RegisterSemaphore(MessageDescriptor descriptor)
    {
        RequireNotFrozen();

        var semaphore = new SemaphoreSlim(initialCount: 0);
        RegisterAction(descriptor, () => semaphore.Release());
        return semaphore;
    }

    public void RegisterAction(MessageDescriptor eventId, Action action)
        => RegisterAction(eventId.Id, action);

    public void RegisterAction(EventId eventId, Action action)
    {
        RequireNotFrozen();

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
