// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Moq;

namespace Aspire.Tools.Service.UnitTests;

public interface IMockFactory
{
    void Verify();
    object GetObject();
}

public class MockFactory<T> : IMockFactory where T : class
{
    public MockFactory(Mocks mocks, MockBehavior? mockBehavior)
    {
        AllMocks = mocks;
        MockObject = new Mock<T>(mockBehavior ?? MockBehavior.Strict);
    }

    protected Mocks AllMocks { get; }
    public Mock<T> MockObject { get; }

    public T Object => MockObject.Object;

    public virtual void Verify()
    {
        MockObject.VerifyAll();
    }

    public object GetObject()
    {
        return Object;
    }
}
