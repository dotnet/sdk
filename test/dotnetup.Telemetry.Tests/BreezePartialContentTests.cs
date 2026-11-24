// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.DotNet.Cli.Telemetry.Implementation;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

[TestClass]
public class BreezePartialContentTests
{
    private static byte[] Ndjson(params string[] lines)
        => Encoding.UTF8.GetBytes(string.Concat(lines.Select(l => l + "\n")));

    [TestMethod]
    public void ParseResponseReadsErrorsByIndex()
    {
        var body = Encoding.UTF8.GetBytes(
            "{\"itemsReceived\":3,\"itemsAccepted\":1,\"errors\":[{\"index\":0,\"statusCode\":500,\"message\":\"boom\"},{\"index\":2,\"statusCode\":400,\"message\":\"bad\"}]}");

        var response = BreezePartialContent.ParseResponse(body);

        response.Should().NotBeNull();
        response!.ItemsReceived.Should().Be(3);
        response.ItemsAccepted.Should().Be(1);
        response.Errors.Should().HaveCount(2);
        response.Errors![0].Index.Should().Be(0);
        response.Errors[0].StatusCode.Should().Be(500);
    }

    [TestMethod]
    public void ParseResponseReturnsNullForMalformedBody()
    {
        BreezePartialContent.ParseResponse(Encoding.UTF8.GetBytes("not json")).Should().BeNull();
        BreezePartialContent.ParseResponse([]).Should().BeNull();
    }

    [TestMethod]
    public void GetRetriablePayloadKeepsOnlyRetriableEnvelopes()
    {
        var payload = Ndjson("{\"i\":0}", "{\"i\":1}", "{\"i\":2}");
        var response = BreezePartialContent.ParseResponse(Encoding.UTF8.GetBytes(
            // 0 -> 500 (retriable), 1 -> 429 (retriable), 2 -> 400 (permanent)
            "{\"itemsReceived\":3,\"itemsAccepted\":0,\"errors\":[{\"index\":0,\"statusCode\":500},{\"index\":1,\"statusCode\":429},{\"index\":2,\"statusCode\":400}]}"));

        var retriable = BreezePartialContent.GetRetriablePayload(payload, response);

        retriable.Should().NotBeNull();
        Encoding.UTF8.GetString(retriable!).Should().Be("{\"i\":0}\n{\"i\":1}\n");
    }

    [TestMethod]
    public void GetRetriablePayloadReturnsNullWhenAllErrorsArePermanent()
    {
        var payload = Ndjson("{\"i\":0}", "{\"i\":1}");
        var response = BreezePartialContent.ParseResponse(Encoding.UTF8.GetBytes(
            "{\"itemsReceived\":2,\"itemsAccepted\":1,\"errors\":[{\"index\":0,\"statusCode\":400}]}"));

        BreezePartialContent.GetRetriablePayload(payload, response).Should().BeNull();
    }

    [TestMethod]
    public void GetRetriablePayloadReturnsNullWhenNoErrors()
    {
        var payload = Ndjson("{\"i\":0}");
        BreezePartialContent.GetRetriablePayload(payload, new TrackResponse { ItemsReceived = 1, ItemsAccepted = 1 })
            .Should().BeNull();
        BreezePartialContent.GetRetriablePayload(payload, null).Should().BeNull();
    }

    [TestMethod]
    public void GetRetriablePayloadIgnoresOutOfRangeIndices()
    {
        var payload = Ndjson("{\"i\":0}");
        var response = new TrackResponse
        {
            ItemsReceived = 1,
            ItemsAccepted = 0,
            Errors = [new TrackResponseError { Index = 5, StatusCode = 500 }],
        };

        BreezePartialContent.GetRetriablePayload(payload, response).Should().BeNull();
    }
}
