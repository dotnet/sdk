// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;

namespace Microsoft.AspNetCore.StaticWebAssets.Benchmarks;

[MemoryDiagnoser]
public class StaticWebAssetEndpointSelectorBenchmarks
{
    private const string TestValue = """[{"Name":"Content-Encoding","Value":"gzip","Quality":"0.100000000000"},{"Name":"Content-Encoding","Value":"br","Quality":"0.5"}]""";

    private readonly List<StaticWebAssetEndpointSelector> _selectors = [];
    private readonly StaticWebAssetEndpointSelector[] _selectorsArray;
    private readonly List<StaticWebAssetEndpointSelector> _selectorsList;
    private readonly JsonWriterContext _context;

    public StaticWebAssetEndpointSelectorBenchmarks()
    {
        // Initialize test data for ToMetadataValue benchmarks
        _selectorsArray = StaticWebAssetEndpointSelector.FromMetadataValue(TestValue);
        _selectorsList = new List<StaticWebAssetEndpointSelector>(_selectorsArray);
        _context = StaticWebAssetEndpointSelector.CreateWriter();
        _context.Reset();
    }

    [Benchmark]
    public StaticWebAssetEndpointSelector[] FromMetadataValue_Current()
    {
        return StaticWebAssetEndpointSelector.FromMetadataValue(TestValue);
    }

    [Benchmark]
    public List<StaticWebAssetEndpointSelector> PopulateFromMetadataValue_New()
    {
        StaticWebAssetEndpointSelector.PopulateFromMetadataValue(TestValue, _selectors);
        _selectors.Clear();
        return _selectors;
    }

    [Benchmark]
    public string ToMetadataValue_Current()
    {
        return StaticWebAssetEndpointSelector.ToMetadataValue(_selectorsArray);
    }

    [Benchmark]
    public string ToMetadataValue_New()
    {
        return StaticWebAssetEndpointSelector.ToMetadataValue(_selectorsList, _context);
    }
}
