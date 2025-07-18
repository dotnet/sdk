﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Xunit.Sdk;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class DockerRegistryManager
{
    public const string RuntimeBaseImage = "dotnet/runtime";
    public const string AspNetBaseImage = "dotnet/aspnet";
    public const string BaseImageSource = "mcr.microsoft.com";
    public const string Net6ImageTag = "6.0";
    public const string Net7ImageTag = "7.0";
    public const string Net8ImageTag = "8.0";
    public const string Net9ImageTag = "9.0";
    public const string Net9ImageDigest = "sha256:d8f01f752bf9bd3ff630319181a2ccfbeecea4080a1912095a34002f61bfa345";
    public const string Net8PreviewWindowsSpecificImageTag = $"{Net8ImageTag}-nanoserver-ltsc2022";
    public const string LocalRegistry = "localhost:5010";
    public const string FullyQualifiedBaseImageDefault = $"{BaseImageSource}/{RuntimeBaseImage}:{Net9ImageTag}";
    public const string FullyQualifiedBaseImageAspNet = $"{BaseImageSource}/{AspNetBaseImage}:{Net9ImageTag}";
    public const string FullyQualifiedBaseImageAspNetDigest = $"{BaseImageSource}/{AspNetBaseImage}@{Net9ImageDigest}";
    private static string? s_registryContainerId;

    private static string SDK_AzureContainerRegistryImage => "dotnetdhmirror-f8bzbjakh8cga6ab.azurecr.io/registry:2";
    private static string Docker_HubRegistryImage => "docker.io/library/registry:2";

    // TODO: some logic to pivot between this and Docker Hub
    private static string RegistryImageToUse => SDK_AzureContainerRegistryImage;

    internal class SameArchManifestPicker : IManifestPicker
    {
        public PlatformSpecificManifest? PickBestManifestForRid(IReadOnlyDictionary<string, PlatformSpecificManifest> manifestList, string runtimeIdentifier)
        {
            return manifestList.Values.SingleOrDefault(m => m.platform.os == "linux" && m.platform.architecture == "amd64");
        }

        public PlatformSpecificOciManifest? PickBestManifestForRid(IReadOnlyDictionary<string, PlatformSpecificOciManifest> manifestList, string runtimeIdentifier)
        {
            return manifestList.Values.SingleOrDefault(m => m.platform.os == "linux" && m.platform.architecture == "amd64");
        }
    }

    public static async Task StartAndPopulateDockerRegistry(ITestOutputHelper testOutput)
    {
        using TestLoggerFactory loggerFactory = new(testOutput);

        if (!new DockerCli(loggerFactory).IsAvailable())
        {
            throw new InvalidOperationException("Docker is not available, tests cannot run");
        }

        ILogger logger = loggerFactory.CreateLogger("Docker Registry Init");

        const int spawnRegistryMaxRetry = 5;
        int spawnRegistryDelay = 1000; //ms
        StringBuilder failureReasons = new();

        var pullRegistry = new Registry(BaseImageSource, logger, RegistryMode.Pull);
        var pushRegistry = new Registry(LocalRegistry, logger, RegistryMode.Push);

        for (int spawnRegistryAttempt = 1; spawnRegistryAttempt <= spawnRegistryMaxRetry; spawnRegistryAttempt++)
        {
            try
            {
                logger.LogInformation("Spawning local registry at '{registry}', attempt #{attempt}.", LocalRegistry, spawnRegistryAttempt);

                CommandResult processResult = ContainerCli.RunCommand(testOutput, "--rm", "--publish", "5010:5000", "--detach", RegistryImageToUse).Execute();

                processResult.Should().Pass().And.HaveStdOut();

                logger.LogInformation("StdOut: {stream}", processResult.StdOut);
                logger.LogInformation("StdErr: {stream}", processResult.StdErr);

                using var reader = new StringReader(processResult.StdOut!);
                s_registryContainerId = reader.ReadLine();

                EnsureRegistryLoaded(new Uri($"http://{LocalRegistry}"), s_registryContainerId, logger, testOutput);

                foreach (string? tag in new[] { Net6ImageTag, Net7ImageTag, Net8ImageTag, Net9ImageTag })
                {
                    logger.LogInformation("Pulling image '{repo}/{image}:{tag}'.", BaseImageSource, RuntimeBaseImage, tag);
                    string dotnetdll = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    var ridjson = Path.Combine(Path.GetDirectoryName(dotnetdll)!, "RuntimeIdentifierGraph.json");

                    var image = await pullRegistry.GetImageManifestAsync(RuntimeBaseImage, tag, "linux-x64", new SameArchManifestPicker(), CancellationToken.None);
                    var source = new SourceImageReference(pullRegistry, RuntimeBaseImage, tag, null);
                    var dest = new DestinationImageReference(pushRegistry, RuntimeBaseImage, [tag]);
                    logger.LogInformation($"Pushing image for {BaseImageSource}/{RuntimeBaseImage}:{tag}");
                    await pushRegistry.PushAsync(image.Build(), source, dest, CancellationToken.None);
                    logger.LogInformation($"Pushed image  for {BaseImageSource}/{RuntimeBaseImage}:{tag}");
                }

                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Spawn registry attempt #{attempt} failed.", spawnRegistryAttempt);
                // logging is not easily available, so collect error messages to throw in final exception if needed.
                failureReasons.AppendLine($"Spawn registry attempt #{spawnRegistryAttempt} failed, {ex}");

                //stop registry, if started and ignore errors.
                if (!string.IsNullOrWhiteSpace(s_registryContainerId))
                {
                    try
                    {
                        ContainerCli.StopCommand(testOutput, s_registryContainerId).Execute();
                    }
                    catch (Exception ex2)
                    {
                        logger.LogError(ex2, "Failed to stop the registry {id}.", s_registryContainerId);
                    }
                }

                logger.LogInformation("Retrying after {delay} ms.", spawnRegistryDelay);
                Thread.Sleep(spawnRegistryDelay);
                spawnRegistryDelay *= 2;
            }
        }
        throw new InvalidOperationException($"The registry was not loaded after {spawnRegistryMaxRetry} retries. {failureReasons}");
    }

    public static void ShutdownDockerRegistry(ITestOutputHelper testOutput)
    {
        if (s_registryContainerId != null)
        {
            ContainerCli.StopCommand(testOutput, s_registryContainerId)
                .Execute()
                .Should().Pass();
        }
    }

    private static void EnsureRegistryLoaded(Uri registryBaseUri, string? containerRegistryId, ILogger logger, ITestOutputHelper testOutput)
    {
        const int registryLoadMaxRetry = 10;
        const int registryLoadTimeout = 1000; //ms

        using HttpClient client = new();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(registryBaseUri, "/v2/"));

        logger.LogInformation("Checking if the registry '{registry}' is available.", registryBaseUri);

        int attempt = 1;
        while (attempt <= registryLoadMaxRetry)
        {
            //added an additional delay to allow registry to load
            Thread.Sleep(registryLoadTimeout);

            try
            {
                HttpResponseMessage response = client.Send(request);

                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("The registry '{registry}' is available after {timeout} ms.", registryBaseUri, attempt * registryLoadTimeout);
                    return;
                }
                logger.LogWarning("The registry '{registry} is not loaded after {timeout} ms. Returned status code: {statusCode}.", registryBaseUri, attempt * registryLoadTimeout, response.StatusCode);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "The registry '{registry} is not loaded after {timeout} ms.", registryBaseUri, attempt * registryLoadTimeout);
            }
            attempt++;
        }
        logger.LogError("The registry was not loaded after {timeout} ms.", registryLoadMaxRetry * registryLoadTimeout);
        if (string.IsNullOrWhiteSpace(containerRegistryId))
        {
            return;
        }

        //try to collect the logs from started registry for more info
        try
        {
            CommandResult logsResult = ContainerCli.LogsCommand(testOutput, containerRegistryId).Execute();
            logger.LogInformation("Registry logs: {stdout} {stderr}", logsResult.StdOut, logsResult.StdErr);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to gather logs from container {id}.", containerRegistryId);
        }
    }
}
