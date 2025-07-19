// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;

namespace Microsoft.AspNetCore.StaticWebAssets.Benchmarks;

[MemoryDiagnoser]
public class StaticWebAssetEndpointPropertyBenchmarks
{
    private const string TestValue = """[{"Name":"label","Value":"resource.ext"},{"Name":"integrity","Value":"sha256-abcdef1234567890abcdef1234567890abcdef12"}]""";

    private readonly List<StaticWebAssetEndpointProperty> _properties = [];
    private readonly StaticWebAssetEndpointProperty[] _propertiesArray;
    private readonly List<StaticWebAssetEndpointProperty> _propertiesList;
    private readonly JsonWriterContext _context;

    public StaticWebAssetEndpointPropertyBenchmarks()
    {
        // Initialize test data for ToMetadataValue benchmarks
        _propertiesArray = StaticWebAssetEndpointProperty.FromMetadataValue(TestValue);
        _propertiesList = new List<StaticWebAssetEndpointProperty>(_propertiesArray);
        _context = StaticWebAssetEndpointProperty.CreateWriter();
        _context.Reset();
    }

    [Benchmark]
    public StaticWebAssetEndpointProperty[] FromMetadataValue_Current()
    {
        return StaticWebAssetEndpointProperty.FromMetadataValue(TestValue);
    }

    [Benchmark]
    public List<StaticWebAssetEndpointProperty> PopulateFromMetadataValue_New()
    {
        StaticWebAssetEndpointProperty.PopulateFromMetadataValue(TestValue, _properties);
        _properties.Clear();
        return _properties;
    }

    [Benchmark]
    public string ToMetadataValue_Current()
    {
        return StaticWebAssetEndpointProperty.ToMetadataValue(_propertiesArray);
    }

    [Benchmark]
    public string ToMetadataValue_New()
    {
        return StaticWebAssetEndpointProperty.ToMetadataValue(_propertiesList, _context);
    }
}
