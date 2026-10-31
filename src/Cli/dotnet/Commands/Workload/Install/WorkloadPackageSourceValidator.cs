// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

/// <summary>
///  Validates that the package sources passed to a workload command via <c>--source</c> are NuGet feeds
///  before any manifest or pack is acquired, so that a wrong URL produces a single clear error instead of
///  a series of confusing "not found" failures for every workload manifest.
/// </summary>
internal static class WorkloadPackageSourceValidator
{
    public static async Task ValidateAsync(IEnumerable<string>? sources, CancellationToken cancellationToken = default)
    {
        if (sources is null)
        {
            return;
        }

        foreach (var source in sources.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var packageSource = new PackageSource(source);

            //  Local folder feeds have no protocol to check, and invalid URIs are already reported
            //  (and skipped) when the NuGet package downloader loads the sources.
            if (!packageSource.IsHttp)
            {
                continue;
            }

            bool isNuGetFeed;
            try
            {
                isNuGetFeed = await IsNuGetFeedAsync(packageSource, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is FatalProtocolException or HttpRequestException)
            {
                throw new GracefulException(string.Format(CliCommandStrings.WorkloadPackageSourceCouldNotBeLoaded, source, e.Message), e, isUserError: true);
            }

            if (!isNuGetFeed)
            {
                throw new GracefulException(string.Format(CliCommandStrings.WorkloadPackageSourceIsNotANuGetFeed, source), isUserError: true);
            }
        }
    }

    private static async Task<bool> IsNuGetFeedAsync(PackageSource packageSource, CancellationToken cancellationToken)
    {
        var repository = Repository.Factory.GetCoreV3(packageSource);

        if (FeedTypeUtility.GetFeedType(packageSource) == FeedType.HttpV3)
        {
            //  NuGet only creates this resource when the URL returns a valid service index,
            //  and throws a FatalProtocolException otherwise.
            return await repository.GetResourceAsync<ServiceIndexResourceV3>(cancellationToken).ConfigureAwait(false) is not null;
        }

        //  For V2 sources NuGet only checks that the URL returns a success status code, so any web page
        //  would be accepted. Check that the response is actually an OData service document instead.
        var httpSource = (await repository.GetResourceAsync<HttpSourceResource>(cancellationToken).ConfigureAwait(false))!.HttpSource;
        return await httpSource.ProcessStreamAsync(
            new HttpSourceRequest(packageSource.Source, NullLogger.Instance),
            stream => Task.FromResult(IsODataServiceDocument(stream)),
            NullLogger.Instance,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///  Returns whether the stream contains an AtomPub/OData service document, which is what the root of a
    ///  NuGet V2 feed returns (for example, <c>&lt;service xmlns="http://www.w3.org/2007/app"&gt;</c>).
    /// </summary>
    internal static bool IsODataServiceDocument(Stream? stream)
    {
        if (stream is null)
        {
            return false;
        }

        try
        {
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return reader.MoveToContent() == XmlNodeType.Element &&
                reader.LocalName.Equals("service", StringComparison.Ordinal);
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
