// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Moq;

namespace Microsoft.WebTools.AspireServer.UnitTests;

internal class IServiceProviderMock : MockFactory<IServiceProvider>
{
    public IServiceProviderMock(Mocks mocks, MockBehavior? mockBehavior = null)
        : base(mocks, mockBehavior)
    {
    }

    public IServiceProviderMock ImplementService(Type type, object service)
    {
        MockObject.Setup(x => x.GetService(type)).Returns(service);

        return this;
    }
}
