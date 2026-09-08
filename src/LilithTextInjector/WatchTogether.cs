using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LilithTextInjector;

internal sealed class WatchTogetherState
{
    public string Title { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal static class WatchTogether
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(4);

    private static readonly Regex WatchCue = new(
        @"(?i)\b(?:netflix|prime\s*video|disney\s*\+?|hbo|max|assistir|assisti|watching|watch\s+together|dorama|k[- ]?drama|série|serie|temporada|epis[oó]dio|\bep(?:isodio)?\s*\d+|bota(?:r)?\s+(?:a[ií]|no|o)|vamos\s+ver|quero\s+ver|sinopse|synopsis|recap)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StopCue = new(
        @"(?i)\b(?:parei\s+de\s+assistir|stop\s+watching|deslig(?:uei|a)\s+(?:a\s+)?netflix|n[aã]o\s+quero\s+mais\s+ver|cansei\s+de\s+assistir|fechou\s+o\s+filme)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NetflixCue = new(
        @"(?i)\bnetflix\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EpisodeCue = new(
        @"(?i)(?:epis[oó]dio|episode|\bep(?:isode)?\.?)\s*(?:n[uú]mero\s*)?(?<num>\d{1,3}|um|uma|dois|duas|tr[eê]s|tres|quatro|cinco|seis|sete|oito|nove|dez)|(?:bota(?:r)?|colocar|come[cç]ar(?:\s+pelo)?|no|pelo)\s+(?:o\s+)?(?<num2>\d{1,3}|tr[eê]s|tres|dois|quatro)\b|s(?<season>\d{1,2})e(?<ep>\d{1,3})",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SeasonCue = new(
        @"(?i)(?:temporada|season)\s*(?<num>\d{1,2}|um|uma|dois|duas|tr[eê]s|tres)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TitleCue = new(
        @"(?i)(?:assistir|watching|ver(?:\s+o|\s+a)?|bota(?:r)?)\s+(?:o\s+|a\s+|um\s+|uma\s+)?[""“](?<title>[^""”]{2,80})[""”]|(?:assistir|watching)\s+(?:o\s+|a\s+)?(?<title2>(?!netflix|dorama|filme|s[eé]rie|serie|terror|call|msc|musica)[A-Za-zÀ-ÿ0-9][A-Za-zÀ-ÿ0-9 '&:]{2,50}?)(?:\s+na\s+netflix|\s+ep|\s+epis|\?|$)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static string SubtitlesDirectory { get; set; } = string.Empty;

    internal static WatchTogetherState? Active { get; private set; }

    internal static bool IsActive
        => Active != null && DateTimeOffset.UtcNow - Active.UpdatedAt < SessionTtl;

    internal static bool LooksLikeWatchTurn(string? text)
        => !string.IsNullOrWhiteSpace(text) && WatchCue.IsMatch(text);

    internal static bool ShouldSearch(string? userText)
        => IsActive || LooksLikeWatchTurn(userText);

    internal static WatchTogetherState? Observe(string userText, IReadOnlyList<string>? recentTurns = null)
    {
        if (string.IsNullOrWhiteSpace(userText))
            return Active;

        if (StopCue.IsMatch(userText))
        {
            Active = null;
            return null;
        }

        if (Active != null && DateTimeOffset.UtcNow - Active.UpdatedAt >= SessionTtl)
            Active = null;

        var blob = userText;
        if (recentTurns != null)
        {
            for (var i = Math.Max(0, recentTurns.Count - 8); i < recentTurns.Count; i++)
                blob += "\n" + recentTurns[i];
        }

        var watching = LooksLikeWatchTurn(userText) || Active != null;
        if (!watching)
            return Active;

        var episode = ParseEpisode(userText) ?? (Active == null ? ParseEpisode(blob) : null);
        var season = ParseSeason(userText) ?? (Active == null ? ParseSeason(blob) : null);
        var title = ParseTitle(userText) ?? Active?.Title ?? ParseTitle(blob);
        var platform = NetflixCue.IsMatch(userText) || NetflixCue.IsMatch(blob)
            ? "Netflix"
            : Active?.Platform ?? string.Empty;

        Active = new WatchTogetherState
        {
            Title = title ?? Active?.Title ?? string.Empty,
            Platform = platform,
            Season = season ?? Active?.Season,
            Episode = episode ?? Active?.Episode,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        return Active;
    }

    internal static string BuildPrompt()
    {
        if (!IsActive || Active == null)
            return string.Empty;

        var label = FormatLabel(Active);
        var query = BuildSearchQuery(Active);
        return
            "\nWATCHING TOGETHER: You are watching " + label + " with the user"
            + (Active.Platform.Length > 0 ? " on " + Active.Platform : string.Empty)
            + ". You must use the web-search tool this turn for a public recap, synopsis, reviews, and episode discussion of exactly this installment. Search queries like: \""
            + query
            + "\". Prefer Wikipedia, IMDb, fandom wikis, and episode discussion threads. "
            + "Do not download subtitle files from the web. Public recaps and commonly quoted lines are enough unless a local subtitle file was provided. "
            + "If the title is unknown, search using distinctive plot details from the recent recap. "
            + "Do not spoil later episodes. Stay in character: short informal chat, no lecture, no wiki dump. React to the scene they are on."
            + " If they change episode, search the new one."
            + BuildLocalSubtitlePrompt(Active);
    }

    internal static string BuildLocalSubtitlePrompt(WatchTogetherState state)
    {
        var excerpt = LoadMatchingSubtitleExcerpt(state, SubtitlesDirectory);
        if (string.IsNullOrWhiteSpace(excerpt))
            return string.Empty;
        return "\nLocal dialogue notes for this episode (follow along; do not recite long stretches):\n" + excerpt;
    }

    internal static string LoadMatchingSubtitleExcerpt(WatchTogetherState state, string? directory, int maxChars = 5000)
    {
        var path = FindMatchingSubtitle(state, directory);
        if (path == null)
            return string.Empty;
        try
        {
            return SummarizeSrt(File.ReadAllText(path), maxChars);
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static string? FindMatchingSubtitle(WatchTogetherState state, string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;
        string[] files;
        try
        {
            files = Directory.GetFiles(directory, "*.srt");
        }
        catch
        {
            return null;
        }
        if (files.Length == 0)
            return null;
        if (files.Length == 1)
            return files[0];

        var episode = state.Episode;
        var title = (state.Title ?? string.Empty).Trim();
        string? best = null;
        var bestScore = 0;
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var folded = name.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ');
            var score = 0;
            if (episode is int ep && Regex.IsMatch(name, $@"(?:e|ep|episode|epis[oó]dio)[\s._-]*0*{ep}\b", RegexOptions.IgnoreCase))
                score += 3;
            if (episode is int ep2 && Regex.IsMatch(name, $@"\bs\d{{1,2}}e0*{ep2}\b", RegexOptions.IgnoreCase))
                score += 4;
            if (title.Length >= 3 && folded.IndexOf(title.Replace('.', ' '), StringComparison.OrdinalIgnoreCase) >= 0)
                score += 5;
            else if (title.Length >= 3)
            {
                foreach (var token in Regex.Split(title, @"\W+"))
                {
                    if (token.Length >= 4 && name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                        score += 1;
                }
            }
            if (score > bestScore)
            {
                bestScore = score;
                best = file;
            }
        }
        return bestScore > 0 ? best : files[0];
    }

    internal static string SummarizeSrt(string raw, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        var lines = new List<string>();
        foreach (var line in raw.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;
            if (Regex.IsMatch(trimmed, @"^\d+$"))
                continue;
            if (trimmed.Contains("-->"))
                continue;
            trimmed = Regex.Replace(trimmed, "<[^>]+>", string.Empty).Trim();
            if (trimmed.Length == 0)
                continue;
            lines.Add(trimmed);
        }
        if (lines.Count == 0)
            return string.Empty;
        var joined = string.Join(" / ", lines);
        if (joined.Length <= maxChars)
            return joined;
        return joined.Substring(0, maxChars).TrimEnd() + "…";
    }

    internal static string FormatLabel(WatchTogetherState state)
    {
        var title = string.IsNullOrWhiteSpace(state.Title) ? "the show you are watching" : state.Title.Trim();
        if (state.Season is int season && state.Episode is int episode)
            return $"{title} S{season:00}E{episode:00}";
        if (state.Episode is int onlyEpisode)
            return $"{title} episode {onlyEpisode}";
        return title;
    }

    internal static string BuildSearchQuery(WatchTogetherState state)
    {
        var title = string.IsNullOrWhiteSpace(state.Title) ? "tv episode" : state.Title.Trim();
        var parts = new List<string> { title };
        if (state.Season is int season)
            parts.Add("season " + season.ToString(CultureInfo.InvariantCulture));
        if (state.Episode is int episode)
            parts.Add("episode " + episode.ToString(CultureInfo.InvariantCulture));
        parts.Add("recap synopsis reviews discussion");
        return string.Join(" ", parts);
    }

    internal static int? ParseEpisode(string text)
    {
        var match = EpisodeCue.Match(text);
        if (!match.Success)
            return null;
        if (match.Groups["ep"].Success)
            return ParseNumber(match.Groups["ep"].Value);
        if (match.Groups["num"].Success)
            return ParseNumber(match.Groups["num"].Value);
        if (match.Groups["num2"].Success)
            return ParseNumber(match.Groups["num2"].Value);
        return null;
    }

    internal static int? ParseSeason(string text)
    {
        var match = SeasonCue.Match(text);
        if (match.Success)
            return ParseNumber(match.Groups["num"].Value);
        var se = EpisodeCue.Match(text);
        if (se.Success && se.Groups["season"].Success)
            return ParseNumber(se.Groups["season"].Value);
        return null;
    }

    internal static string? ParseTitle(string text)
    {
        var match = TitleCue.Match(text);
        if (!match.Success)
            return null;
        var title = match.Groups["title"].Success ? match.Groups["title"].Value : match.Groups["title2"].Value;
        title = title.Trim(" .!?".ToCharArray());
        if (title.Length < 3 || title.Length > 80)
            return null;
        if (Regex.IsMatch(title, "(?i)^(netflix|dorama|filme|terror|call|msc|musica|música)$"))
            return null;
        return title;
    }

    internal static void Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Active = null;
                return;
            }
            var loaded = JsonSerializer.Deserialize<WatchTogetherState>(File.ReadAllText(path));
            if (loaded == null || DateTimeOffset.UtcNow - loaded.UpdatedAt >= SessionTtl)
            {
                Active = null;
                return;
            }
            Active = loaded;
        }
        catch
        {
            Active = null;
        }
    }

    internal static void Save(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            if (Active == null)
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }
            File.WriteAllText(path, JsonSerializer.Serialize(Active, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }

    internal static void Reset() => Active = null;

    private static int? ParseNumber(string raw)
    {
        raw = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            return value;
        return raw switch
        {
            "um" or "uma" => 1,
            "dois" or "duas" => 2,
            "tres" or "três" => 3,
            "quatro" => 4,
            "cinco" => 5,
            "seis" => 6,
            "sete" => 7,
            "oito" => 8,
            "nove" => 9,
            "dez" => 10,
            _ => null
        };
    }
}
