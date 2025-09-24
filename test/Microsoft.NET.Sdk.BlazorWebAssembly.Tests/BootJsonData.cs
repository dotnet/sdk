// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResourceHashesByNameDictionary = System.Collections.Generic.Dictionary<string, string>;

// For test purposes only. Actual build time implementation lives in runtime repository with WasmSDK

namespace Microsoft.NET.Sdk.WebAssembly;

#pragma warning disable IDE1006 // Naming Styles
/// <summary>
/// Defines the structure of a Blazor boot JSON file
/// </summary>
public class BootJsonData
{
    /// <summary>
    /// Gets the name of the assembly with the application entry point
    /// </summary>
    /// <remarks>
    /// Deprecated in .NET 8. Use <see cref="mainAssemblyName"/>
    /// </remarks>
    public string entryAssembly
    {
        get { return mainAssemblyName; }
        set { mainAssemblyName = value; }
    }

    public string mainAssemblyName { get; set; }

    /// <summary>
    /// Gets the set of resources needed to boot the application. This includes the transitive
    /// closure of .NET assemblies (including the entrypoint assembly), the dotnet.wasm file,
    /// and any PDBs to be loaded.
    ///
    /// Within <see cref="ResourceHashesByNameDictionary"/>, dictionary keys are resource names,
    /// and values are SHA-256 hashes formatted in prefixed base-64 style (e.g., 'sha256-abcdefg...')
    /// as used for subresource integrity checking.
    /// </summary>
    [JsonIgnore]
    public ResourcesData resources => (ResourcesData)resourcesRaw;

    [JsonPropertyName("resources")]
    public object resourcesRaw { get; set; }

    /// <summary>
    /// Gets a value that determines whether to enable caching of the <see cref="resources"/>
    /// inside a CacheStorage instance within the browser.
    /// </summary>
    public bool? cacheBootResources { get; set; }

    /// <summary>
    /// Gets a value that determines if this is a debug build.
    /// </summary>
    public bool? debugBuild { get; set; }

    /// <summary>
    /// Gets a value that determines what level of debugging is configured.
    /// </summary>
    public int debugLevel { get; set; }

    /// <summary>
    /// Gets a value that determines if the linker is enabled.
    /// </summary>
    public bool? linkerEnabled { get; set; }

    /// <summary>
    /// Config files for the application
    /// </summary>
    /// <remarks>
    /// Deprecated in .NET 8, use <see cref="appsettings"/>
    /// </remarks>
    public List<string> config
    {
        get { return appsettings; }
        set { appsettings = value; }
    }

    /// <summary>
    /// Config files for the application
    /// </summary>
    public List<string> appsettings { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="ICUDataMode"/> that determines how icu files are loaded.
    /// </summary>
    /// <remarks>
    /// Deprecated since .NET 8. Use <see cref="globalizationMode"/> instead.
    /// </remarks>
    public GlobalizationMode? icuDataMode
    {
        get { return Enum.Parse<GlobalizationMode>(globalizationMode); }
        set { globalizationMode = value.ToString().ToLowerInvariant(); }
    }

    /// <summary>
    /// Gets or sets the <see cref="GlobalizationMode"/> that determines how icu files are loaded.
    /// </summary>
    public string globalizationMode { get; set; }

    /// <summary>
    /// Gets or sets a value that determines if the caching startup memory is enabled.
    /// </summary>
    public bool? startupMemoryCache { get; set; }

    /// <summary>
    /// Gets a value for mono runtime options.
    /// </summary>
    public string[] runtimeOptions { get; set; }

    /// <summary>
    /// Gets or sets configuration extensions.
    /// </summary>
    public Dictionary<string, Dictionary<string, object>> extensions { get; set; }

    /// <summary>
    /// Gets or sets environment variables.
    /// </summary>
    public object environmentVariables { get; set; }

    /// <summary>
    /// Gets or sets diagnostic tracing.
    /// </summary>
    public object diagnosticTracing { get; set; }

    /// <summary>
    /// Gets or sets pthread pool size.
    /// </summary>
    public int? pthreadPoolSize { get; set; }
}

public class ResourcesData
{
    /// <summary>
    /// Gets a hash of all resources
    /// </summary>
    public string hash { get; set; }

