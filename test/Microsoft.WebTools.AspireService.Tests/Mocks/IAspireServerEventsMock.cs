// Copyright (c) Microsoft Corporation. All rights reserved.

using Moq;

namespace Aspire.Tools.Service.UnitTests;

internal class IAspireServerEventsMock : MockFactory<IAspireServerEvents>
{
    public IAspireServerEventsMock(Mocks mocks, MockBehavior? mockBehavior = null)
        : base(mocks, mockBehavior)
    {
    }

    public IAspireServerEventsMock ImplementStartProjectAsync(string dcpId, string sessionId, Exception? ex = null)
    {
        MockObject.Setup(x => x.StartProjectAsync(dcpId, It.IsAny<ProjectLaunchRequest>(), It.IsAny<CancellationToken>()))
                  .Returns(() =>
                  {
                        if (ex is not null)
                        {
                            throw ex;
                        }

                        return new ValueTask<string>(sessionId);
                  })
                  .Verifiable();
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
