// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;
using System.Text.Json;
using DotNetWatchTasks;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.NET.Build.Tasks.UnitTests;

namespace Microsoft.DotNet.Watcher.Tools;

public class FileSetSerializerTests(ITestOutputHelper output)
{
    private readonly TestAssetsManager _testAssetManager = new (output);

    private static string Serialize(MSBuildFileSetResult fileSetResult, Stream stream)
    {
        foreach (var item in fileSetResult.Projects.Values)
        {
            item.PrepareForSerialization();
        }

        var serializer = new DataContractJsonSerializer(fileSetResult.GetType(), new DataContractJsonSerializerSettings
        {
            UseSimpleDictionaryFormat = true,
        });

        using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, ownsStream: false, indent: true))
        {
            serializer.WriteObject(writer, fileSetResult);
        }

        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task Roundtrip()
    {
        var result1 = new MSBuildFileSetResult()
        {
            Projects = new()
            {
                {
                    "A.csproj", new ProjectItems()
                    {
                        FileSetBuilder = ["a.cs", "b.cs" ],
                        StaticFileSetBuilder = new() { { "path1", "path2" } }
                    }
                },
                {
                    "B.csproj", new ProjectItems()
                    {
                        FileSetBuilder = ["a.cs", "c.cs"],
                        StaticFileSetBuilder = new() { { "path2", "path3" } }
                    }
                }
            }
        };

        using var stream1 = new MemoryStream();
        var serialized1 = Serialize(result1, stream1);
        stream1.Position = 0;
        var result2 = await JsonSerializer.DeserializeAsync<MSBuildFileSetResult>(stream1, cancellationToken: CancellationToken.None);

        using var stream2 = new MemoryStream();
        var serialized2 = Serialize(result2, stream2);
        AssertEx.Equal(serialized1, serialized2);

        AssertEx.Equal("""
            {
              "Projects": {
                "A.csproj": {
                  "Files": [
                    "a.cs",
                    "b.cs"
                  ],
                  "StaticFiles": [
                    {
                      "FilePath": "path1",
                      "StaticWebAssetPath": "path2"
                    }
                  ]
                },
                "B.csproj": {
                  "Files": [
                    "a.cs",
                    "c.cs"
                  ],
                  "StaticFiles": [
                    {
                      "FilePath": "path2",
                      "StaticWebAssetPath": "path3"
                    }
                  ]
                }
              }
            }
            """.Replace("\r\n", "\n"), serialized1.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task Task()
    {
        var dir = _testAssetManager.CreateTestDirectory().Path;
        var outputPath = Path.Combine(dir, "output.txt");

        var engine = new MockBuildEngine();

        var task = new FileSetSerializer()
        {
            BuildEngine = engine,
            OutputPath = outputPath,
            WatchFiles =
            [
                new MockTaskItem("file1.cs", new()
                {
                    { "FullPath", "file1.cs" },
                    { "ProjectFullPath", "ProjectA.csproj" },
                }),
                new MockTaskItem("file1.cs", new()
                {
                    { "FullPath", "file1.cs" },
                    { "ProjectFullPath", "ProjectA.csproj" },
                }),
                new MockTaskItem("ProjectA.csproj", new()
                {
                    { "FullPath", "ProjectA.csproj" },
                    { "ProjectFullPath", "ProjectA.csproj" },
                }),
                new MockTaskItem("ProjectA.csproj", new()
                {
                    { "FullPath", "ProjectA.csproj" },
                    { "ProjectFullPath", "ProjectA.csproj" },
                }),
                new MockTaskItem("ProjectB.csproj", new()
                {
                    { "FullPath", "ProjectB.csproj" },
                    { "ProjectFullPath", "ProjectB.csproj" },
                }),
                new MockTaskItem("file.css", new()
                {
                    { "FullPath", "file.css" },
                    { "ProjectFullPath", "ProjectB.csproj" },
                    { "StaticWebAssetPath", "/wwwroot/a/b/file.css" }
                })
            ]
        };

        var result = task.Execute();
        Assert.True(result);

        AssertEx.Equal("""
            {
              "Projects": {
                "ProjectA.csproj": {
                  "Files": [
                    "file1.cs",
                    "ProjectA.csproj"
                  ],
                  "StaticFiles": [ ]
                },
                "ProjectB.csproj": {
                  "Files": [
                    "ProjectB.csproj"
                  ],
                  "StaticFiles": [
                    {
                      "FilePath": "file.css",
                      "StaticWebAssetPath": "\/wwwroot\/a\/b\/file.css"
                    }
                  ]
                }
              }
            }
            """.Replace("\r\n", "\n"), File.ReadAllText(outputPath, Encoding.UTF8).Replace("\r\n", "\n"));

        using var stream = File.OpenRead(outputPath);
        var value = await JsonSerializer.DeserializeAsync<MSBuildFileSetResult>(stream, cancellationToken: CancellationToken.None);
    }
}