    /// <summary>
    /// .NET Wasm runtime resources (dotnet.wasm, dotnet.js) etc.
    /// </summary>
    /// <remarks>
    /// Deprecated in .NET 8, use <see cref="jsModuleWorker"/>, <see cref="jsModuleNative"/>, <see cref="jsModuleRuntime"/>, <see cref="wasmNative"/>, <see cref="jsSymbols"/>, <see cref="icu"/>.
    /// </remarks>
    [DataMember(EmitDefaultValue = false)]
    public ResourceHashesByNameDictionary runtime { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public ResourceHashesByNameDictionary jsModuleWorker { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public ResourceHashesByNameDictionary jsModuleDiagnostics { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public ResourceHashesByNameDictionary jsModuleNative { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public ResourceHashesByNameDictionary jsModuleRuntime { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public ResourceHashesByNameDictionary wasmNative { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public ResourceHashesByNameDictionary wasmSymbols { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public ResourceHashesByNameDictionary icu { get; set; }

    public ResourceHashesByNameDictionary coreAssembly { get; set; } = new ResourceHashesByNameDictionary();

    /// <summary>
    /// "assembly" (.dll) resources
    /// </summary>
    public ResourceHashesByNameDictionary assembly { get; set; } = new ResourceHashesByNameDictionary();

    [DataMember(EmitDefaultValue = false)]
    public ResourceHashesByNameDictionary corePdb { get; set; }

    /// <summary>
    /// "debug" (.pdb) resources
    /// </summary>
    [DataMember(EmitDefaultValue = false)]
    public ResourceHashesByNameDictionary pdb { get; set; }

    /// <summary>
    /// localization (.satellite resx) resources
    /// </summary>
    [DataMember(EmitDefaultValue = false)]
    public Dictionary<string, ResourceHashesByNameDictionary> satelliteResources { get; set; }

    /// <summary>
    /// Assembly (.dll) resources that are loaded lazily during runtime
    /// </summary>
    [DataMember(EmitDefaultValue = false)]
    public ResourceHashesByNameDictionary lazyAssembly { get; set; }

    /// <summary>
    /// JavaScript module initializers that Blazor will be in charge of loading.
    /// Used in .NET < 8
    /// </summary>
    [DataMember(EmitDefaultValue = false)]
    public ResourceHashesByNameDictionary libraryInitializers { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public ResourceHashesByNameDictionary modulesAfterConfigLoaded { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public ResourceHashesByNameDictionary modulesAfterRuntimeReady { get; set; }

    /// <summary>
    /// Extensions created by users customizing the initialization process. The format of the file(s)
    /// is up to the user.
    /// </summary>
    [DataMember(EmitDefaultValue = false)]
    public Dictionary<string, ResourceHashesByNameDictionary> extensions { get; set; }

    /// <summary>
    /// Additional assets that the runtime consumes as part of the boot process.
    /// </summary>
    [DataMember(EmitDefaultValue = false)]
    public Dictionary<string, AdditionalAsset> runtimeAssets { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public Dictionary<string, ResourceHashesByNameDictionary> coreVfs { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public Dictionary<string, ResourceHashesByNameDictionary> vfs { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public List<string> remoteSources { get; set; }
}

public class AssetsData
{
    /// <summary>
    /// Gets a hash of all resources
    /// </summary>
    public string hash { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public List<JsAsset> jsModuleWorker { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public List<JsAsset> jsModuleDiagnostics { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public List<JsAsset> jsModuleNative { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public List<JsAsset> jsModuleRuntime { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public List<WasmAsset> wasmNative { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public List<SymbolsAsset> wasmSymbols { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public List<GeneralAsset> icu { get; set; }

    /// <summary>
    /// "assembly" (.dll) resources needed to start MonoVM
    /// </summary>
    public List<GeneralAsset> coreAssembly { get; set; } = new();

    /// <summary>
    /// "assembly" (.dll) resources
    /// </summary>
    public List<GeneralAsset> assembly { get; set; } = new();

    /// <summary>
    /// "debug" (.pdb) resources needed to start MonoVM
    /// </summary>
    [DataMember(EmitDefaultValue = false)]
    public List<GeneralAsset> corePdb { get; set; }

    /// <summary>
    /// "debug" (.pdb) resources
    /// </summary>
    [DataMember(EmitDefaultValue = false)]
    public List<GeneralAsset> pdb { get; set; }

    /// <summary>
    /// localization (.satellite resx) resources
    /// </summary>
    [DataMember(EmitDefaultValue = false)]
    public Dictionary<string, List<GeneralAsset>> satelliteResources { get; set; }

    /// <summary>
    /// Assembly (.dll) resources that are loaded lazily during runtime
    /// </summary>
    [DataMember(EmitDefaultValue = false)]
    public List<GeneralAsset> lazyAssembly { get; set; }

    /// <summary>
    /// JavaScript module initializers that Blazor will be in charge of loading.
    /// Used in .NET < 8
    /// </summary>
    [DataMember(EmitDefaultValue = false)]
    public List<JsAsset> libraryInitializers { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public List<JsAsset> modulesAfterConfigLoaded { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public List<JsAsset> modulesAfterRuntimeReady { get; set; }

    /// <summary>
    /// Extensions created by users customizing the initialization process. The format of the file(s)
    /// is up to the user.
    /// </summary>
    [DataMember(EmitDefaultValue = false)]
    public Dictionary<string, ResourceHashesByNameDictionary> extensions { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public List<VfsAsset> coreVfs { get; set; }

    [DataMember(EmitDefaultValue = false)]
    public List<VfsAsset> vfs { get; set; }
}

[DataContract]
public class JsAsset
{
    public string name { get; set; }
    public string moduleExports { get; set; }
}

[DataContract]
public class SymbolsAsset
{
    public string name { get; set; }
}

[DataContract]
public class WasmAsset
{
    public string name { get; set; }
    public string integrity { get; set; }
    public string resolvedUrl { get; set; }
}

[DataContract]
public class GeneralAsset
{
    public string virtualPath { get; set; }
    public string name { get; set; }
    public string integrity { get; set; }
    public string resolvedUrl { get; set; }
}

[DataContract]
public class VfsAsset
{
    public string virtualPath { get; set; }
    public string name { get; set; }
    public string integrity { get; set; }
    public string resolvedUrl { get; set; }
}

public enum GlobalizationMode : int
{
    // Note that the numeric values are serialized and used in JS code, so don't change them without also updating the JS code
    // Note that names are serialized as string and used in JS code

    /// <summary>
    /// Load optimized icu data file based on the user's locale
    /// </summary>
    Sharded = 0,

    /// <summary>
    /// Use the combined icudt.dat file
    /// </summary>
    All = 1,

    /// <summary>
    /// Do not load any icu data files.
    /// </summary>
    Invariant = 2,

    /// <summary>
    /// Load custom icu file provided by the developer.
    /// </summary>
    Custom = 3,
}

[DataContract]
public class AdditionalAsset
{
    [DataMember(Name = "hash")]
    public string hash { get; set; }

    [DataMember(Name = "behavior")]
    public string behavior { get; set; }
}
#pragma warning restore IDE1006 // Naming Styles

public class BootJsonDataLoader
{
    public static BootJsonData ParseBootData(string bootConfigPath)
    {
        string jsonContent = GetJsonContent(bootConfigPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new ResourcesConverter());

        BootJsonData config = JsonSerializer.Deserialize<BootJsonData>(jsonContent, options);
        if (config.resourcesRaw is AssetsData assets)
        {
            config.resourcesRaw = ConvertAssetsToResources(assets);
        }

        return config;
    }

    private static ResourcesData ConvertAssetsToResources(AssetsData assets)
    {
        static Dictionary<string, ResourceHashesByNameDictionary> ConvertSatelliteResources(Dictionary<string, List<GeneralAsset>> satelliteResources)
        {
            if (satelliteResources == null)
                return null;

            var result = new Dictionary<string, ResourceHashesByNameDictionary>();
            foreach (var kvp in satelliteResources)
                result[kvp.Key] = kvp.Value.ToDictionary(a => a.name, a => a.integrity);

            return result;
        }

        static Dictionary<string, ResourceHashesByNameDictionary> ConvertVfsAssets(List<VfsAsset> vfsAssets)
        {
            return vfsAssets?.ToDictionary(a => a.virtualPath, a => new ResourceHashesByNameDictionary
            {
                { a.name, a.integrity }
            });
        }

        var resources = new ResourcesData
        {
            hash = assets.hash,
            jsModuleWorker = assets.jsModuleWorker?.ToDictionary(a => a.name, a => (string)null),
            jsModuleDiagnostics = assets.jsModuleDiagnostics?.ToDictionary(a => a.name, a => (string)null),
            jsModuleNative = assets.jsModuleNative?.ToDictionary(a => a.name, a => (string)null),
            jsModuleRuntime = assets.jsModuleRuntime?.ToDictionary(a => a.name, a => (string)null),
            wasmNative = assets.wasmNative?.ToDictionary(a => a.name, a => a.integrity),
            wasmSymbols = assets.wasmSymbols?.ToDictionary(a => a.name, a => (string)null),
            icu = assets.icu?.ToDictionary(a => a.name, a => a.integrity),
            coreAssembly = assets.coreAssembly?.ToDictionary(a => a.name, a => a.integrity),
            assembly = assets.assembly?.ToDictionary(a => a.name, a => a.integrity),
            corePdb = assets.corePdb?.ToDictionary(a => a.name, a => a.integrity),
            pdb = assets.pdb?.ToDictionary(a => a.name, a => a.integrity),
            satelliteResources = ConvertSatelliteResources(assets.satelliteResources),
            lazyAssembly = assets.lazyAssembly?.ToDictionary(a => a.name, a => a.integrity),
            libraryInitializers = assets.libraryInitializers?.ToDictionary(a => a.name, a => (string)null),
            modulesAfterConfigLoaded = assets.modulesAfterConfigLoaded?.ToDictionary(a => a.name, a => (string)null),
            modulesAfterRuntimeReady = assets.modulesAfterRuntimeReady?.ToDictionary(a => a.name, a => (string)null),
            extensions = assets.extensions,
            coreVfs = ConvertVfsAssets(assets.coreVfs),
            vfs = ConvertVfsAssets(assets.vfs)
        };
        return resources;
    }

    public static string GetJsonContent(string bootConfigPath)
    {
        string startComment = "/*json-start*/";
        string endComment = "/*json-end*/";

        string moduleContent = File.ReadAllText(bootConfigPath);
        int startCommentIndex = moduleContent.IndexOf(startComment);
        int endCommentIndex = moduleContent.IndexOf(endComment);
        if (startCommentIndex >= 0 && endCommentIndex >= 0)
        {
            // boot.js
            int startJsonIndex = startCommentIndex + startComment.Length;
            string jsonContent = moduleContent.Substring(startJsonIndex, endCommentIndex - startJsonIndex);
            return jsonContent;
        }

        return moduleContent;
    }
}

internal class ResourcesConverter : JsonConverter<object>
{
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var nestedOptions = new JsonSerializerOptions(options);
        nestedOptions.Converters.Remove(this);

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            try
            {

                return JsonSerializer.Deserialize<AssetsData>(ref reader, nestedOptions)!;
            }
            catch
            {
                return JsonSerializer.Deserialize<ResourcesData>(ref reader, nestedOptions)!;
            }
        }

        return JsonSerializer.Deserialize<object>(ref reader, nestedOptions)!;
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
