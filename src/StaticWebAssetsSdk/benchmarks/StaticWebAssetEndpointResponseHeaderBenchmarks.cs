// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;

namespace Microsoft.AspNetCore.StaticWebAssets.Benchmarks;

[MemoryDiagnoser]
public class StaticWebAssetEndpointResponseHeaderBenchmarks
{
    private const string TestValue = """[{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Cache-Control","Value":"no-cache"},{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Content-Length","Value":"__content-length__"},{"Name":"Content-Type","Value":"text/javascript"},{"Name":"ETag","Value":"__etag__"},{"Name":"Last-Modified","Value":"__last-modified__"},{"Name":"Vary","Value":"Content-Encoding"}]""";

    private readonly List<StaticWebAssetEndpointResponseHeader> _headers = [];
    private readonly StaticWebAssetEndpointResponseHeader[] _headersArray;
    private readonly List<StaticWebAssetEndpointResponseHeader> _headersList;
    private readonly JsonWriterContext _context;

    public StaticWebAssetEndpointResponseHeaderBenchmarks()
    {
        // Initialize test data for ToMetadataValue benchmarks
        _headersArray = StaticWebAssetEndpointResponseHeader.FromMetadataValue(TestValue);
        _headersList = new List<StaticWebAssetEndpointResponseHeader>(_headersArray);
        _context = StaticWebAssetEndpointResponseHeader.CreateWriter();
        _context.Reset();
    }

    [Benchmark]
    public StaticWebAssetEndpointResponseHeader[] FromMetadataValue_Current()
    {
        return StaticWebAssetEndpointResponseHeader.FromMetadataValue(TestValue);
    }

    [Benchmark]
    public List<StaticWebAssetEndpointResponseHeader> PopulateFromMetadataValue_New()
    {
        StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue(TestValue, _headers);
        _headers.Clear();
        return _headers;
    }

    [Benchmark]
    public string ToMetadataValue_Current()
    {
        return StaticWebAssetEndpointResponseHeader.ToMetadataValue(_headersArray);
    }

    [Benchmark]
    public string ToMetadataValue_New()
    {
        return StaticWebAssetEndpointResponseHeader.ToMetadataValue(_headersList, _context);
    }
}
