// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.commands.package.search;

namespace Microsoft.DotNet.Cli.Package.Tests
{
    public class NuGetSearchApiRequestTests
    {
        [Fact]
        public async Task NuGetSearchApiRequest_WithMockServerPackageExists_OutputOnePackage()
        {
            // Arrange: Set up the mock server with a specific response
            var mockServer = new MockServer();
            var indexObject = new MockIndex
            {
                Version = "3.0.0",
                Resources = new[]
                {
                    new Resource
                    {
                        @Id = "http://localhost:" + mockServer.Port +"/search/query",
                        @Type = "SearchQueryService/Versioned",
                        Comment = "Query endpoint of NuGet Search service (primary)"
                    }
                },
                @Context = new Context
                {
                    @Vocab = "http://schema.nuget.org/services#",
                    Comment = "http://www.w3.org/2000/01/rdf-schema#comment"
                }
            };

            mockServer.AddMockResponse("/v3/index.json", indexObject);
            var resultObject = new QueryResult
            {
                Context = new Context
                {
                    Vocab = "http://schema.nuget.org/schema#",
                    Base = "https://api.nuget.org/v3/registration5-semver1/"
                },
                TotalHits = 396,
                Data = new List<DataItem>
                {
                    new DataItem
                    {
                        Id = "https://api.nuget.org/v3/registration5-semver1/newtonsoft.json/index.json",
                        Type = "Package",
                        Registration = "https://api.nuget.org/v3/registration5-semver1/newtonsoft.json/index.json",
                        PackageId = "Fake.Newtonsoft.Json",
                        Version = "12.0.3",
                        Description = "Json.NET is a popular high-performance JSON framework for .NET",
                        Summary = "",
                        Title = "Json.NET",
                        IconUrl = "https://api.nuget.org/v3-flatcontainer/newtonsoft.json/12.0.3/icon",
                        LicenseUrl = "https://www.nuget.org/packages/Newtonsoft.Json/12.0.3/license",
                        ProjectUrl = "https://www.newtonsoft.com/json",
                        Tags = new List<string>
                        {
                            "json"
                        },
                        Authors = new List<string>
                        {
                            "James Newton-King"
                        },
                        TotalDownloads = 531607259,
                        Verified = true,
                        PackageTypes = new List<PackageType>
                        {
                            new PackageType { Name = "Dependency" }
                        },
                        Versions = new List<VersionItem>
                        {
                            new VersionItem
                            {
                                Version = "3.5.8",
                                Downloads = 461992,
                                Id = "https://api.nuget.org/v3/registration5-semver1/newtonsoft.json/3.5.8.json"
                            }
                        }
                    }
                }
            };

            string expectedResult = "====================\r\nSource: http://localhost:" + mockServer.Port + "/v3/index.json\r\nFake.Newtonsoft.Json | 12.0.3 | Downloads: 531607259\r\n--------------------\r\n";
            mockServer.AddMockResponse("/search/query?q=json&skip=0&take=20&prerelease=false&semVerLevel=2.0.0", resultObject);
            mockServer.Start();
            var request = new NuGetSearchApiRequest("json", null, null, false, false, new List<string> { "http://localhost:" + mockServer.Port + "/v3/index.json" });

            // Redirect console output
            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            // Act
            await request.ExecuteCommandAsync();

            // Assert
            Assert.Equal(expectedResult, consoleOutput.ToString());

            //stop mock server
            mockServer.Stop();
        }
    }
    
}



