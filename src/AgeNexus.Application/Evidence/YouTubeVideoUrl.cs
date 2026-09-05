namespace AgeNexus.Application.Evidence;

public sealed record YouTubeVideoReference(string VideoId, string WatchUrl, string EmbedUrl);

public static class YouTubeVideoUrl
{
    public static bool TryParse(string? value, out YouTubeVideoReference? video)
    {
        video = null;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var host = uri.Host.TrimEnd('.').ToLowerInvariant();
        string? videoId = null;
        if (host is "youtu.be" or "www.youtu.be")
        {
            videoId = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com")
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2 && segments[0] is "embed" or "live" or "shorts")
            {
                videoId = segments[1];
            }
            else if (segments.Length == 1 && segments[0] == "watch")
            {
                videoId = ParseQueryValue(uri.Query, "v");
            }
        }

        if (videoId is null || videoId.Length != 11 ||
            videoId.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
        {
            return false;
        }

        video = new YouTubeVideoReference(
            videoId,
            $"https://www.youtube.com/watch?v={videoId}",
            $"https://www.youtube-nocookie.com/embed/{videoId}");
        return true;
    }

    private static string? ParseQueryValue(string query, string name)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && Uri.UnescapeDataString(pair[0]).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(pair[1].Replace('+', ' '));
            }
        }

        return null;
    }
}
