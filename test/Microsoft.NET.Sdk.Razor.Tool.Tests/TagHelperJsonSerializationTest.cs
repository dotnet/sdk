// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.NET.Sdk.Razor.Tool.Json;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    /// <summary>
    /// Tests that validate JSON serialization round-tripping for TagHelperDescriptor,
    /// which is the core data structure used by DiscoverCommand (serialization) and
    /// GenerateCommand (deserialization).
    /// </summary>
    /// <remarks>
    /// These tests validate that the JSON serialization format produced by DiscoverCommand
    /// can be correctly deserialized by GenerateCommand. The tests use predefined JSON
    /// strings that represent various TagHelperDescriptor configurations.
    /// </remarks>
    public class TagHelperJsonSerializationTest
    {
        [Fact]
        public void RoundTrip_SimpleTagHelper_PreservesData()
        {
            // Arrange - JSON representing a simple tag helper
            var json = """
                [
                    {
                        "Flags": 1,
                        "Name": "TestTagHelper",
                        "AssemblyName": "TestAssembly",
                        "DisplayName": "Test Tag Helper",
                        "TypeName": "TestNamespace.TestTagHelper",
                        "TagMatchingRules": [
                            {
                                "TagName": "test-tag"
                            }
                        ]
                    }
                ]
                """;

            // Act - Deserialize then reserialize
            var deserialized = Deserialize(json);
            var reserialized = Serialize(deserialized);
            var roundTripped = Deserialize(reserialized);

            // Assert
            Assert.Single(roundTripped);
            Assert.Equal("TestTagHelper", roundTripped[0].Name);
            Assert.Equal("TestAssembly", roundTripped[0].AssemblyName);
            Assert.Equal("Test Tag Helper", roundTripped[0].DisplayName);
            Assert.Equal("TestNamespace.TestTagHelper", roundTripped[0].TypeName);
            Assert.Single(roundTripped[0].TagMatchingRules);
            Assert.Equal("test-tag", roundTripped[0].TagMatchingRules[0].TagName);
        }

        [Fact]
        public void RoundTrip_TagHelperWithBoundAttributes_PreservesData()
        {
            // Arrange
            var json = """
                [
                    {
                        "Flags": 1,
                        "Name": "BoundTagHelper",
                        "AssemblyName": "TestAssembly",
                        "TypeName": "TestNamespace.BoundTagHelper",
                        "TagMatchingRules": [
                            {
                                "TagName": "bound-tag"
                            }
                        ],
                        "BoundAttributes": [
                            {
                                "Flags": 0,
                                "Name": "value",
                                "PropertyName": "Value",
                                "TypeName": "System.String",
                                "DisplayName": "Value Attribute"
                            },
                            {
                                "Flags": 0,
                                "Name": "count",
                                "PropertyName": "Count",
                                "TypeName": "System.Int32",
                                "DisplayName": "Count Attribute"
                            }
                        ]
                    }
                ]
                """;

            // Act
            var deserialized = Deserialize(json);
            var reserialized = Serialize(deserialized);
            var roundTripped = Deserialize(reserialized);

            // Assert
            Assert.Single(roundTripped);
            var result = roundTripped[0];

            Assert.Equal("BoundTagHelper", result.Name);
            Assert.Equal(2, result.BoundAttributes.Length);

            Assert.Equal("value", result.BoundAttributes[0].Name);
            Assert.Equal("Value", result.BoundAttributes[0].PropertyName);
            Assert.Equal("System.String", result.BoundAttributes[0].TypeName);

            Assert.Equal("count", result.BoundAttributes[1].Name);
            Assert.Equal("Count", result.BoundAttributes[1].PropertyName);
            Assert.Equal("System.Int32", result.BoundAttributes[1].TypeName);
        }

        [Fact]
        public void RoundTrip_TagHelperWithTagMatchingRules_PreservesData()
        {
            // Arrange
            // TagStructure enum: Unspecified=0, NormalOrSelfClosing=1, WithoutEndTag=2
            var json = """
                [
                    {
                        "Flags": 1,
                        "Name": "MultiRuleTagHelper",
                        "AssemblyName": "TestAssembly",
                        "TypeName": "TestNamespace.MultiRuleTagHelper",
                        "TagMatchingRules": [
                            {
                                "TagName": "my-tag",
                                "ParentTag": "parent-tag",
                                "TagStructure": 2,
                                "CaseSensitive": false
                            },
                            {
                                "TagName": "alternate-tag",
                                "TagStructure": 1
                            }
                        ]
                    }
                ]
                """;

            // Act
            var deserialized = Deserialize(json);
            var reserialized = Serialize(deserialized);
            var roundTripped = Deserialize(reserialized);

            // Assert
            Assert.Single(roundTripped);
            var result = roundTripped[0];

            Assert.Equal(2, result.TagMatchingRules.Length);

            Assert.Equal("my-tag", result.TagMatchingRules[0].TagName);
            Assert.Equal("parent-tag", result.TagMatchingRules[0].ParentTag);
            Assert.Equal(TagStructure.WithoutEndTag, result.TagMatchingRules[0].TagStructure);
            Assert.False(result.TagMatchingRules[0].CaseSensitive);

            Assert.Equal("alternate-tag", result.TagMatchingRules[1].TagName);
            Assert.Null(result.TagMatchingRules[1].ParentTag);
            Assert.Equal(TagStructure.NormalOrSelfClosing, result.TagMatchingRules[1].TagStructure);
        }

        [Fact]
        public void RoundTrip_TagHelperWithRequiredAttributes_PreservesData()
        {
            // Arrange
            var json = """
                [
                    {
                        "Flags": 1,
                        "Name": "RequiredAttrTagHelper",
                        "AssemblyName": "TestAssembly",
                        "TypeName": "TestNamespace.RequiredAttrTagHelper",
                        "TagMatchingRules": [
                            {
                                "TagName": "required-tag",
                                "Attributes": [
                                    {
                                        "Flags": 0,
                                        "Name": "required-attr",
                                        "NameComparison": 0,
                                        "Value": "expected-value",
                                        "ValueComparison": 1
                                    }
                                ]
                            }
                        ]
                    }
                ]
                """;

            // Act
            var deserialized = Deserialize(json);
            var reserialized = Serialize(deserialized);
            var roundTripped = Deserialize(reserialized);

            // Assert
            Assert.Single(roundTripped);
            var result = roundTripped[0];

            Assert.Single(result.TagMatchingRules);
            Assert.Single(result.TagMatchingRules[0].Attributes);

            var attr = result.TagMatchingRules[0].Attributes[0];
            Assert.Equal("required-attr", attr.Name);
            Assert.Equal(RequiredAttributeNameComparison.FullMatch, attr.NameComparison);
            Assert.Equal("expected-value", attr.Value);
            Assert.Equal(RequiredAttributeValueComparison.FullMatch, attr.ValueComparison);
        }

        [Fact]
        public void RoundTrip_TagHelperWithAllowedChildTags_PreservesData()
        {
            // Arrange
            var json = """
                [
                    {
                        "Flags": 1,
                        "Name": "ParentTagHelper",
                        "AssemblyName": "TestAssembly",
                        "TypeName": "TestNamespace.ParentTagHelper",
                        "TagMatchingRules": [
                            {
                                "TagName": "parent-tag"
                            }
                        ],
                        "AllowedChildTags": [
                            {
                                "Name": "child-one",
                                "DisplayName": "Child One",
                                "Diagnostics": []
                            },
                            {
                                "Name": "child-two",
                                "DisplayName": "Child Two",
                                "Diagnostics": []
                            }
                        ]
                    }
                ]
                """;

            // Act
            var deserialized = Deserialize(json);
            var reserialized = Serialize(deserialized);
            var roundTripped = Deserialize(reserialized);

            // Assert
            Assert.Single(roundTripped);
            var result = roundTripped[0];

            Assert.Equal(2, result.AllowedChildTags.Length);
            Assert.Equal("child-one", result.AllowedChildTags[0].Name);
            Assert.Equal("Child One", result.AllowedChildTags[0].DisplayName);
            Assert.Equal("child-two", result.AllowedChildTags[1].Name);
            Assert.Equal("Child Two", result.AllowedChildTags[1].DisplayName);
        }

        [Fact]
        public void RoundTrip_ComponentTagHelper_PreservesMetadata()
        {
            // Arrange
            // When Kind is not specified, it defaults to Component.
            // The serializer uses WriteIfNotDefault, so it won't write Kind if it's the default (Component).
            // For this test, we omit Kind to rely on the default behavior.
            var json = """
                [
                    {
                        "Flags": 1,
                        "Name": "MyComponent",
                        "AssemblyName": "ComponentAssembly",
                        "TypeName": "ComponentNamespace.MyComponent",
                        "TagMatchingRules": [
                            {
                                "TagName": "MyComponent"
                            }
                        ],
                        "MetadataKind": 5
                    }
                ]
                """;

            // Act
            var deserialized = Deserialize(json);
            var reserialized = Serialize(deserialized);
            var roundTripped = Deserialize(reserialized);

            // Assert
            Assert.Single(roundTripped);
            var result = roundTripped[0];

            Assert.Equal(TagHelperKind.Component, result.Kind);
            Assert.Equal(RuntimeKind.IComponent, result.RuntimeKind);
            Assert.Equal("MyComponent", result.Name);
        }

        [Fact]
        public void RoundTrip_MultipleTagHelpers_PreservesAll()
        {
            // Arrange
            var json = """
                [
                    {
                        "Flags": 1,
                        "Name": "FirstTagHelper",
                        "AssemblyName": "TestAssembly",
                        "TypeName": "TestNamespace.FirstTagHelper",
                        "TagMatchingRules": [{ "TagName": "first-tag" }]
                    },
                    {
                        "Flags": 1,
                        "Name": "SecondTagHelper",
                        "AssemblyName": "TestAssembly",
                        "TypeName": "TestNamespace.SecondTagHelper",
                        "TagMatchingRules": [{ "TagName": "second-tag" }]
                    },
                    {
                        "Flags": 1,
                        "Name": "ThirdTagHelper",
                        "AssemblyName": "OtherAssembly",
                        "TypeName": "OtherNamespace.ThirdTagHelper",
                        "TagMatchingRules": [{ "TagName": "third-tag" }]
                    }
                ]
                """;

            // Act
            var deserialized = Deserialize(json);
            var reserialized = Serialize(deserialized);
            var roundTripped = Deserialize(reserialized);

            // Assert
            Assert.Equal(3, roundTripped.Count);
            Assert.Equal("FirstTagHelper", roundTripped[0].Name);
            Assert.Equal("SecondTagHelper", roundTripped[1].Name);
            Assert.Equal("ThirdTagHelper", roundTripped[2].Name);
            Assert.Equal("OtherAssembly", roundTripped[2].AssemblyName);
        }

        [Fact]
        public void RoundTrip_EmptyTagHelperList_PreservesEmpty()
        {
            // Arrange
            var json = "[]";

            // Act
            var deserialized = Deserialize(json);
            var reserialized = Serialize(deserialized);
            var roundTripped = Deserialize(reserialized);

            // Assert
            Assert.Empty(roundTripped);
        }

        [Fact]
        public void RoundTrip_TagHelperWithDocumentation_PreservesDocumentation()
        {
            // Arrange - Documentation as a string
            var json = """
                [
                    {
                        "Flags": 1,
                        "Name": "DocumentedTagHelper",
                        "AssemblyName": "TestAssembly",
                        "TypeName": "TestNamespace.DocumentedTagHelper",
                        "Documentation": "This is a documented tag helper.",
                        "TagMatchingRules": [{ "TagName": "doc-tag" }]
                    }
                ]
                """;

            // Act
            var deserialized = Deserialize(json);
            var reserialized = Serialize(deserialized);
            var roundTripped = Deserialize(reserialized);

            // Assert
            Assert.Single(roundTripped);
            Assert.Equal("This is a documented tag helper.", roundTripped[0].Documentation);
        }

        [Fact]
        public void RoundTrip_TagHelperWithDocumentationDescriptor_PreservesDocumentation()
        {
            // Arrange - Documentation as a DocumentationDescriptor with Id and Args
            var json = """
                [
                    {
                        "Flags": 1,
                        "Name": "DocumentedTagHelper",
                        "AssemblyName": "TestAssembly",
                        "TypeName": "TestNamespace.DocumentedTagHelper",
                        "Documentation": {
                            "Id": 1,
                            "Args": ["SomeArg"]
                        },
                        "TagMatchingRules": [{ "TagName": "doc-tag" }]
                    }
                ]
                """;

            // Act
            var deserialized = Deserialize(json);
            var reserialized = Serialize(deserialized);
            var roundTripped = Deserialize(reserialized);

            // Assert
            Assert.Single(roundTripped);
            // Documentation should be preserved (the exact format depends on the DocumentationDescriptor)
            Assert.NotNull(roundTripped[0].Documentation);
        }

        [Fact]
        public void RoundTrip_TagHelperWithBoundAttributeParameters_PreservesParameters()
        {
            // Arrange
            var json = """
                [
                    {
                        "Flags": 1,
                        "Name": "ParameterTagHelper",
                        "AssemblyName": "TestAssembly",
                        "TypeName": "TestNamespace.ParameterTagHelper",
                        "TagMatchingRules": [{ "TagName": "param-tag" }],
                        "BoundAttributes": [
                            {
                                "Flags": 0,
                                "Name": "bind-value",
                                "PropertyName": "Value",
                                "TypeName": "System.String",
                                "DisplayName": "Bind Value",
                                "Parameters": [
                                    {
                                        "Flags": 0,
                                        "Name": "format",
                                        "PropertyName": "Format",
                                        "TypeName": "System.String"
                                    },
                                    {
                                        "Flags": 0,
                                        "Name": "culture",
                                        "PropertyName": "Culture",
                                        "TypeName": "System.Globalization.CultureInfo"
                                    }
                                ]
                            }
                        ]
                    }
                ]
                """;

            // Act
            var deserialized = Deserialize(json);
            var reserialized = Serialize(deserialized);
            var roundTripped = Deserialize(reserialized);

            // Assert
            Assert.Single(roundTripped);
            var result = roundTripped[0];

            Assert.Single(result.BoundAttributes);
            var attr = result.BoundAttributes[0];

            Assert.Equal(2, attr.Parameters.Length);
            Assert.Equal("format", attr.Parameters[0].Name);
            Assert.Equal("Format", attr.Parameters[0].PropertyName);
            Assert.Equal("System.String", attr.Parameters[0].TypeName);
            Assert.Equal("culture", attr.Parameters[1].Name);
            Assert.Equal("Culture", attr.Parameters[1].PropertyName);
        }

        [Fact]
        public void Serialize_ProducesValidJson()
        {
            // Arrange
            var json = """
                [
                    {
                        "Flags": 1,
                        "Name": "TestTagHelper",
                        "AssemblyName": "TestAssembly",
                        "TypeName": "TestNamespace.TestTagHelper",
                        "TagMatchingRules": [{ "TagName": "test-tag" }]
                    }
                ]
                """;

            // Act
            var deserialized = Deserialize(json);
            var serialized = Serialize(deserialized);

            // Assert - should be valid JSON
            Assert.NotNull(serialized);
            Assert.StartsWith("[", serialized);
            Assert.EndsWith("]", serialized);

            // Should parse without error
            using var document = JsonDocument.Parse(serialized);
            Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
            Assert.Equal(1, document.RootElement.GetArrayLength());
        }

        [Fact]
        public void Deserialize_InvalidJson_Throws()
        {
            // Arrange
            var invalidJson = "{ not valid json";

            // Act & Assert
            Assert.Throws<JsonException>(() =>
            {
                Deserialize(invalidJson);
            });
        }

        [Fact]
        public void Deserialize_EmptyArray_ReturnsEmptyCollection()
        {
            // Arrange
            var json = "[]";

            // Act
            var result = Deserialize(json);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void RoundTrip_TagHelperFlags_PreservesFlags()
        {
            // Arrange - CaseSensitive flag (value 1)
            var json = """
                [
                    {
                        "Flags": 1,
                        "Name": "CaseSensitiveTagHelper",
                        "AssemblyName": "TestAssembly",
                        "TypeName": "TestNamespace.CaseSensitiveTagHelper",
                        "TagMatchingRules": [{ "TagName": "case-tag" }]
                    }
                ]
                """;

            // Act
            var deserialized = Deserialize(json);
            var reserialized = Serialize(deserialized);
            var roundTripped = Deserialize(reserialized);

            // Assert
            Assert.Single(roundTripped);
            Assert.True(roundTripped[0].CaseSensitive);
        }

        [Fact]
        public void RoundTrip_TagHelperWithIndexerAttribute_PreservesIndexer()
        {
            // Arrange
            var json = """
                [
                    {
                        "Flags": 1,
                        "Name": "IndexerTagHelper",
                        "AssemblyName": "TestAssembly",
                        "TypeName": "TestNamespace.IndexerTagHelper",
                        "TagMatchingRules": [{ "TagName": "indexer-tag" }],
                        "BoundAttributes": [
                            {
                                "Flags": 0,
                                "Name": "items",
                                "PropertyName": "Items",
                                "TypeName": "System.Collections.Generic.Dictionary`2[[System.String],[System.Object]]",
                                "IndexerNamePrefix": "item-",
                                "IndexerTypeName": "System.Object",
                                "DisplayName": "Items Dictionary"
                            }
                        ]
                    }
                ]
                """;

            // Act
            var deserialized = Deserialize(json);
            var reserialized = Serialize(deserialized);
            var roundTripped = Deserialize(reserialized);

            // Assert
            Assert.Single(roundTripped);
            var attr = roundTripped[0].BoundAttributes[0];
            Assert.Equal("item-", attr.IndexerNamePrefix);
            Assert.Equal("System.Object", attr.IndexerTypeName);
        }

        [Fact]
        public void RoundTrip_TagHelperWithTypeNameObject_PreservesTypeInfo()
        {
            // Arrange - TypeName as an object with FullName, Namespace, and Name
            var json = """
                [
                    {
                        "Flags": 1,
                        "Name": "TypedTagHelper",
                        "AssemblyName": "TestAssembly",
                        "TypeName": {
                            "FullName": "TestNamespace.Nested.TypedTagHelper",
                            "Namespace": "TestNamespace.Nested",
                            "Name": "TypedTagHelper"
                        },
                        "TagMatchingRules": [{ "TagName": "typed-tag" }]
                    }
                ]
                """;

            // Act
            var deserialized = Deserialize(json);
            var reserialized = Serialize(deserialized);
            var roundTripped = Deserialize(reserialized);

            // Assert
            Assert.Single(roundTripped);
            Assert.Equal("TestNamespace.Nested.TypedTagHelper", roundTripped[0].TypeName);
            Assert.Equal("TestNamespace.Nested", roundTripped[0].TypeNamespace);
            Assert.Equal("TypedTagHelper", roundTripped[0].TypeNameIdentifier);
        }

        [Fact]
        public void RoundTrip_TagHelperWithTagOutputHint_PreservesHint()
        {
            // Arrange
            var json = """
                [
                    {
                        "Flags": 1,
                        "Name": "OutputHintTagHelper",
                        "AssemblyName": "TestAssembly",
                        "TypeName": "TestNamespace.OutputHintTagHelper",
                        "TagOutputHint": "div",
                        "TagMatchingRules": [{ "TagName": "custom-tag" }]
                    }
                ]
                """;

            // Act
            var deserialized = Deserialize(json);
            var reserialized = Serialize(deserialized);
            var roundTripped = Deserialize(reserialized);

            // Assert
            Assert.Single(roundTripped);
            Assert.Equal("div", roundTripped[0].TagOutputHint);
        }

        [Fact]
        public void RoundTrip_ComplexTagHelper_PreservesAllData()
        {
            // Arrange - A complex tag helper with multiple features
            // TagStructure enum: Unspecified=0, NormalOrSelfClosing=1, WithoutEndTag=2
            // Kind and RuntimeKind are omitted to use defaults (Component and IComponent)
            var json = """
                [
                    {
                        "Flags": 1,
                        "Name": "ComplexComponent",
                        "AssemblyName": "ComplexAssembly",
                        "DisplayName": "Complex Component",
                        "TypeName": {
                            "FullName": "ComplexNamespace.ComplexComponent",
                            "Namespace": "ComplexNamespace",
                            "Name": "ComplexComponent"
                        },
                        "Documentation": "A complex component with many features.",
                        "TagOutputHint": "section",
                        "TagMatchingRules": [
                            {
                                "TagName": "ComplexComponent",
                                "ParentTag": "div",
                                "TagStructure": 1,
                                "Attributes": [
                                    {
                                        "Flags": 0,
                                        "Name": "id"
                                    }
                                ]
                            }
                        ],
                        "BoundAttributes": [
                            {
                                "Flags": 0,
                                "Name": "title",
                                "PropertyName": "Title",
                                "TypeName": "System.String",
                                "DisplayName": "Title"
                            }
                        ],
                        "AllowedChildTags": [
                            {
                                "Name": "content",
                                "DisplayName": "Content",
                                "Diagnostics": []
                            }
                        ],
                        "MetadataKind": 5
                    }
                ]
                """;

            // Act
            var deserialized = Deserialize(json);
            var reserialized = Serialize(deserialized);
            var roundTripped = Deserialize(reserialized);

            // Assert
            Assert.Single(roundTripped);
            var result = roundTripped[0];

            Assert.Equal("ComplexComponent", result.Name);
            Assert.Equal("ComplexAssembly", result.AssemblyName);
            Assert.Equal("Complex Component", result.DisplayName);
            Assert.Equal("ComplexNamespace.ComplexComponent", result.TypeName);
            Assert.Equal("ComplexNamespace", result.TypeNamespace);
            Assert.Equal("A complex component with many features.", result.Documentation);
            Assert.Equal("section", result.TagOutputHint);
            Assert.True(result.CaseSensitive);

            Assert.Single(result.TagMatchingRules);
            Assert.Equal("ComplexComponent", result.TagMatchingRules[0].TagName);
            Assert.Equal("div", result.TagMatchingRules[0].ParentTag);
            Assert.Equal(TagStructure.NormalOrSelfClosing, result.TagMatchingRules[0].TagStructure);

            Assert.Single(result.BoundAttributes);
            Assert.Equal("title", result.BoundAttributes[0].Name);

            Assert.Single(result.AllowedChildTags);
            Assert.Equal("content", result.AllowedChildTags[0].Name);
        }

        #region Helper Methods

        private static string Serialize(IReadOnlyList<TagHelperDescriptor> tagHelpers)
        {
            using var stream = new MemoryStream();
            JsonSerializer.Serialize(stream, tagHelpers, TagHelperDescriptorJsonConverter.SerializerOptions);
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static IReadOnlyList<TagHelperDescriptor> Deserialize(string json)
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            return JsonSerializer.Deserialize<IReadOnlyList<TagHelperDescriptor>>(stream, TagHelperDescriptorJsonConverter.SerializerOptions);
        }

        #endregion
    }
}
