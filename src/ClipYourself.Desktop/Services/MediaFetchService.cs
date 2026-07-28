using System.IO;
using System.Net.Http;

namespace ClipYourself.Desktop.Services;

/// <summary>Downloads media referenced by a browser drag (images/videos arrive as URLs, not files).</summary>
public static class MediaFetchService
{
    private const long MaxBytes = 200L * 1024 * 1024;

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        // Some hosts 403 requests without a browser-ish User-Agent.
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ClipYourself/0.1");
        return client;
    }

    public readonly record struct Fetched(byte[] Bytes, string ContentType);

    public static async Task<Fetched?> TryFetchAsync(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

            using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode) return null;
            if (response.Content.Headers.ContentLength > MaxBytes) return null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxBytes) return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            return new Fetched(buffer.ToArray(), contentType);
        }
        catch
        {
            return null;
        }
    }
}
