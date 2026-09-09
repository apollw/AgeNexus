using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgeNexus.Application.Evidence;
using Microsoft.Extensions.Configuration;

namespace AgeNexus.Infrastructure.Evidence;

internal sealed class SupabaseEvidenceObjectStorage : IEvidenceObjectStorage
{
    private readonly HttpClient client;
    private readonly Uri? baseUri;
    private readonly string? serviceRoleKey;
    private readonly string bucket;
    private readonly SemaphoreSlim setupGate = new(1, 1);
    private bool bucketReady;

    public SupabaseEvidenceObjectStorage(IHttpClientFactory clientFactory, IConfiguration configuration)
    {
        client = clientFactory.CreateClient(nameof(SupabaseEvidenceObjectStorage));
        var configuredUrl = configuration["Supabase:Url"]?.Trim().TrimEnd('/');
        if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var parsedUri) && parsedUri.Scheme == Uri.UriSchemeHttps)
        {
            baseUri = parsedUri;
        }

        serviceRoleKey = configuration["Supabase:ServiceRoleKey"]?.Trim();
        var configuredBucket = configuration["Supabase:EvidenceBucket"]?.Trim();
        bucket = string.IsNullOrWhiteSpace(configuredBucket) ? "match-evidence" : configuredBucket;
    }

    public bool IsConfigured => baseUri is not null && !string.IsNullOrWhiteSpace(serviceRoleKey);

    public async Task UploadAsync(
        string objectKey,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureBucketAsync(cancellationToken);
        using var request = CreateRequest(HttpMethod.Post,
            $"storage/v1/object/{Escape(bucket)}/{EscapePath(objectKey)}");
        request.Headers.TryAddWithoutValidation("x-upsert", "false");
        request.Content = new ByteArrayContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = CreateRequest(HttpMethod.Delete, $"storage/v1/object/{Escape(bucket)}");
        request.Content = JsonContent.Create(new { prefixes = new[] { objectKey } });
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public string GetPublicUrl(string objectKey)
    {
        EnsureConfigured();
        return new Uri(baseUri!, $"storage/v1/object/public/{Escape(bucket)}/{EscapePath(objectKey)}").AbsoluteUri;
    }

    public string GetPublicDownloadUrl(string objectKey, string fileName)
    {
        var publicUrl = GetPublicUrl(objectKey);
        return $"{publicUrl}?download={Uri.EscapeDataString(fileName)}";
    }

    public string? TryGetObjectKey(string publicUrl)
    {
        if (!IsConfigured || !Uri.TryCreate(publicUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var prefix = $"/storage/v1/object/public/{Escape(bucket)}/";
        if (uri.Host != baseUri!.Host || !uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return Uri.UnescapeDataString(uri.AbsolutePath[prefix.Length..]);
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (bucketReady)
        {
            return;
        }

        await setupGate.WaitAsync(cancellationToken);
        try
        {
            if (bucketReady)
            {
                return;
            }

            using var lookup = CreateRequest(HttpMethod.Get, $"storage/v1/bucket/{Escape(bucket)}");
            using var lookupResponse = await client.SendAsync(lookup, cancellationToken);
            if (lookupResponse.IsSuccessStatusCode)
            {
                await UpdateBucketAsync(cancellationToken);
                bucketReady = true;
                return;
            }

            if (lookupResponse.StatusCode != HttpStatusCode.NotFound)
            {
                lookupResponse.EnsureSuccessStatusCode();
            }

            using var create = CreateRequest(HttpMethod.Post, "storage/v1/bucket");
            create.Content = JsonContent.Create(new
            {
                id = bucket,
                name = bucket,
                @public = true,
                file_size_limit = 50 * 1024 * 1024,
                allowed_mime_types = new[] { "image/jpeg", "image/png", "image/webp", "application/octet-stream" }
            });
            using var createResponse = await client.SendAsync(create, cancellationToken);
            if (!createResponse.IsSuccessStatusCode && createResponse.StatusCode != HttpStatusCode.Conflict)
            {
                createResponse.EnsureSuccessStatusCode();
            }

            bucketReady = true;
        }
        finally
        {
            setupGate.Release();
        }
    }

    private async Task UpdateBucketAsync(CancellationToken cancellationToken)
    {
        using var update = CreateRequest(HttpMethod.Put, $"storage/v1/bucket/{Escape(bucket)}");
        update.Content = JsonContent.Create(new
        {
            @public = true,
            file_size_limit = 50 * 1024 * 1024,
            allowed_mime_types = new[] { "image/jpeg", "image/png", "image/webp", "application/octet-stream" }
        });
        using var updateResponse = await client.SendAsync(update, cancellationToken);
        updateResponse.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        EnsureConfigured();
        var request = new HttpRequestMessage(method, new Uri(baseUri!, relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);
        request.Headers.TryAddWithoutValidation("apikey", serviceRoleKey);
        return request;
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Supabase Storage is not configured.");
        }
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string EscapePath(string value) =>
        string.Join('/', value.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
}
