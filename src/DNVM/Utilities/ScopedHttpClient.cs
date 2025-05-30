
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Dnvm;

public sealed class ScopedHttpClient(HttpClient client)
{
    internal async Task<ScopedHttpResponseMessage> GetAsync([StringSyntax("Uri")] string url)
    {
        return new ScopedHttpResponseMessage(
            await client.GetAsync(url, CancelScope.Current.Token)
        );
    }

    internal async Task<ScopedHttpResponseMessage> GetAsync(
        [StringSyntax("Uri")] string url,
        HttpCompletionOption completionOption)
    {
        return new ScopedHttpResponseMessage(
            await client.GetAsync(url, completionOption, CancelScope.Current.Token)
        );
    }

    internal Task<string> GetStringAsync(string url)
    {
        // This is a commonly used method for small requests, so we set a default timeout in case
        // the caller forgets to set one
        return CancelScope.WithTimeoutAfter(
            DnvmEnv.DefaultTimeout,
            _ => client.GetStringAsync(url, CancelScope.Current.Token)
        );
    }

    internal Task<Stream> GetStreamAsync(string uri)
        => client.GetStreamAsync(uri, CancelScope.Current.Token);
}

public sealed class ScopedHttpResponseMessage(HttpResponseMessage response) : IDisposable
{
    public bool IsSuccessStatusCode => response.IsSuccessStatusCode;

    public ScopedHttpContent Content => new(response.Content);

    public void Dispose() => response.Dispose();

    public void EnsureSuccessStatusCode() => response.EnsureSuccessStatusCode();
}

public sealed class ScopedHttpContent(HttpContent content)
{
    public HttpContentHeaders Headers => content.Headers;

    public Task<string> ReadAsStringAsync()
    {
        return content.ReadAsStringAsync(CancelScope.Current.Token);
    }

    public async Task<ScopedStream> ReadAsStreamAsync()
    {
        return new(await content.ReadAsStreamAsync(CancelScope.Current.Token));
    }
}

public sealed class ScopedStream(Stream stream) : IDisposable
{
    public void Dispose() => stream.Dispose();

    public ValueTask<int> ReadAsync(Memory<byte> buffer)
        => stream.ReadAsync(buffer, CancelScope.Current.Token);
}