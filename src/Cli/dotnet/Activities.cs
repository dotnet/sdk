using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli;


public static class Activities
{
    public static ActivitySource s_source = new("dotnet-cli", Product.Version);

    public const string DOTNET_CLI_TRACEPARENT = nameof(DOTNET_CLI_TRACEPARENT);
    public const string DOTNET_CLI_TRACESTATE = nameof(DOTNET_CLI_TRACESTATE);
}
