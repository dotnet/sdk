// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Moq;
using Moq.Language.Flow;

namespace Aspire.Tools.Service.UnitTests;

internal class IAspireServerEventsMock : MockFactory<IAspireServerEvents>
{
    public IAspireServerEventsMock(Mocks mocks, MockBehavior? mockBehavior = null)
        : base(mocks, mockBehavior)
    {
    }

    public IAspireServerEventsMock ImplementStartProjectAsync(string dcpId, string sessionId, Exception? ex = null, bool requireNullArguments = false)
    {
        ISetup<IAspireServerEvents, ValueTask<string>> setup;
        if (requireNullArguments)
        {
            setup = MockObject.Setup(x => x.StartProjectAsync(dcpId, It.Is<ProjectLaunchRequest>(plr => plr.Arguments == null), It.IsAny<CancellationToken>()));
        }
        else
        {
            setup = MockObject.Setup(x => x.StartProjectAsync(dcpId, It.IsAny<ProjectLaunchRequest>(), It.IsAny<CancellationToken>()));
        }

        setup.Returns(() =>
        {
            if (ex is not null)
            {
                throw ex;
            }
             
            return new ValueTask<string>(sessionId);
        }).Verifiable();

        return this;
    }

    public IAspireServerEventsMock ImplementStopSessionAsync(string dcpId, string sessionId, bool exists, Exception? ex = null)
    {
        MockObject.Setup(x => x.StopSessionAsync(dcpId, sessionId, It.IsAny<CancellationToken>()))
                  .Returns(() =>
                  {
                        if (ex is not null)
                        {
                            throw ex;
                        }

                        return new ValueTask<bool>(exists);
                  })
                  .Verifiable();
        return this;
    }
}
