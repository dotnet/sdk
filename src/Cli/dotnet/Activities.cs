using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli;


public static class Activities
{
    public static ActivitySource Source = new("dotnet-cli", Product.Version);
}
