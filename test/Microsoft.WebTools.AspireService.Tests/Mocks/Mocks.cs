// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Moq;

namespace Microsoft.WebTools.AspireServer.UnitTests;

public class Mocks
{
    private readonly Dictionary<Type, IMockFactory> _mockFactories = new();

    public void Add(IMockFactory factory)
    {
        _mockFactories.Add(factory.GetType(), factory);
    }

    public T GetOrCreate<T>(MockBehavior? mockBehavior = null) where T : IMockFactory
    {
        if (_mockFactories.TryGetValue(typeof(T), out var factory))
        {
            return (T)factory;
        }

        var newMock = (IMockFactory?)Activator.CreateInstance(typeof(T), this, mockBehavior);
        Debug.Assert(newMock != null);
        Add(newMock);
        return (T)newMock;
    }

    public virtual void Verify()
    {
        foreach (var factory in _mockFactories)
        {
            factory.Value.Verify();
        }
    }
}
