using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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

    internal readonly record struct PlayResult(bool Success, string Url, string Title, bool UsedPearDesktop = false, string VideoId = "");
    internal readonly record struct SearchHit(string VideoId, string Title, string Artist = "");

    internal static readonly string[] PearDesktopProcessNames =
    {
        "YouTube Music",
        "youtube-music",
        "youtube-music-desktop-app",
        "YouTube Music Desktop App",
        "pear-desktop",
        "Pear Desktop"
    };

    internal static bool LastOpenUsedPearDesktop { get; private set; }

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
        return Open("https://music.youtube.com/watch?v=" + hit.Value.VideoId, DisplayTitle(hit.Value));
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
        return Open($"https://music.youtube.com/watch?v={hit.Value.VideoId}&list=RDAMVM{hit.Value.VideoId}", DisplayTitle(hit.Value));
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
        LastOpenUsedPearDesktop = false;
        try
        {
            LastOpenUsedPearDesktop = TryOpenInPearDesktop(playUrl);
        }
        catch
        {
            LastOpenUsedPearDesktop = false;
        }
        if (!LastOpenUsedPearDesktop)
            OpenInDefaultBrowser(playUrl);
        return new PlayResult(true, playUrl, title, LastOpenUsedPearDesktop, VideoIdFromUrl(playUrl) ?? string.Empty);
    }

    internal static string? VideoIdFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        var match = Regex.Match(url, @"[?&]v=([\w-]{11})");
        return match.Success ? match.Groups[1].Value : null;
    }

    internal static string PearProtocolUri(string scheme, string command)
    {
        scheme = string.IsNullOrWhiteSpace(scheme) ? "youtubemusic" : scheme.Trim();
        command = (command ?? string.Empty).Trim();
        return scheme + "://" + command.Replace(" ", "%20");
    }

    internal static bool TrySendPearCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;
        var sent = false;
        foreach (var scheme in new[] { "youtubemusic", "peardesktop" })
        {
            try
            {
                var uri = PearProtocolUri(scheme, command);
                Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
                sent = true;
            }
            catch
            {
            }
        }
        return sent;
    }

    internal static void OpenInDefaultBrowser(string playUrl)
    {
        Process.Start(new ProcessStartInfo(playUrl) { UseShellExecute = true });
    }

    internal static bool LooksLikePearDesktopProcess(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return false;
        var name = Path.GetFileNameWithoutExtension(processName.Trim());
        return PearDesktopProcessNames.Any(candidate =>
            string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool LooksLikePearDesktopShortcut(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return false;
        var name = Path.GetFileNameWithoutExtension(displayName.Trim());
        return Regex.IsMatch(
            name,
            @"pear\s*desktop|youtube\s*music(?:\s*desktop(?:\s*app)?)?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    internal static bool TryOpenInPearDesktop(string playUrl)
    {
        var target = FindPearDesktopTarget();
        if (string.IsNullOrWhiteSpace(target))
            return false;
        try
        {
            var start = new ProcessStartInfo(target)
            {
                UseShellExecute = true
            };
            if (string.Equals(Path.GetExtension(target), ".exe", StringComparison.OrdinalIgnoreCase))
                start.Arguments = QuoteArgument(playUrl);
            Process.Start(start);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static string? FindPearDesktopTarget()
    {
        foreach (var name in PearDesktopProcessNames)
        {
            Process[] processes;
            try { processes = Process.GetProcessesByName(name); }
            catch { continue; }
            foreach (var process in processes)
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        return path;
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        foreach (var path in EnumeratePearDesktopInstallPaths())
        {
            if (File.Exists(path))
                return path;
        }

        return FindPearDesktopShortcut();
    }

    internal static IEnumerable<string> EnumeratePearDesktopInstallPaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programsX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        foreach (var root in new[] { local, programs, programsX86 })
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;
            yield return Path.Combine(root, "Programs", "youtube-music", "YouTube Music.exe");
            yield return Path.Combine(root, "Programs", "YouTube Music", "YouTube Music.exe");
            yield return Path.Combine(root, "Programs", "pear-desktop", "YouTube Music.exe");
            yield return Path.Combine(root, "Programs", "pear-desktop", "Pear Desktop.exe");
            yield return Path.Combine(root, "youtube-music-desktop-app", "YouTube Music.exe");
            yield return Path.Combine(root, "youtube-music", "YouTube Music.exe");
            yield return Path.Combine(root, "pear-desktop", "YouTube Music.exe");
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            yield return Path.Combine(userProfile, "scoop", "apps", "pear-desktop", "current", "YouTube Music.exe");
            yield return Path.Combine(userProfile, "scoop", "apps", "youtube-music", "current", "YouTube Music.exe");
        }
    }

    private static string? FindPearDesktopShortcut()
    {
        foreach (var root in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        })
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                continue;
            string[] shortcuts;
            try { shortcuts = Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories); }
            catch { continue; }
            foreach (var shortcut in shortcuts)
            {
                if (LooksLikePearDesktopShortcut(Path.GetFileNameWithoutExtension(shortcut)))
                    return shortcut;
            }
        }
        return null;
    }

    internal static string QuoteArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";
        if (!value.Contains(' ') && !value.Contains('"') && !value.Contains('\t'))
            return value;
        return "\"" + value.Replace("\"", "\\\"") + "\"";
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
            var score = ScoreTitle(query, hit.Title, hit.Artist);
            if (score <= bestScore)
                continue;
            bestScore = score;
            best = hit;
        }
        return bestScore >= 200 ? best : hits.Count > 0 ? hits[0] : null;
    }

    internal static int ScoreTitle(string query, string title, string artist = "")
    {
        var q = NormalizeMusicText(query);
        var t = NormalizeMusicText(string.IsNullOrWhiteSpace(artist) ? title : title + " " + artist);
        if (q.Length == 0 || t.Length == 0)
            return 0;
        var queryTokens = MeaningfulMusicTokens(q);
        var titleTokens = MeaningfulMusicTokens(t);
        if (queryTokens.Length == 0)
            return 0;
        if (t.Contains(q, StringComparison.Ordinal) && queryTokens.Length >= 2)
            return 1000;
        if (q.Contains(t, StringComparison.Ordinal) && titleTokens.Length >= queryTokens.Length)
            return 1000;
        var hits = queryTokens.Count(token => t.Contains(token, StringComparison.Ordinal));
        var score = hits * 100;
        if (hits == queryTokens.Length)
            score += 250;
        if (t.Contains("karaoke", StringComparison.Ordinal) && !q.Contains("karaoke", StringComparison.Ordinal))
            score -= 80;
        return score;
    }

    internal static string[] MeaningfulMusicTokens(string normalized)
    {
        return (normalized ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 2
                && token is not "ft" and not "feat" and not "the" and not "by"
                    and not "do" and not "da" and not "de" and not "of" and not "and")
            .ToArray();
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
                    var (title, artist) = FindItemTitleAndArtist(item);
                    if (!string.IsNullOrWhiteSpace(videoId)
                        && hits.TrueForAll(hit => !string.Equals(hit.VideoId, videoId, StringComparison.Ordinal)))
                    {
                        hits.Add(new SearchHit(videoId, title, artist));
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

    private static (string Title, string Artist) FindItemTitleAndArtist(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.Object
            && item.TryGetProperty("flexColumns", out var columns)
            && columns.ValueKind == JsonValueKind.Array)
        {
            var texts = new List<string>();
            foreach (var column in columns.EnumerateArray())
            {
                var text = FindItemTitle(column);
                if (text.Length > 0)
                    texts.Add(text);
            }
            var title = texts.Count > 0 ? texts[0] : FindItemTitle(item);
            var artist = texts.Count > 1 ? texts[1] : string.Empty;
            var cut = Regex.Split(artist, @"\s*[•·|]\s*");
            if (cut.Length > 0)
                artist = cut[0].Trim();
            return (title, artist);
        }
        return (FindItemTitle(item), string.Empty);
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

    internal static string DisplayTitle(SearchHit hit)
    {
        if (string.IsNullOrWhiteSpace(hit.Artist) || NormalizeMusicText(hit.Title).Contains(NormalizeMusicText(hit.Artist)))
            return hit.Title;
        return hit.Title + " - " + hit.Artist;
    }

    internal static string EnsureSongAndArtistQuery(string query, string artist)
    {
        return FormatSearchQuery(query, artist);
    }

    internal static string FormatSearchQuery(string query, string artist)
    {
        query = (query ?? string.Empty).Trim();
        artist = (artist ?? string.Empty).Trim();
        if (LooksLikeMusicUrl(query))
            query = string.Empty;
        if (artist.Length < 1)
            return query;
        if (query.Length < 1)
            return artist;
        if (NormalizeMusicText(query).Contains(NormalizeMusicText(artist)))
            return query;
        return query + " " + artist;
    }

    internal static bool HasSongAndArtist(string? text)
    {
        return MeaningfulMusicTokens(NormalizeMusicText(text ?? string.Empty)).Length >= 2;
    }

    internal static string SearchBarQuery(string intent, string title, string query, string artist = "")
    {
        intent = NormalizeIntent(intent);
        if (intent is "liked" or "library")
            return string.Empty;
        var requested = FormatSearchQuery(query, artist);
        if (HasSongAndArtist(requested))
            return requested;
        var catalog = FormatSearchQuery(LooksLikeMusicUrl(title) ? string.Empty : title, artist);
        if (HasSongAndArtist(catalog))
            return catalog;
        return requested.Length > 0 ? requested : catalog;
    }

    internal static bool LooksLikeMusicUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        return text.Contains("://", StringComparison.Ordinal)
            || text.StartsWith("music.youtube.com", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("youtube.com", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("youtu.be", StringComparison.OrdinalIgnoreCase);
    }

    internal static string NormalizeIntent(string? intent)
    {
        intent = (intent ?? "song").Trim().ToLowerInvariant();
        return intent switch
        {
            "likes" or "liked_songs" or "liked_music" => "liked",
            _ => string.IsNullOrWhiteSpace(intent) ? "song" : intent
        };
    }

    internal static bool LooksLikeSamePlayRequest(
        string intent,
        string query,
        string lastIntent,
        string lastQuery)
    {
        intent = NormalizeIntent(intent);
        lastIntent = NormalizeIntent(lastIntent);
        if (intent != lastIntent)
            return false;
        if (intent is "liked" or "library")
            return true;
        var normalized = NormalizeMusicText(query ?? string.Empty);
        var lastNormalized = NormalizeMusicText(lastQuery ?? string.Empty);
        return normalized.Length > 0
            && string.Equals(normalized, lastNormalized, StringComparison.Ordinal);
    }

    internal static bool ShouldSkipDuplicatePlay(
        string intent,
        string query,
        string lastIntent,
        string lastQuery,
        bool recentlyStarted,
        bool askedToChange)
    {
        if (!recentlyStarted)
            return false;
        if (LooksLikeSamePlayRequest(intent, query, lastIntent, lastQuery))
            return true;
        return !askedToChange
            && LooksLikeGenericLofiQuery(query)
            && LooksLikeGenericLofiQuery(lastQuery);
    }

    internal static bool UserAskedToPlayOrChangeMusic(string? text, string? previousUserText = null)
    {
        if (MatchesExplicitPlayOrChange(text))
            return true;
        return LooksLikeRetryMusicPrompt(text) && MatchesExplicitPlayOrChange(previousUserText);
    }

    internal static bool MatchesExplicitPlayOrChange(string? text)
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

    internal static bool LooksLikeRetryMusicPrompt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        return Regex.IsMatch(
            text,
            @"\b(?:tenta(?:r)?|again|retry|novamente|dnvo|nvo)\b|\bde\s+novo\b|\bmais\s+uma\s+vez\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    internal static bool UserAskedToChangeMusic(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        return Regex.IsMatch(
            text,
            @"\b(?:change|another|different|instead|next|switch)\b.{0,32}\b(?:songs?|tracks?|playlists?|music|lo[- ]?fi)\b"
            + @"|\b(?:troca(?:r)?|muda(?:r)?|outra|pr[oó]xima)\b.{0,32}\b(?:m[uú]sicas?|musicas?|playlist|faixa|lo[- ]?fi)\b"
            + @"|\b(?:m[uú]sicas?|musicas?)\b.{0,32}\b(?:troca(?:r)?|muda(?:r)?)\b"
            + @"|\b(?:something else|another one|stop this|not this|para essa|n[aã]o essa|troca essa|toca outra|coloca(?:r)? outra)\b",
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
