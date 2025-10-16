// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferJsonElementParse,
    Microsoft.NetCore.Analyzers.Runtime.PreferJsonElementParseFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferJsonElementParseTests
    {
        [Fact]
        public async Task NoDiagnostic_WhenJsonElementParseNotAvailable()
        {
            var source = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement { }
                }

                class Test
                {
                    void M()
                    {
                        JsonElement element = JsonDocument.Parse("json").RootElement;
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task Diagnostic_WhenUsingJsonDocumentParseRootElement()
        {
            var source = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json) => default;
                    }
                }

                class Test
                {
                    void M()
                    {
                        JsonElement element = [|JsonDocument.Parse("json").RootElement|];
                    }
                }
                """;
            var fixedSource = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json) => default;
                    }
                }

                class Test
                {
                    void M()
                    {
                        JsonElement element = JsonElement.Parse("json");
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task Diagnostic_WhenUsingJsonDocumentParseRootElement_WithMultipleArguments()
        {
            var source = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json, JsonDocumentOptions options) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json, JsonDocumentOptions options) => default;
                    }

                    public struct JsonDocumentOptions { }
                }

                class Test
                {
                    void M()
                    {
                        JsonElement element = [|JsonDocument.Parse("json", default).RootElement|];
                    }
                }
                """;
            var fixedSource = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json, JsonDocumentOptions options) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json, JsonDocumentOptions options) => default;
                    }

                    public struct JsonDocumentOptions { }
                }

                class Test
                {
                    void M()
                    {
                        JsonElement element = JsonElement.Parse("json", default);
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NoDiagnostic_WhenJsonDocumentProperlyDisposed_UsingStatement()
        {
            var source = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json) => default;
                    }
                }

                class Test
                {
                    void M()
                    {
                        using (var doc = JsonDocument.Parse("json"))
                        {
                            JsonElement element = doc.RootElement;
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NoDiagnostic_WhenJsonDocumentProperlyDisposed_UsingDeclaration()
        {
            var source = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json) => default;
                    }
                }

                class Test
                {
                    void M()
                    {
                        using var doc = JsonDocument.Parse("json");
                        JsonElement element = doc.RootElement;
                    }
                }
                """;
            await new VerifyCS.Test
            {
                TestCode = source,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8
            }.RunAsync();
        }

        [Fact]
        public async Task Diagnostic_InlineAccess_DirectAssignment()
        {
            var source = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json) => default;
                    }
                }

                class Test
                {
                    void M(string json)
                    {
                        var element = [|JsonDocument.Parse(json).RootElement|];
                    }
                }
                """;
            var fixedSource = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json) => default;
                    }
                }

                class Test
                {
                    void M(string json)
                    {
                        var element = JsonElement.Parse(json);
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task Diagnostic_ReturnStatement()
        {
            var source = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json) => default;
                    }
                }

                class Test
                {
                    JsonElement M(string json)
                    {
                        return [|JsonDocument.Parse(json).RootElement|];
                    }
                }
                """;
            var fixedSource = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json) => default;
                    }
                }

                class Test
                {
                    JsonElement M(string json)
                    {
                        return JsonElement.Parse(json);
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task Diagnostic_ExpressionBodiedMember()
        {
            var source = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json) => default;
                    }
                }

                class Test
                {
                    JsonElement M(string json) => [|JsonDocument.Parse(json).RootElement|];
                }
                """;
            var fixedSource = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json) => default;
                    }
                }

                class Test
                {
                    JsonElement M(string json) => JsonElement.Parse(json);
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NoDiagnostic_WhenJsonElementParseHasNoMatchingOverload()
        {
            var source = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json, JsonDocumentOptions options) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        // JsonElement.Parse only has single string parameter, not the overload with options
                        public static JsonElement Parse(string json) => default;
                    }

                    public struct JsonDocumentOptions { }
                }

                class Test
                {
                    void M()
                    {
                        // This should NOT produce a diagnostic because JsonElement.Parse doesn't have an overload with JsonDocumentOptions
                        JsonElement element = JsonDocument.Parse("json", default(JsonDocumentOptions)).RootElement;
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task Diagnostic_WhenChainedWithOtherOperations()
        {
            var source = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json) => default;
                        public JsonValueKind ValueKind => default;
                    }

                    public enum JsonValueKind { }
                }

                class Test
                {
                    void M()
                    {
                        // Should produce diagnostic even when chained with property access
                        var kind = [|JsonDocument.Parse("json").RootElement|].ValueKind;
                    }
                }
                """;
            var fixedSource = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json) => default;
                        public JsonValueKind ValueKind => default;
                    }

                    public enum JsonValueKind { }
                }

                class Test
                {
                    void M()
                    {
                        // Should produce diagnostic even when chained with property access
                        var kind = JsonElement.Parse("json").ValueKind;
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NoDiagnostic_WhenRootElementAccessedViaVariable()
        {
            var source = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json) => default;
                    }
                }

                class Test
                {
                    void M()
                    {
                        // Not the pattern we're looking for - JsonDocument is stored in a variable
                        var doc = JsonDocument.Parse("json");
                        JsonElement element = doc.RootElement;
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task Diagnostic_MultipleParametersWithMatchingOverload()
        {
            var source = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json, JsonDocumentOptions options) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json, JsonDocumentOptions options) => default;
                    }

                    public struct JsonDocumentOptions { }
                }

                class Test
                {
                    void M()
                    {
                        JsonElement element = [|JsonDocument.Parse("json", default).RootElement|];
                    }
                }
                """;
            var fixedSource = """
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json, JsonDocumentOptions options) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json, JsonDocumentOptions options) => default;
                    }

                    public struct JsonDocumentOptions { }
                }

                class Test
                {
                    void M()
                    {
                        JsonElement element = JsonElement.Parse("json", default);
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task Diagnostic_InLambdaExpression()
        {
            var source = """
                using System;
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json) => default;
                    }
                }

                class Test
                {
                    void M()
                    {
                        Func<string, JsonElement> parser = json => [|JsonDocument.Parse(json).RootElement|];
                    }
                }
                """;
            var fixedSource = """
                using System;
                using System.Text.Json;

                namespace System.Text.Json
                {
                    public sealed class JsonDocument : System.IDisposable
                    {
                        public static JsonDocument Parse(string json) => null;
                        public JsonElement RootElement => default;
                        public void Dispose() { }
                    }
                    
                    public struct JsonElement
                    {
                        public static JsonElement Parse(string json) => default;
                    }
                }

                class Test
                {
                    void M()
                    {
                        Func<string, JsonElement> parser = json => JsonElement.Parse(json);
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }
    }
}
