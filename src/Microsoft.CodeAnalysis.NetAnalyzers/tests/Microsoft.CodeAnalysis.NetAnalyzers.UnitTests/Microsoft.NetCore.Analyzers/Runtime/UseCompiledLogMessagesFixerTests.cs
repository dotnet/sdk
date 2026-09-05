// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.NetCore.CSharp.Analyzers.Runtime;
using Test.Utilities;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.LoggerMessageDefineAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpUseCompiledLogMessagesFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests;

/// <summary>
/// Tests for CA1848 code fixer <see cref="CSharpUseCompiledLogMessagesFixer"/>.
/// The fixer generates [LoggerMessage] attributed extension methods.
/// Uses Pattern 2: Verify Analyzer + Code Fix with explicit DiagnosticResult.
/// </summary>
public class UseCompiledLogMessagesFixerTests
{
    #region Fixer Property Tests

    [Fact]
    public void FixerFixableDiagnosticIds()
    {
        var fixer = new CSharpUseCompiledLogMessagesFixer();
        Assert.Single(fixer.FixableDiagnosticIds);
        Assert.Equal("CA1848", fixer.FixableDiagnosticIds[0]);
    }

    [Fact]
    public void FixerGetFixAllProviderReturnsNull()
    {
        var fixer = new CSharpUseCompiledLogMessagesFixer();
        Assert.Null(fixer.GetFixAllProvider());
    }

    #endregion

    #region Analyzer Tests - Verify Diagnostics with Explicit DiagnosticResult (Pattern 2)

