using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LilithTextInjector;

internal static class YouTubeMusicPlayback
{
    private const string SearchEndpoint = "https://music.youtube.com/youtubei/v1/search?prettyPrint=false";
    private const string SongsFilter = "EgWKAQIIAWoKEAkQBRAKEAMQBA%3D%3D";
    private const string VideosFilter = "EgWKAQIQAWoKEAkQBRAKEAMQBA%3D%3D";
    private const string PlaylistsFilter = "EgWKAQIoAWoKEAkQBRAKEAMQBA%3D%3D";
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    internal readonly record struct PlayResult(bool Success, string Url, string Title);
    internal readonly record struct SearchHit(string VideoId, string Title);

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
        var hit = FindBestVideo(query);
        if (hit == null)
            return Open("https://music.youtube.com/search?q=" + Uri.EscapeDataString(query), query);
        return Open("https://music.youtube.com/watch?v=" + hit.Value.VideoId, hit.Value.Title);
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
        var hit = FindBestVideo(query);
        if (hit == null)
            return Open("https://music.youtube.com/search?q=" + Uri.EscapeDataString(query), query);
        return Open($"https://music.youtube.com/watch?v={hit.Value.VideoId}&list=RDAMVM{hit.Value.VideoId}", hit.Value.Title);
    }

    private static SearchHit? FindBestVideo(string query)
    {
        var hits = new List<SearchHit>();
        foreach (var filter in new[] { SongsFilter, VideosFilter, null })
        {
            try
            {
                CollectHits(Search(query, filter), hits);
            }
            catch
            {
            }
        }
        return PickBestHit(query, hits);
    }

    internal static string WithAutoplay(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;
        if (url.Contains("autoplay=", StringComparison.OrdinalIgnoreCase))
            return url;
        return url.Contains('?', StringComparison.Ordinal) ? url + "&autoplay=1" : url + "?autoplay=1";
    }

    private static PlayResult Open(string url, string title)
    {
        var playUrl = WithAutoplay(url);
        Process.Start(new ProcessStartInfo(playUrl) { UseShellExecute = true });
        return new PlayResult(true, playUrl, title);
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
        var hits = new List<SearchHit>();
        CollectHits(root, hits);
        return hits.Count > 0 ? hits[0].VideoId : null;
    }

    internal static SearchHit? PickBestHit(string query, IReadOnlyList<SearchHit> hits)
    {
        SearchHit? best = null;
        var bestScore = 0;
        foreach (var hit in hits)
        {
            if (string.IsNullOrWhiteSpace(hit.VideoId))
                continue;
            var score = ScoreTitle(query, hit.Title);
            if (score <= bestScore)
                continue;
            bestScore = score;
            best = hit;
        }
        return bestScore >= 200 ? best : hits.Count > 0 ? hits[0] : null;
    }

    internal static int ScoreTitle(string query, string title)
    {
        var q = NormalizeMusicText(query);
        var t = NormalizeMusicText(title);
        if (q.Length == 0 || t.Length == 0)
            return 0;
        if (t.Contains(q, StringComparison.Ordinal) || q.Contains(t, StringComparison.Ordinal))
            return 1000;
        var tokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 2 && token is not "ft" and not "feat" and not "the")
            .ToArray();
        if (tokens.Length == 0)
            return 0;
        var hits = tokens.Count(token => t.Contains(token, StringComparison.Ordinal));
        var score = hits * 100;
        if (hits == tokens.Length)
            score += 250;
        if (t.Contains("karaoke", StringComparison.Ordinal) && !q.Contains("karaoke", StringComparison.Ordinal))
            score -= 80;
        return score;
    }

    internal static string NormalizeMusicText(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        }
        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
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

    private static void CollectHits(JsonElement element, List<SearchHit> hits)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("musicResponsiveListItemRenderer", out var item))
                {
                    var videoId = FindVideoId(item);
                    var title = FindItemTitle(item);
                    if (!string.IsNullOrWhiteSpace(videoId)
                        && hits.TrueForAll(hit => !string.Equals(hit.VideoId, videoId, StringComparison.Ordinal)))
                    {
                        hits.Add(new SearchHit(videoId, title));
                    }
                }
                foreach (var property in element.EnumerateObject())
                    CollectHits(property.Value, hits);
                break;
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                    CollectHits(child, hits);
                break;
        }
    }

    private static string? FindVideoId(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("videoId", out var video)
                && video.ValueKind == JsonValueKind.String)
            {
                var id = video.GetString();
                if (id is { Length: 11 })
                    return id;
            }
            foreach (var property in element.EnumerateObject())
            {
                var nested = FindVideoId(property.Value);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                var nested = FindVideoId(child);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }
        }
        return null;
    }

    private static string FindItemTitle(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("runs", out var runs)
            && runs.ValueKind == JsonValueKind.Array)
        {
            var builder = new StringBuilder();
            foreach (var run in runs.EnumerateArray())
            {
                if (run.ValueKind == JsonValueKind.Object
                    && run.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String)
                    builder.Append(text.GetString());
            }
            var title = builder.ToString().Trim();
            if (title.Length > 0
                && !string.Equals(title, "Criar mix", StringComparison.OrdinalIgnoreCase)
                && !title.StartsWith("Tocar", StringComparison.OrdinalIgnoreCase)
                && !title.StartsWith("Play", StringComparison.OrdinalIgnoreCase))
                return title;
        }
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var nested = FindItemTitle(property.Value);
                if (nested.Length > 0)
                    return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                var nested = FindItemTitle(child);
                if (nested.Length > 0)
                    return nested;
            }
        }
        return string.Empty;
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

    internal static bool UserAskedToPlayOrChangeMusic(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        if (UserAskedToChangeMusic(text))
            return true;
        return Regex.IsMatch(
            text,
            @"\b(?:play|playing|put on|queue|toca(?:r|e)?|p[oõ]e|coloca(?:r)?|bota(?:r)?|quero\s+(?:ouvir|escutar)|播放|播一首|聽|听)\b"
            + @"|\b(?:youtube\s*music|yt\s*music|youtubemusic)\b"
            + @"|liked\s+(?:songs|music)|我喜歡的歌|我喜欢的歌"
            + @"|播放清單|播放列表|プレイリスト",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    internal static bool UserAskedToChangeMusic(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        return Regex.IsMatch(
            text,
            @"\b(?:change|another|different|instead|next)\b.{0,24}\b(?:song|track|playlist|music|lo[- ]?fi)\b"
            + @"|\b(?:troca(?:r)?|muda(?:r)?|outra|pr[oó]xima)\b.{0,16}\b(?:m[uú]sica|musica|playlist|faixa|lo[- ]?fi)\b"
            + @"|\b(?:something else|another one|stop this|not this|para essa|n[aã]o essa|troca essa|toca outra)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    internal static bool LooksLikeGenericLofiQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;
        return Regex.IsMatch(
            query,
            @"\b(?:lo[- ]?fi|chill(?:hop| beats)?|study beats|relax(?:ing)? beats)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
