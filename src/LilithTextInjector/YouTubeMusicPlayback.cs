using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LilithTextInjector;

internal static class YouTubeMusicPlayback
{
    private const string SearchEndpoint = "https://music.youtube.com/youtubei/v1/search?prettyPrint=false";
    private const string SongsFilter = "EgWKAQIIAWoKEAkQBRAKEAMQBA%3D%3D";
    private const string PlaylistsFilter = "EgWKAQIoAWoKEAkQBRAKEAMQBA%3D%3D";
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    internal readonly record struct PlayResult(bool Success, string Url, string Title);

    internal static PlayResult Play(string intent, string query)
    {
        intent = (intent ?? string.Empty).Trim().ToLowerInvariant();
        query = (query ?? string.Empty).Trim();
        try
        {
            return intent switch
            {
                "liked" or "likes" or "liked_songs" or "liked_music" =>
                    Open("https://music.youtube.com/playlist?list=LM", "Liked music"),
                "library" =>
                    Open("https://music.youtube.com/library", "YouTube Music library"),
                "playlist" => PlayPlaylist(query),
                "radio" => PlayRadio(query),
                _ => PlaySong(query)
            };
        }
        catch (Exception exception)
        {
            return new PlayResult(false, string.Empty, exception.Message);
        }
    }

    private static PlayResult PlaySong(string query)
    {
        if (query.Length < 1)
            return new PlayResult(false, string.Empty, "missing query");
        var videoId = FindFirstVideoId(Search(query, SongsFilter));
        if (string.IsNullOrWhiteSpace(videoId))
            return Open("https://music.youtube.com/search?q=" + Uri.EscapeDataString(query), query);
        return Open("https://music.youtube.com/watch?v=" + videoId, query);
    }

    private static PlayResult PlayPlaylist(string query)
    {
        if (query.Length < 1)
            return new PlayResult(false, string.Empty, "missing query");
        var playlistId = FindFirstPlaylistId(Search(query, PlaylistsFilter));
        if (string.IsNullOrWhiteSpace(playlistId))
            return Open("https://music.youtube.com/search?q=" + Uri.EscapeDataString(query), query);
        return Open("https://music.youtube.com/playlist?list=" + Uri.EscapeDataString(playlistId), query);
    }

    private static PlayResult PlayRadio(string query)
    {
        if (query.Length < 1)
            return new PlayResult(false, string.Empty, "missing query");
        var videoId = FindFirstVideoId(Search(query, SongsFilter));
        if (string.IsNullOrWhiteSpace(videoId))
            return Open("https://music.youtube.com/search?q=" + Uri.EscapeDataString(query), query);
        return Open($"https://music.youtube.com/watch?v={videoId}&list=RDAMVM{videoId}", query);
    }

    private static PlayResult Open(string url, string title)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return new PlayResult(true, url, title);
    }

    private static JsonElement Search(string query, string? filter)
    {
        using var document = JsonDocument.Parse(SearchJson(query, filter));
        return document.RootElement.Clone();
    }

    private static string SearchJson(string query, string? filter)
    {
        var payload = new Dictionary<string, object>
        {
            ["context"] = new
            {
                client = new
                {
                    clientName = "WEB_REMIX",
                    clientVersion = "1.20241204.01.00",
                    hl = "en"
                }
            },
            ["query"] = query
        };
        if (!string.IsNullOrWhiteSpace(filter))
            payload["params"] = filter;

        using var request = new HttpRequestMessage(HttpMethod.Post, SearchEndpoint);
        request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
        request.Headers.Referrer = new Uri("https://music.youtube.com/");
        request.Headers.TryAddWithoutValidation("Origin", "https://music.youtube.com");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = Http.Send(request);
        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"YouTube Music search HTTP {(int)response.StatusCode}");
        return body;
    }

    internal static string? FindFirstVideoId(JsonElement root)
    {
        var videos = new List<string>();
        CollectIds(root, videos, playlists: null);
        return videos.Count > 0 ? videos[0] : null;
    }

    internal static string? FindFirstPlaylistId(JsonElement root)
    {
        var playlists = new List<string>();
        CollectIds(root, videos: null, playlists);
        foreach (var id in playlists)
        {
            if (id.StartsWith("PL", StringComparison.Ordinal) || id.StartsWith("OLAK", StringComparison.Ordinal))
                return id;
        }
        foreach (var id in playlists)
        {
            if (!id.StartsWith("RD", StringComparison.OrdinalIgnoreCase))
                return id;
        }
        return playlists.Count > 0 ? playlists[0] : null;
    }

    private static void CollectIds(JsonElement element, List<string>? videos, List<string>? playlists)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (videos != null
                    && element.TryGetProperty("videoId", out var video)
                    && video.ValueKind == JsonValueKind.String)
                {
                    var id = video.GetString();
                    if (id is { Length: 11 } && !videos.Contains(id))
                        videos.Add(id);
                }
                if (playlists != null
                    && element.TryGetProperty("playlistId", out var playlist)
                    && playlist.ValueKind == JsonValueKind.String)
                {
                    var id = playlist.GetString();
                    if (!string.IsNullOrWhiteSpace(id) && !playlists.Contains(id))
                        playlists.Add(id);
                }
                foreach (var property in element.EnumerateObject())
                    CollectIds(property.Value, videos, playlists);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    CollectIds(item, videos, playlists);
                break;
        }
    }
}
