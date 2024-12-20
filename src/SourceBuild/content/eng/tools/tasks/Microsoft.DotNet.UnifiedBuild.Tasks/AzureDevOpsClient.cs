// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.DotNet.UnifiedBuild.Tasks;

public class AzureDevOpsClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly TaskLoggingHelper _logger;

    private const string _azureDevOpsApiVersion = "7.1-preview.5";
    // download in 100 MB chunks
    private const int _downloadBufferSize = 1024 * 1024 * 100;
    private const int _httpTimeoutSeconds = 300;

    public AzureDevOpsClient(
        string? azureDevOpsToken,
        string azureDevOpsBaseUri,
        string azureDevOpsProject,
        TaskLoggingHelper logger)
    {

        _logger = logger;

        _httpClient = new(new HttpClientHandler { CheckCertificateRevocationList = true });

        _httpClient.BaseAddress = new Uri($"{azureDevOpsBaseUri}/{azureDevOpsProject}/_apis/");

        _httpClient.Timeout = TimeSpan.FromSeconds(_httpTimeoutSeconds);

        if (!string.IsNullOrEmpty(azureDevOpsToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($":{azureDevOpsToken}")));
        }
    }

    /// <summary>
    /// Downloads a build artifact as zip file
    /// </summary>
    public async Task DownloadArtifactZip(string buildId, string artifactName, string downloadPath, int retryCount)
    {
        var artifactInformation = await GetArtifactInformation(buildId, artifactName, retryCount);
        string downloadUrl = artifactInformation.Resource.DownloadUrl;

        _logger.LogMessage(MessageImportance.High, $"Downloading artifact zip from {downloadUrl}");

        try
        {
            using HttpResponseMessage httpResponse = await ExecuteApiCallWithRetry(downloadUrl, retryCount);
            using Stream readStream = await httpResponse.Content.ReadAsStreamAsync();
            using FileStream writeStream = File.Create(downloadPath);

            await readStream.CopyToAsync(writeStream, _downloadBufferSize);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to download artifact zip: {ex.Message}");
            throw;
        }
    }

    public async Task DownloadSingleFileFromArtifact(string buildId, string artifactName, string itemId, string itemSubPath, string downloadPath, int retryCount)
    {
        try
        {
            var downloadFileUrl = $"build/builds/{buildId}/artifacts?artifactName={artifactName}&fileId={itemId}&fileName={itemSubPath}&api-version={_azureDevOpsApiVersion}";

            _logger.LogMessage(MessageImportance.High, $"Downloading file {itemSubPath} from {downloadFileUrl}");

            using HttpResponseMessage fileDownloadResponse = await ExecuteApiCallWithRetry(downloadFileUrl, retryCount);
            using Stream readStream = await fileDownloadResponse.Content.ReadAsStreamAsync();
            using FileStream writeStream = File.Create(downloadPath);

            await readStream.CopyToAsync(writeStream, _downloadBufferSize);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to download file: {ex.Message}");
            throw;
        }
    }

    public async Task<ArtifactFiles> GetArtifactFilesInformation(string buildId, string artifactName, int retryCount)
    {
        var artifactInformation = await GetArtifactInformation(buildId, artifactName, retryCount);
        string artifactId = artifactInformation.Resource.Data;

        var getManifestUrl = $"build/builds/{buildId}/artifacts?artifactName={artifactName}&fileId={artifactId}&fileName={artifactName}&api-version={_azureDevOpsApiVersion}";

        _logger.LogMessage(MessageImportance.High, $"Getting {artifactName} artifact manifest");

        try
        {
            using HttpResponseMessage httpResponse = await ExecuteApiCallWithRetry(getManifestUrl, retryCount);

            ArtifactFiles filesInformation = await httpResponse.Content.ReadFromJsonAsync<ArtifactFiles>()
                ?? throw new ArgumentException($"Couldn't parse AzDo response {httpResponse.Content} to {nameof(ArtifactFiles)}");

            return filesInformation;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to download file: {ex.Message}");
            throw;
        }
    }
    
    public async Task<AzureDevOpsArtifactInformation> GetArtifactInformation(string buildId, string artifactName, int retryCount)
    {
        string relativeUrl = $"build/builds/{buildId}/artifacts?artifactName={artifactName}&api-version={_azureDevOpsApiVersion}";
        
        _logger.LogMessage(MessageImportance.High, $"Getting {artifactName} metadata from {relativeUrl}");

        try
        {
            using HttpResponseMessage httpResponse = await ExecuteApiCallWithRetry(relativeUrl, retryCount);

            AzureDevOpsArtifactInformation artifactInformation = await httpResponse.Content.ReadFromJsonAsync<AzureDevOpsArtifactInformation>()
                ?? throw new ArgumentException($"Couldn't parse AzDo response {httpResponse.Content} to {nameof(AzureDevOpsArtifactInformation)}");

            return artifactInformation;
        }
        catch(Exception ex)
        {
            _logger.LogError($"Failed to get artifact download URL: {ex.Message}");
            throw;
        }
    }

    private async Task<HttpResponseMessage> ExecuteApiCallWithRetry(string relativeUrl, int retryCount)
    {
        int retriesRemaining = retryCount;

        while (true)
        {
            try
            {
                HttpResponseMessage httpResponse = await _httpClient.GetAsync(relativeUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

                httpResponse.EnsureSuccessStatusCode();

                return httpResponse;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                if (ex is HttpRequestException && ex.Message.Contains(((int)HttpStatusCode.NotFound).ToString()))
                {
                    _logger.LogError($"Resource not found at {relativeUrl}: {ex.Message}");
                    throw;
                }

                if (ex is HttpRequestException && ex.Message.Contains(((int)HttpStatusCode.Unauthorized).ToString()))
                {
                    _logger.LogError($"Failure to authenticate: {ex.Message}");
                    throw;
                }

                if (retriesRemaining <= 0)
                {
                    _logger.LogError($"There was an error calling AzureDevOps API against URI '{relativeUrl}' " +
                                     $"after {retryCount} attempts. Exception: {ex}");
                    throw;
                }

                _logger.LogWarning($"There was an error calling AzureDevOps API against URI against URI '{relativeUrl}'. " +
                                   $"{retriesRemaining} attempts remaining. Exception: {ex.ToString()}");
            }

            --retriesRemaining;
            await Task.Delay(5000);
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    public record Blob(string Id, int Size);
    public record ArtifactItem(string Path, Blob Blob);
    public record ArtifactFiles(string ManifestFormat, ArtifactItem[] Items, string[] ManifestReferences);
}