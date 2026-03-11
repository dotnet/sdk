// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Handles downloading and parsing .NET release manifests to find the correct installer/archive for a given installation.
/// </summary>
internal class DotnetArchiveDownloader : IArchiveDownloader
{
    private const int MaxRetryCount = 3;
    private const int RetryDelayMilliseconds = 1000;

    private readonly HttpClient _httpClient;
    private readonly bool _shouldDisposeHttpClient;
    private readonly ReleaseManifest _releaseManifest;
    private readonly DownloadCache _downloadCache;

    public DotnetArchiveDownloader()
        : this(new ReleaseManifest())
    {
    }

    public DotnetArchiveDownloader(ReleaseManifest releaseManifest, HttpClient? httpClient = null)
    {
        _releaseManifest = releaseManifest ?? throw new ArgumentNullException(nameof(releaseManifest));
        _downloadCache = new DownloadCache();
        if (httpClient == null)
        {
            _httpClient = CreateDefaultHttpClient();
            _shouldDisposeHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
            _shouldDisposeHttpClient = false;
        }
    }

    /// <summary>
    /// Creates an HttpClient with enhanced proxy support for enterprise environments.
    /// </summary>
    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new HttpClientHandler()
        {
            UseProxy = true,
            UseDefaultCredentials = true,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            // Do NOT set AutomaticDecompression here. The archives are .tar.gz files
            // whose gzip layer is handled explicitly by DecompressTarGzIfNeeded().
            // Enabling automatic decompression causes HttpClient to add Accept-Encoding: gzip
            // and transparently strip the gzip layer when the CDN returns Content-Encoding: gzip,
            // resulting in a raw .tar on disk whose hash does not match the manifest's .tar.gz hash.
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        // Set user-agent to identify dotnetup in telemetry, including version
        var informationalVersion = typeof(DotnetArchiveDownloader).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string userAgent = informationalVersion == null ? "dotnetup-dotnet-installer" : $"dotnetup-dotnet-installer/{informationalVersion}";

        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

        return client;
    }