    [Fact]
    public async Task CA1848_Analyzer_SimpleLogTrace()
    {
        const string Source = @"
using Microsoft.Extensions.Logging;

namespace Example
{
    public class TestClass
    {
        public void Test(ILogger logger)
        {
            logger.LogTrace(""Hello"");
        }
    }
}
";

        await new VerifyCS.Test
        {
            LanguageVersion = LanguageVersion.CSharp9,
            ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMELogging,
            TestCode = Source,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic("CA1848")
                    .WithSpan(10, 13, 10, 37)
                    .WithArguments("LoggerExtensions.LogTrace(ILogger, string, params object[])"),
            },
            // Skip fix verification - this test focuses on analyzer detection
            NumberOfFixAllIterations = 0,
        }.RunAsync();
    }

    [Fact]
    public async Task CA1848_Analyzer_WithException()
    {
        const string Source = @"
using System;
using Microsoft.Extensions.Logging;

namespace Example
{
    public class TestClass
    {
        public void Test(ILogger logger, Exception ex)
        {
            logger.LogError(ex, ""An error occurred"");
        }
    }
}
";

        await new VerifyCS.Test
        {
            LanguageVersion = LanguageVersion.CSharp9,
            ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMELogging,
            TestCode = Source,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic("CA1848")
                    .WithSpan(11, 13, 11, 53)
                    .WithArguments("LoggerExtensions.LogError(ILogger, Exception, string, params object[])"),
            },
            NumberOfFixAllIterations = 0,
        }.RunAsync();
    }

    [Fact]
    public async Task CA1848_Analyzer_AllLogLevels()
    {
        const string Source = @"
using Microsoft.Extensions.Logging;

namespace Example
{
    public class TestClass
    {
        public void Test(ILogger logger)
        {
            logger.LogTrace(""Trace"");
            logger.LogDebug(""Debug"");
            logger.LogInformation(""Info"");
            logger.LogWarning(""Warn"");
            logger.LogError(""Error"");
            logger.LogCritical(""Critical"");
        }
    }
}
";

        await new VerifyCS.Test
        {
            LanguageVersion = LanguageVersion.CSharp9,
            ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMELogging,
            TestCode = Source,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic("CA1848").WithSpan(10, 13, 10, 37).WithArguments("LoggerExtensions.LogTrace(ILogger, string, params object[])"),
                VerifyCS.Diagnostic("CA1848").WithSpan(11, 13, 11, 37).WithArguments("LoggerExtensions.LogDebug(ILogger, string, params object[])"),
                VerifyCS.Diagnostic("CA1848").WithSpan(12, 13, 12, 42).WithArguments("LoggerExtensions.LogInformation(ILogger, string, params object[])"),
                VerifyCS.Diagnostic("CA1848").WithSpan(13, 13, 13, 38).WithArguments("LoggerExtensions.LogWarning(ILogger, string, params object[])"),
                VerifyCS.Diagnostic("CA1848").WithSpan(14, 13, 14, 37).WithArguments("LoggerExtensions.LogError(ILogger, string, params object[])"),
                VerifyCS.Diagnostic("CA1848").WithSpan(15, 13, 15, 43).WithArguments("LoggerExtensions.LogCritical(ILogger, string, params object[])"),
            },
            NumberOfFixAllIterations = 0,
        }.RunAsync();
    }

    [Fact]
    public async Task CA1848_Analyzer_WithEventIdPassed()
    {
        const string Source = @"
using Microsoft.Extensions.Logging;

namespace Example
{
    public class TestClass
    {
        public void Test(ILogger logger)
        {
            logger.LogTrace(new EventId(1), ""Hello"");
        }
    }
}
";

        await new VerifyCS.Test
        {
            LanguageVersion = LanguageVersion.CSharp9,
            ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMELogging,
            TestCode = Source,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic("CA1848")
                    .WithSpan(10, 13, 10, 53)
                    .WithArguments("LoggerExtensions.LogTrace(ILogger, EventId, string, params object[])"),
            },
            // The source should remain unchanged since no fix is offered
            FixedCode = Source,
        }.RunAsync();
    }

    [Fact]
    public async Task CA1848_NoDiagnostic_WhenUsingLoggerMessageDefine()
    {
        const string Source = @"
#nullable enable
using Microsoft.Extensions.Logging;

namespace Example
{
    public static partial class Log
    {
        private static readonly System.Action<ILogger, System.Exception?> _logHello =
            LoggerMessage.Define(LogLevel.Trace, new EventId(1), ""Hello"");

        public static void LogHello(this ILogger logger)
        {
            _logHello(logger, null);
        }
    }

    public class TestClass
    {
        public void Test(ILogger logger)
        {
            logger.LogHello();
        }
    }
}
";

        // No diagnostics expected - source should compile without changes
        await new VerifyCS.Test
        {
            LanguageVersion = LanguageVersion.CSharp9,
            ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMELogging,
            TestCode = Source,
        }.RunAsync();
    }

    [Fact]
    public async Task CA1848_Analyzer_WithFileScopedNamespace()
    {
        const string Source = @"
using Microsoft.Extensions.Logging;

namespace Example;

public class TestClass
{
    public void Test(ILogger logger)
    {
        logger.LogTrace(""Hello"");
    }
}
";

        await new VerifyCS.Test
        {
            LanguageVersion = LanguageVersion.CSharp10,
            ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMELogging,
            TestCode = Source,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic("CA1848")
                    .WithSpan(10, 9, 10, 33)
                    .WithArguments("LoggerExtensions.LogTrace(ILogger, string, params object[])"),
            },
            NumberOfFixAllIterations = 0,
        }.RunAsync();
    }

    [Fact]
    public async Task CA1848_Analyzer_WithConditionalAccess()
    {
        const string Source = @"
#nullable enable
using Microsoft.Extensions.Logging;

namespace Example
{
    public class TestClass
    {
        public void Test(ILogger? logger)
        {
            logger?.LogTrace(""Hello"");
        }
    }
}
";

        await new VerifyCS.Test
        {
            LanguageVersion = LanguageVersion.CSharp9,
            ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMELogging,
            TestCode = Source,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic("CA1848")
                    .WithSpan(11, 20, 11, 38)
                    .WithArguments("LoggerExtensions.LogTrace(ILogger, string, params object[])"),
            },
            NumberOfFixAllIterations = 0,
        }.RunAsync();
    }

    [Fact]
    public async Task CA1848_Analyzer_WithMessageParameter()
    {
        const string Source = @"
using Microsoft.Extensions.Logging;

namespace Example
{
    public class TestClass
    {
        public void Test(ILogger logger, string user)
        {
            logger.LogTrace(""Hello {User}"", user);
        }
    }
}
";

        await new VerifyCS.Test
        {
            LanguageVersion = LanguageVersion.CSharp9,
            ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMELogging,
            TestCode = Source,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic("CA1848")
                    .WithSpan(10, 13, 10, 50)
                    .WithArguments("LoggerExtensions.LogTrace(ILogger, string, params object[])"),
            },
            NumberOfFixAllIterations = 0,
        }.RunAsync();
    }

    [Fact]
    public async Task CA1848_Analyzer_SimpleLogDebug()
    {
        const string Source = @"
using Microsoft.Extensions.Logging;

namespace Example
{
    public class TestClass
    {
        public void Test(ILogger logger)
        {
            logger.LogDebug(""Debug message"");
        }
    }
}
";

        await new VerifyCS.Test
        {
            LanguageVersion = LanguageVersion.CSharp9,
            ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMELogging,
            TestCode = Source,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic("CA1848")
                    .WithSpan(10, 13, 10, 45)
                    .WithArguments("LoggerExtensions.LogDebug(ILogger, string, params object[])"),
            },
            NumberOfFixAllIterations = 0,
        }.RunAsync();
    }

    [Fact]
    public async Task CA1848_Analyzer_LogWarning()
    {
        const string Source = @"
using Microsoft.Extensions.Logging;

namespace Example
{
    public class TestClass
    {
        public void Test(ILogger logger)
        {
            logger.LogWarning(""Warning message"");
        }
    }
}
";

        await new VerifyCS.Test
        {
            LanguageVersion = LanguageVersion.CSharp9,
            ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMELogging,
            TestCode = Source,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic("CA1848")
                    .WithSpan(10, 13, 10, 49)
                    .WithArguments("LoggerExtensions.LogWarning(ILogger, string, params object[])"),
            },
            NumberOfFixAllIterations = 0,
        }.RunAsync();
    }

    [Fact]
    public async Task CA1848_Analyzer_MultipleParameters()
    {
        const string Source = @"
using Microsoft.Extensions.Logging;

namespace Example
{
    public class TestClass
    {
        public void Test(ILogger logger, string name, int id)
        {
            logger.LogInformation(""User {Name} has ID {Id}"", name, id);
        }
    }
}
";

        await new VerifyCS.Test
        {
            LanguageVersion = LanguageVersion.CSharp9,
            ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMELogging,
            TestCode = Source,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic("CA1848")
                    .WithSpan(10, 13, 10, 71)
                    .WithArguments("LoggerExtensions.LogInformation(ILogger, string, params object[])"),
            },
            NumberOfFixAllIterations = 0,
        }.RunAsync();
    }

    #endregion
}
