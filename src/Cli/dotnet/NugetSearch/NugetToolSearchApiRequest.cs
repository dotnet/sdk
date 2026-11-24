// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Specialized;
using System.Web;
#if CLI_AOT
using System.Text.Json;
using System.Text.Json.Serialization;
#else
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
#endif

namespace Microsoft.DotNet.Cli.NugetSearch;

internal class NugetToolSearchApiRequest : INugetToolSearchApiRequest
{
    private const string ServiceIndexUrl = "https://api.nuget.org/v3/index.json";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public async Task<string> GetResult(
        NugetSearchApiParameter nugetSearchApiParameter,
        CancellationToken cancellationToken)
        => await ExecuteRequest(
            token => ConstructUrl(
                nugetSearchApiParameter.SearchTerm,
                nugetSearchApiParameter.Skip,
                nugetSearchApiParameter.Take,
                nugetSearchApiParameter.Prerelease,
                token),
            GetResultCore,
            cancellationToken,
            RequestTimeout);

    private static async Task<string> GetResultCore(
        Uri queryUrl,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        using HttpResponseMessage response = await httpClient.GetAsync(queryUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode >= 500 && (int)response.StatusCode < 600)
            {
                throw new NugetSearchApiRequestException(
                    string.Format(
                        CliStrings.RetriableNugetSearchFailure,
                        queryUrl.AbsoluteUri, response.ReasonPhrase, response.StatusCode));
            }

            throw new NugetSearchApiRequestException(
                string.Format(
                    CliStrings.NonRetriableNugetSearchFailure,
                    queryUrl.AbsoluteUri, response.ReasonPhrase, response.StatusCode));
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    internal static async Task<T> ExecuteRequest<T>(
        Func<CancellationToken, Task<Uri>> constructUrl,
        Func<Uri, CancellationToken, Task<T>> executeQuery,
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        Uri queryUrl = await constructUrl(cancellationToken);
        return await ExecuteWithTimeout(
            token => executeQuery(queryUrl, token),
            cancellationToken,
            timeout,
            queryUrl.AbsoluteUri);
    }

    internal static async Task<T> ExecuteWithTimeout<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken,
        TimeSpan timeout,
        string requestUrl = ServiceIndexUrl)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);

        try
        {
            return await operation(timeoutCancellation.Token);
        }
        catch (OperationCanceledException e) when (
            timeoutCancellation.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            throw new NugetSearchApiRequestException(
                string.Format(
                    CliStrings.RetriableNugetSearchFailure,
                    requestUrl,
                    e.Message,
                    "N/A"));
        }
    }

    internal static async Task<Uri> ConstructUrl(string searchTerm = null, int? skip = null, int? take = null,
        bool prerelease = false, CancellationToken cancellationToken = default, Uri domainAndPathOverride = null)
    {
        var uriBuilder = new UriBuilder(domainAndPathOverride ?? await DomainAndPath(cancellationToken));
        NameValueCollection query = HttpUtility.ParseQueryString(uriBuilder.Query);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query["q"] = searchTerm;
        }

        query["packageType"] = "dotnettool";

        // This is a field for internal nuget back
        // compatibility should be "2.0.0" for all new API usage
        query["semVerLevel"] = "2.0.0";

        if (skip.HasValue)
        {
            query["skip"] = skip.Value.ToString();
        }

        if (take.HasValue)
        {
            query["take"] = take.Value.ToString();
        }

        if (prerelease)
        {
            query["prerelease"] = "true";
        }

        uriBuilder.Query = query.ToString();

        return uriBuilder.Uri;
    }

    // More detail on this API https://github.com/dotnet/sdk/issues/12038
#if CLI_AOT
    // NuGet.Protocol's service-index resolution isn't AOT-compatible, so under AOT we read the
    // service index directly over HTTP and select the SearchQueryService endpoint using
    // System.Text.Json source generation. Failures are translated into NugetSearchApiRequestException
    // (a GracefulException) with the same retriable/non-retriable messaging as GetResult.
    private static async Task<Uri> DomainAndPath(CancellationToken cancellationToken)
        => await ExecuteWithTimeout(
            DomainAndPathCore,
            cancellationToken,
            RequestTimeout);

    private static async Task<Uri> DomainAndPathCore(CancellationToken cancellationToken)
    {
        const string searchQueryServiceType = "SearchQueryService/3.5.0";

        using var httpClient = new HttpClient();
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(ServiceIndexUrl, cancellationToken);
        }
        catch (HttpRequestException e)
        {
            // Transient network failures (DNS, connection refused, timeout) are retriable.
            throw new NugetSearchApiRequestException(
                string.Format(
                    CliStrings.RetriableNugetSearchFailure,
                    ServiceIndexUrl, e.Message, "N/A"));
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode >= 500 && (int)response.StatusCode < 600)
                {
                    throw new NugetSearchApiRequestException(
                        string.Format(
                            CliStrings.RetriableNugetSearchFailure,
                            ServiceIndexUrl, response.ReasonPhrase, response.StatusCode));
                }

                throw new NugetSearchApiRequestException(
                    string.Format(
                        CliStrings.NonRetriableNugetSearchFailure,
                        ServiceIndexUrl, response.ReasonPhrase, response.StatusCode));
            }

            var indexJson = await response.Content.ReadAsStringAsync(cancellationToken);

            NugetServiceIndex index;
            try
            {
                index = JsonSerializer.Deserialize(indexJson, NugetServiceIndexJsonSerializerContext.Default.NugetServiceIndex);
            }
            catch (JsonException e)
            {
                throw new NugetSearchApiRequestException(
                    string.Format(
                        CliStrings.NonRetriableNugetSearchFailure,
                        ServiceIndexUrl, e.Message, response.StatusCode));
            }

            var resource = index?.Resources?.FirstOrDefault(r => r.Type == searchQueryServiceType)
                ?? throw new NugetSearchApiRequestException(
                    string.Format(
                        CliStrings.NonRetriableNugetSearchFailure,
                        ServiceIndexUrl, $"{searchQueryServiceType} not found in service index", response.StatusCode));

            // The service index is server-supplied, so don't trust the URL to be well-formed.
            try
            {
                return new Uri(resource.Id);
            }
            catch (Exception e) when (e is UriFormatException or ArgumentException)
            {
                throw new NugetSearchApiRequestException(
                    string.Format(
                        CliStrings.NonRetriableNugetSearchFailure,
                        ServiceIndexUrl, $"{searchQueryServiceType} returned a malformed URL: {e.Message}", response.StatusCode));
            }
        }
    }
#else
    private static async Task<Uri> DomainAndPath(CancellationToken cancellationToken)
    {
        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var resource = await repository.GetResourceAsync<ServiceIndexResourceV3>(cancellationToken);
        var uris = resource.GetServiceEntryUris("SearchQueryService/3.5.0");
        return uris[0];
    }
#endif
}

#if CLI_AOT
internal sealed class NugetServiceIndex
{
    [JsonPropertyName("resources")]
    public NugetServiceResource[] Resources { get; set; }
}

internal sealed class NugetServiceResource
{
    [JsonPropertyName("@id")]
    public string Id { get; set; }

    [JsonPropertyName("@type")]
    public string Type { get; set; }
}

[JsonSerializable(typeof(NugetServiceIndex))]
internal partial class NugetServiceIndexJsonSerializerContext : JsonSerializerContext;
#endif