    /// <summary>
    /// Downloads the archive from the specified URL to the destination path with progress reporting.
    /// </summary>
    /// <param name="downloadUrl">The URL to download from</param>
    /// <param name="destinationPath">The local path to save the downloaded file</param>
    /// <param name="progress">Optional progress reporting</param>
    private async Task DownloadArchiveAsync(string downloadUrl, string destinationPath, IProgress<DownloadProgress>? progress = null)
    {
        // Create temp file path in same directory for atomic move when complete
        string tempPath = $"{destinationPath}.download";

        for (int attempt = 1; attempt <= MaxRetryCount; attempt++)
        {
            try
            {
                // Ensure the directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                // Content length for progress reporting
                long? totalBytes = null;

                // Make the actual download request
                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentLength.HasValue)
                {
                    totalBytes = response.Content.Headers.ContentLength.Value;
                }

                using var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[81920]; // 80KB buffer
                long bytesRead = 0;
                int read;

                var lastProgressReport = DateTime.MinValue;

                while ((read = await contentStream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);

                    bytesRead += read;

                    // Report progress at most every 100ms to avoid UI thrashing
                    var now = DateTime.UtcNow;
                    if ((now - lastProgressReport).TotalMilliseconds > 100)
                    {
                        lastProgressReport = now;
                        progress?.Report(new DownloadProgress(bytesRead, totalBytes));
                    }
                }

                // Final progress report
                progress?.Report(new DownloadProgress(bytesRead, totalBytes));

                // Ensure all data is written to disk
                await fileStream.FlushAsync().ConfigureAwait(false);
                fileStream.Close();

                // Atomic move to final destination
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                File.Move(tempPath, destinationPath);

                return;
            }
            catch (Exception)
            {
                if (attempt < MaxRetryCount)
                {
                    await Task.Delay(RetryDelayMilliseconds * attempt).ConfigureAwait(false); // Linear backoff
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                // Delete the partial download if it exists
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }

            }
        }

    }

    /// <summary>
    /// Downloads the archive from the specified URL to the destination path (synchronous version).
    /// </summary>
    /// <param name="downloadUrl">The URL to download from</param>
    /// <param name="destinationPath">The local path to save the downloaded file</param>
    /// <param name="progress">Optional progress reporting</param>
    private void DownloadArchive(string downloadUrl, string destinationPath, IProgress<DownloadProgress>? progress = null)
    {
        DownloadArchiveAsync(downloadUrl, destinationPath, progress).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Downloads the archive for the specified installation and verifies its hash.
    /// Checks the download cache first to avoid re-downloading.
    /// </summary>
    /// <param name="installRequest">The .NET installation request details</param>
    /// <param name="resolvedVersion">The resolved version to download</param>
    /// <param name="destinationPath">The local path to save the downloaded file</param>
    /// <param name="progress">Optional progress reporting</param>
    public void DownloadArchiveWithVerification(
        DotnetInstallRequest installRequest,
        ReleaseVersion resolvedVersion,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null)
    {
        var targetFile = _releaseManifest.FindReleaseFile(installRequest, resolvedVersion);

        if (targetFile == null)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.NoMatchingReleaseFileForPlatform,
                $"No matching file found for {installRequest.Component} version {resolvedVersion} on {installRequest.InstallRoot.Architecture}",
                version: resolvedVersion.ToString(),
                component: installRequest.Component.ToString());
        }

        string? downloadUrl = targetFile.Address.ToString();
        string? expectedHash = targetFile.Hash.ToString();

        if (string.IsNullOrEmpty(expectedHash))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ManifestParseFailed,
                $"No hash found in manifest for {resolvedVersion}",
                version: resolvedVersion.ToString(),
                component: installRequest.Component.ToString());
        }
        if (string.IsNullOrEmpty(downloadUrl))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ManifestParseFailed,
                $"No download URL found in manifest for {resolvedVersion}",
                version: resolvedVersion.ToString(),
                component: installRequest.Component.ToString());
        }

        Activity.Current?.SetTag("download.url_domain", UrlSanitizer.SanitizeDomain(downloadUrl));

        // Check the cache first
        string? cachedFilePath = _downloadCache.GetCachedFilePath(downloadUrl);
        if (cachedFilePath != null)
        {
            try
            {
                // Verify the cached file's hash
                VerifyFileHash(cachedFilePath, expectedHash);

                // Copy from cache to destination
                File.Copy(cachedFilePath, destinationPath, overwrite: true);

                // Report 100% progress immediately since we're using cache
                progress?.Report(new DownloadProgress(100, 100));

                var cachedFileInfo = new FileInfo(cachedFilePath);
                Activity.Current?.SetTag("download.bytes", cachedFileInfo.Length);
                Activity.Current?.SetTag("download.from_cache", true);
                return;
            }
            catch
            {
                // If cached file is corrupted, fall through to download
            }
        }

        // Download the file if not in cache or cache is invalid
        DownloadArchive(downloadUrl, destinationPath, progress);

        // Verify the downloaded file
        VerifyFileHash(destinationPath, expectedHash);

        var fileInfo = new FileInfo(destinationPath);
        Activity.Current?.SetTag("download.bytes", fileInfo.Length);
        Activity.Current?.SetTag("download.from_cache", false);

        // Add the verified file to the cache
        try
        {
            _downloadCache.AddToCache(downloadUrl, destinationPath);
        }
        catch
        {
            // Ignore errors adding to cache - it's not critical
        }
    }

    /// <summary>
    /// Computes the SHA512 hash of a file.
    /// </summary>
    /// <param name="filePath">Path to the file to hash</param>
    /// <returns>The hash as a lowercase hex string</returns>
    public static string ComputeFileHash(string filePath)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        // TODO: Older runtime versions use a different SHA algorithm.
        // Eventually the manifest should indicate which algorithm to use.
        using var sha512 = SHA512.Create();
        byte[] hashBytes = sha512.ComputeHash(fileStream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    // Known alternate acceptable hashes keyed by the manifest's expected hash.
    // Some CDN-served archives may have a valid alternate hash due to re-signing or
    // repackaging. Each entry maps a manifest-listed expected hash to the single
    // alternate hash that should also be accepted for that archive.
    private static readonly Dictionary<string, string> s_knownAlternateHashes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["af1cb62e29c69648ebe334e651c2703cd5e87fa0bb28c670bacb3b3dd1608aeae35ae53402c5eb4ed8bf34abd831a08ccb5ef84e5ec70617d9f8d9969fe7b8fa"
        ] = "5eb176f4df5ea0795cf29f01661913cf1f5b8c8b889e80c175de8fe7984cb8db4d8aa188556832e9fcd9973b1a4ca065545ce1db9a2e3fc492466b931a58554b",
        ["acdcde92f2f2e43584ee59be447f778f4a152c308975c7bdc5c2372b5bbd3092eb9d2233aec3b82756ba1e352a0877ffc17e4c8cfb20a9de91ca6db54d79b591"
        ] = "5cc4e788f9fffefb4a92ada95c9d99011fb7d52eb213e6e33b10ff02c63b2cc59fb8d01bfe247e3067a5cadb69feda03da3dccb20f662a8afe24c53a9cbad891",
        ["7de161ea45faef7693d78ca44b585ab73fc183232d3f8d229fde3d05d696818d8d6402ac7ac86ce239a0a6cdae8fc2eafbb445e99443d0c7a4aab3781df35be6"
        ] = "90d3de8c60fa44e65115af322f627d4f31553bd18c474422fbf3d2768b56832cf25eb1c4854b19a049860bcf7ac58cb64b0fe5e115716124df0af955e3e9a9c9",
        ["00677819450d14d9adc2b65f25b9a069bc2b43f72e4db651e77fe0e48320be8eb7c555277281de968e75d0fb19bef960d4dcb27161b8c57bce076ee18bb5ca98"
        ] = "23407dd0327c863e49628fc4539e0cde775a9e76fb9c6a88d13de8e7b774115f3bd56a145937b4e3b4ff40e8b03422263f0eb2be8ad194410d652c5e44374cb4",
        ["f64e91e968858ec60219761635d2b4062926dc7050c206619a972244c5fb8ae016ec1b48e0a29e61b13e61ccaf7ef1ee27ef7ed4ba96b771a42d43f32b6965ae"
        ] = "91e68b16d7d285cf910e62cbe5978e767fe4933d420971c57c17805cf9a34ca1add43e9501024bfb40e80ea1dad34e2a6302e2c559d7a88995e633b4d9ea0bfa",
        ["ae1e0adddbd6afea3a0f6974433021e3b49e32ad1c56900b1cfde8f656fb645b3ab966cf42ce95b1829fac05e9556cfd4ad1cadb5dc5d1925c157b54fe8c5b95"
        ] = "ea394e7610eae30ad362804cee401a8c83e2ef54eab75799e972bb8b2ad66881493383c464f1e2dd88ff6787edc3237c565eb24e2f52043698a5f40c6a2c3691",
        ["baca2588b25cddaee87e45fd347c5e305dc68604045827213db18221e20f545d81824f411502ce29c93e59094e0f18d66eaa6212c4dd2a1b696ace543adcc0d2"
        ] = "07c0df0c86fa43d45267d6d6d7a82b24d7c2f951ffbb2f18e5b020d834c97017c80913590ce8116261d250041dd118a9cdef7e57de12ba792ad5b1ace5a60e0f",
        ["7b86ce31759717eab5cfc6ca36c57ed080e3f50817f8057f4d43afdd28519f0231445d89274095af5cc5d3ee26e9f2fcbc4187f749b46edc64c793a15cd6b26f"
        ] = "7a98ab98ae4587699c8104ea4148dc4f16ee0956c6f434d4e5a506409d0eb365c1f588abef36706014df6e9ee0d82667f4feb9af7a10c7c9ee6a66dae8194953",
        ["350e84f404d01cacf7660a1cfed6ac23b2bed26a9db4a378eac5007672ed5e73e0b92641cbb6b7944fe347300032eff388b25584f737586f44ab1dd3384a99bc"
        ] = "4a295c1dc18d0238a24e622360732e2466482d3c028b6fb7d488e22e164052deea3c7b9989f34efac4afea7985e8c563aeeea37834c768e946bc90482b1ba19f",
        ["420e07a6df3bdbf209551cf1e563a5e2c1112ec06752bcc4c254c51e4f23ad50924a20a09e4061aa7802cb1d0a0c516327ef9c87974cd7cc31f00da7190f9f56"
        ] = "db4ba997ec46cffc561a114e6d5b69944fcb0f427384a78474e9dba7c1793267cc8b3a63020e47d186f1bfce6e4d2b94f744da8b6d4b65d8028da5946ec18e44",
        ["19c25248a883cd30796791cad3e10f206ed90098971408a0a03924c4efa65d49f7f8d8ac316cd94c29c5478d126da059115dcdd973d16f9cf14404cc083a0e72"
        ] = "163dcaf6ee3f702ab12f3ef0430c48deb29b4d0b6e9b66b0f3265ceaf232495f58c53d3f2bd4d823e81de47b503e0d0721858d68a91c978dc6c6506cf0d86f0f",
        ["a47717e2655413c7db90a9e33894183222855078af277c06630bf6101ada2eac31a8d3aef7e9b5d1d75bf8a6548752f3c4532bbb84f49946899e8ae25949a753"
        ] = "dd3af0a25ba1eb6f5904c48a562b38a3651f776237cd4d0282784240a812d9acb98a7a457a8ef3e2a81aac0f4aad516d87bb129ee4332c4c0948bbcbf6c39a16",
        ["12ccc0cf1e05cdce5e052b0d60db61fe59bcdc7e07c15465c22caa7604e9b16b8e51f502abca0ca0bfcf000ac4d74e64aae5577685a238af87f12718ec75fca8"
        ] = "81b8c4a1e75ad5f258d57fd935c7030f6cfb4b89e2af6d77954c0763acf2e60bb80e696391c8eb368d4bdf92f5f6feda7b0997d621ca14c25dd1b3bb57edab60",
        ["209b3b15058ce908967d11a51cecb468d526d715e9863c07f12582138176739b0dc09bb8eb063c10f1161183ba003d6228aba6a231d2b2ce4fcc402cd4befe4e"
        ] = "910beb492e5b96a6f6f5d721ac2cea7149e2b6907bf6404996c8d1b7b76376de8190842e639137f70a21d204a8a7b21673f5d29398aa12daab293cdbfab49ae6",
        ["3d1869ada5816df75cd336798278609b08fcc878459ad6fb02f4eabe0c20329d8d7605672404ea77440b55855b4c2d03f12fb39e8d0ddc0d919d49c2fa113aaa"
        ] = "44ddcad7dc585fa7fd8a25738736e435166431ff4617a85c13c669e5f038af7acf9f69a5940713b8fefd6a90bd0586471e7e7d514bf6f256f835989bc31d2579",
        ["70a5ae766bff7030c87106cdad9de6474bb9965a6774629f16205a04db0140277995a6cbb2cf9d1bb27c5adfb9162758518c31a8b1f9c86163177acdcaeb3772"
        ] = "7870922335b0acc3ae518bf0e86c3e8eb5fcbd8f2642526445e93fca4aca9caeb1e2bc695c01b302003e6e4737dced52640b00d68d8a17a4ea262b8de2135a42",
        ["646fcaa105db75df2ac9dfe6c41018dc0ad317ff8f4cb419878435c79389b15549cf470c5d649e0bc45e3b42353991a598a23e0c3b41beab3ec92ebdb6afb4d6"
        ] = "a861eba61acb16a63e7f3f295edeb5b3a837134fae379830ed44b0567b5fbeba700d3a6dd5019eb8719e414cc5656c00670db062fc90534cb8677fc3819403d0",
        ["d12ec4de477cf8d067b54799d179692c2397e7c68847eaec7d7941ca946868089e7b576939a7108ac323c0056176b12c32dee532f2361eba2b1c43c8e50a2525"
        ] = "41d5eedfc6ec4feb62c977772405e3eed004b0af8d6ed5d37791f0cd27fbdacf245ea55af935894a80d5960a59211c58604c287c58fa99c8046f92bce4e076be",
        ["fada909f655fc412f08095f977ff3640ee54a799ce02eb34877b5325e1437ed0fb1f217730456be3cd23a662057ab086e15be269702396c9bbf83fe026969b05"
        ] = "3cc0395b8cf790539687f73ceb8ee539c89b8cb72252a63aef39ac52c3be52863ce6e1634008b714e40a462cb21fe34e38a23b16cdf48a97b2ac71502869e9ad",
        ["96d807e79baa7d69d9248736ea9a63ddcc53281a63765b47dcfd9ded24038cc445834647ef375ffad17ea83aee6f95d234109825f885353ccca73f6684a5acd0"
        ] = "eded3143a330d4c1ee394bb220801bcbf69fcfaedc1e2c12f056a6f3d4dd32eee4112c687d9901b1732ce9279e6f815c3c3497e74ecac717016b0722d5d0bb86",
        ["d00bcd9fc6b3c14f700007a2425d1ee4951a727769639b922c6bc993f43aef4ac7be0143f2262f2679f55de44c47b766ce158083ab989cae200fc5285d27df20"
        ] = "31378f15ce4400529730e7f147f95e0e6d3f0a32e810fa4578992ff25907089addda9bfd0ae2589f62bde47c9e4f8e836764d4bc4a934c470731f504ab395684",
        ["98dca56567b0c9a0874475818f6eadad90cf337eda3ba84e5bab222d58e4d1db7a9238c9aabe8e32016a15b1341576a369149e5394ff332c3014946e8687b8ca"
        ] = "12ad99f16d8e345b7b669edbd17c98835f6ae8779ee04ceadaeb734054175b146f3eb9693e9787536bf9961ab9715f074f93a379ea625b9da824f091d14ca3cb",
        ["09bd7620d335edac0c78a88d35dd55c53350df768debaad64ceb6c04d79cb056b8255b5a9b26bf13c6cdad26880905c0da64b7ec1582f525c5386a44d32e9dfd"
        ] = "c3a819f5242279b55421606a6a3af130098c60a2728324adbdacea39a0726f2c67f3035f4dcd8dcfdbba6a1c4d65591cd26b148a4c0f11773f635d8b44b7d3b8",
        ["1b17494985f080ae3fa79f8370a9cc3677a5b834a7ab62339b53dd9991158bc34fcfb7111c7dfa0d75e0da23cd469aca7f8d6e906a7c4cb33fc86e275c74187f"
        ] = "ec49e7bbfd1ecb2b7ca5e7e2e8ca8de82fb6fcd90bc21c54b34740c47411737a51d5e7e0632a2d4d4c8a18402b58cc2dc3190c4047d25cff2de432df72c1df29",
        ["f820b111842973dc7a8ac64e8e7f5c0b2406cabb4fa9a5ad89a521f1515c5cb64789eb17a113854bd574cbf9cbc064c78c7957564fa28c86f4a42be0286aa03e"
        ] = "72ecfd5754e29ddfd2af56d7444504c66de6a184307e5fccae3fa3b2022be7762178e6d206b9885171608423ccb5f8f225c51b3a0bedfbec37d49560ecf19d1f",
        ["233925dc1cbe4027688ed81654ebaa22698646e2847137fe7b34d4806ca40bad4cb654563e964ff1385d77d02233ecba868e083b5377505a0730db580fe06649"
        ] = "482259130ef82d2d5bce28d990961119731df624b800008d33012235ed4bc86bed24570a3b88b39905fc9b7fc82e91dfcbe950025c7bed9bc26f9e19f69a69cd",
        ["f4cfd514793d5602513948b331f4c0e1693cd4e3ed21388f613d7124a2831efb086201024f37f12b533517be0561c8120131dbfa8e87d94ff50d1d64703683ab"
        ] = "73a3f0f64ec2054bcee579126413c882b6d2905f21b2a7107c4450a8955d00a44cc9b4c9c7d5ba74168fb1e6272efd4cfc1179a66108eec311f056ae6b52d8b3",
        ["d813882a3144be33bbc5acfc2185d4ef033513e072dba918c2dc96395694b108b891a529ef8a203339e19f5da25fd747db65d2cec86d90c24a7e25e5c9b70ea2"
        ] = "e2a89e3d8ebdb20ecc8327a7c6fd387e4b91210ec05f09fe0e5f6b8265d3db7dc0e7bcd55e50216ae33acb7e90a04280c66ca104ebb185c0260cbf3bd7ae20a0",
        ["be7bf92984b7171b7f020fb2dfd3ba6ffbac0c226e28948af1ffeae5ed053957b07953b40db3e70152084eee605a9a3d184962a9617da5a5747544df58a8493a"
        ] = "692236152d521d45bc39737c474d4fa52ee3094828d56e5d06fa9eee944ecc6589cd913cabf9eeee7c38541dad0e3bdc62e74c8472690719c0b259fb566c02fe",
        ["be77d99cabc7206072776cd3de8add9f6ba0d4f6ee02346807516a7516b8ea18c5a1828f1872ab3fcdbadf7e4d7fe4bbb0652bef679a49cd0b6163569c038083"
        ] = "11b2b6e934c5db91ddc86d06bf197b40af2e1779157623bb134a93609c495f51857ba0068ded56c399d08fa5670773485f555d27e403fee63491d8acc91c436c",
        ["0130a658fc89cd35c0aa3fcba51f2be428585403c37c36b1600cf615a719c2ecbc606f56fd9b242d1e75f8a65b52894da85b5dcef0aaeacf75cbd9e19023e9a3"
        ] = "5a6f286d864b7a93c9f2e6d61db66c61d9fc686cfd17780d0093041d14b76072e7fce76eaf62358cab643e326426fc911729e66832b61d4c068ac9f42b6f14d5",
        ["b59425c30ae09bc3b2de92963ab246251b03aeb612da8b2c28a792ee7813b599db97f484878231bda221eb4f2480256a5bf7d95191a3180a75b7ca966971c0c9"
        ] = "b1c555eeed7a37415b44806ff1ef1f12b3e0154f28f41b4714bfda03d0dafb764dc7e574b0174e36ee04ceb0bd26a3e00bc4938822f09f3ff39f5f96dbb1beee",
        ["65badae3775a05291f267b668b1f62cddefe45a69e435c15dafa9a24a50e7c5ba1e83d1aa7724608adbc15a1099331e5a6bfaac01da769aaa475e07b1a505d55"
        ] = "17e878f02c5022066b2b0edbb23b34a256253eba939eb9c7a4d2434d05e089b5d9c1ef6faa18670dea1b014a9a798bf63e06c2fe6dbdf5dbf50a0b6837be5657",
        ["adfe0681f86ecfd586eedb54c39c1f1af02153e649787640bd97444e4f177c7e08e280448f38937e48a1eeade2063ec0d2c3eeb2d1d093ccd45b4e203ed33ad6"
        ] = "0dce469c5b6d4a83555e966556c5b8c12f6598f14a5221c5757f77e874f39834d7342fbb0ec928eb659ab8685b944b6845e17495df4a5a2eca83b9986f716c27",
        ["ff44290f288fdf951878a1625b23acbf60088496436fb16f98ab4e6abcd00dd6282343ccc917f0d02e1abe014106420083e00a20645da260ebc41d0e94efbfd7"
        ] = "9779e1c9faf707eb8b6d031a1f3733e90bab7436ca54ab2b866edebe6bd0d2e08c9956ec5810b0cd8875603c0aed5899d79cc1a5581cf8bb8a1a4fc61d7e78d8",
        ["aa7b52e90d6b7baed3400f4feb0d961ce1f0a2a60bf892d2a7311741fbd4e1f81b9526456555c866e73a2a1cd2f541cc84a5be6be8a8be5d0c937eb40d110265"
        ] = "bdd85ecc985edab8eccb4b5767c27b7b1d9f26f839f9007acf1c436d656eb7a6414da33acf03f46b08a619b0cccab899242230484ad5f6d837e42a23755bed06",
        ["ff573c192f196f641d6616addb7d629c22c6aa666b4abbf0c3bd5fa4d6758a74175dd08dec9edbc166a8cf90b1f9888ed791609f03972d12cce99bfaf058f1b6"
        ] = "2d5e22a35dcd1d8478815a3ca1dedf47cce881f40e78375f8a759e31f4a6527cc601acda1c8066e7c1441243561b6cbadd4a04d41c00bdf3c66e84789baf7e22",
        ["ec10d0f77b150f592ca6ed9a9fc182ff164f2f17c185d09a89d90ff1e8de0da1306f0d7952fd7337d41bad391ed25c07f2ee4e63aab62160849fbcdbafa5984c"
        ] = "0171d9f5049836cfdcd9414d1d834ecb4402ebc94b948e544a3ae7398e7feb90568684a1dcab2a4ff0cdcd74a3753bbe6fc34c0589cd8dbf659c3bf071f29100",
        ["321d3560e5cb484ca9e5730c4f5365ad5a294fb19f0a91ac5819c5734eca3ee74cf1a8cd2c61654f291505afbd265df6f17612a5ce2255fb431ee7173b81b538"
        ] = "73eabbb790967e961104eda0cba533d34c6f69770895db72c540384e2e90a70c253a777d6c4e7c2067baf6d7eee31f580992d888ab65affa83ffe93b24d4fdfe",
        ["f5d70b5d1c067d8463e83351d77b4102c80d2a7b0290daa9edf9b5d3c5efbaa7a9da7c06356991f25f0720d2adf9f6f4ce4b093e9a9657e9212399136e34ea82"
        ] = "0c5900bf14a911da5e1de3e8d19bb4508439fb08fea743b47ca7523a0586261fc253ff509c3280a0fb646c6aff20d3b981b842ae5a39bd0c5e2dce1eada4769e",
        ["ed92e4f2675ce12f4ffbc786a3d499aeb0369337ecb99234357ce1fa10562bd4df8a845aa78a331dabece8d7dd3e4f3cdbd5af24151a7a18772e1ea198c54b59"
        ] = "6d7cb711fea81091969bf2263cfcc9151331553a8a6f48bc9cc3d05bb9695a72e8a5693ed4ef02b991e864392b23c2692db9616de057a0f5391ed47cecba2b56",
    };

    /// <summary>
    /// Verifies that a downloaded file matches the expected hash.
    /// </summary>
    /// <param name="filePath">Path to the file to verify</param>
    /// <param name="expectedHash">Expected hash value</param>
    public static void VerifyFileHash(string filePath, string expectedHash)
    {
        if (string.IsNullOrEmpty(expectedHash))
        {
            throw new ArgumentException("Expected hash cannot be null or empty", nameof(expectedHash));
        }

        string actualHash = ComputeFileHash(filePath);
        if (string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Check if the actual hash is a known acceptable alternate for this expected hash
        if (s_knownAlternateHashes.TryGetValue(expectedHash, out var alternate) &&
            string.Equals(actualHash, alternate, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new DotnetInstallException(
                       DotnetInstallErrorCode.HashMismatch,
                       $"File hash mismatch. Expected: {expectedHash}, Actual: {actualHash}");
    }

    public void Dispose()
    {
        if (_shouldDisposeHttpClient)
        {
            _httpClient?.Dispose();
        }
    }
}
