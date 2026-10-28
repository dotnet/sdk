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
    public async Task<string> GetResult(NugetSearchApiParameter nugetSearchApiParameter)
    {
        var queryUrl = await ConstructUrl(
            nugetSearchApiParameter.SearchTerm,
            nugetSearchApiParameter.Skip,
            nugetSearchApiParameter.Take,
            nugetSearchApiParameter.Prerelease);

        var httpClient = new HttpClient();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        HttpResponseMessage response = await httpClient.GetAsync(queryUrl, cancellation.Token);
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

        return await response.Content.ReadAsStringAsync(cancellation.Token);
    }

    internal static async Task<Uri> ConstructUrl(string searchTerm = null, int? skip = null, int? take = null,
        bool prerelease = false, Uri domainAndPathOverride = null)
    {
        var uriBuilder = new UriBuilder(domainAndPathOverride ?? await DomainAndPath());
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
    private static async Task<Uri> DomainAndPath()
    {
        const string serviceIndexUrl = "https://api.nuget.org/v3/index.json";
        const string searchQueryServiceType = "SearchQueryService/3.5.0";

        using var httpClient = new HttpClient();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(serviceIndexUrl, cancellation.Token);
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        {
            // Transient network failures (DNS, connection refused, timeout) are retriable.
            throw new NugetSearchApiRequestException(
                string.Format(
                    CliStrings.RetriableNugetSearchFailure,
                    serviceIndexUrl, e.Message, "N/A"));
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
                            serviceIndexUrl, response.ReasonPhrase, response.StatusCode));
                }

                throw new NugetSearchApiRequestException(
                    string.Format(
                        CliStrings.NonRetriableNugetSearchFailure,
                        serviceIndexUrl, response.ReasonPhrase, response.StatusCode));
            }

            var indexJson = await response.Content.ReadAsStringAsync(cancellation.Token);

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
                        serviceIndexUrl, e.Message, response.StatusCode));
            }

            var resource = index?.Resources?.FirstOrDefault(r => r.Type == searchQueryServiceType)
                ?? throw new NugetSearchApiRequestException(
                    string.Format(
                        CliStrings.NonRetriableNugetSearchFailure,
                        serviceIndexUrl, $"{searchQueryServiceType} not found in service index", response.StatusCode));

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
                        serviceIndexUrl, $"{searchQueryServiceType} returned a malformed URL: {e.Message}", response.StatusCode));
            }
        }
    }
#else
    private static async Task<Uri> DomainAndPath()
    {
        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var resource = await repository.GetResourceAsync<ServiceIndexResourceV3>();
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
