using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli;


public static class Activities
{
    public static ActivitySource s_source = new("dotnet-cli", Product.Version);
}
