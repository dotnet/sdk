#if (!csharpFeature_ImplicitUsings)
using Microsoft.Testing.Platform.Builder;
using System.Threading.Tasks;

#else
using Microsoft.Testing.Platform.Builder;

#endif
#if (csharpFeature_FileScopedNamespaces)
namespace Company.TestProject1;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        SelfRegisteredExtensions.AddSelfRegisteredExtensions(builder, args);
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}
#else
namespace Company.TestProject1
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
            SelfRegisteredExtensions.AddSelfRegisteredExtensions(builder, args);
            using (ITestApplication app = await builder.BuildAsync())
            {
                return await app.RunAsync();
            }
        }
    }
}
#endif
