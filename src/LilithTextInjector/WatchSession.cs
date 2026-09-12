using System;
using System.Text.RegularExpressions;

namespace LilithTextInjector;

internal sealed class WatchSessionState
{
    public bool Active { get; set; }
    public long WindowHandle { get; set; }
    public string WindowTitle { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset LastCaptureAt { get; set; }
    public DateTimeOffset LastCommentAt { get; set; }
    public ulong LastHash { get; set; }
    public bool HasHash { get; set; }
    public bool FirstLookPending { get; set; }
    public string? TempPath { get; set; }

    public void Reset()
    {
        Active = false;
        WindowHandle = 0;
        WindowTitle = string.Empty;
        StartedAt = default;
        EndsAt = default;
        LastCaptureAt = default;
        LastCommentAt = default;
        LastHash = 0;
        HasHash = false;
        FirstLookPending = false;
        TempPath = null;
    }
}

internal static class WatchSession
{
    internal const string GlanceMarker = "[watch-session-glance]";

    internal const int DefaultDurationMinutes = 15;
    internal const int DefaultCaptureSeconds = 8;
    internal const int DefaultCommentSeconds = 40;

    internal static WatchSessionState Current { get; } = new();

    internal static bool IsActive => Current.Active;

    private static readonly Regex StartCue = new(
        @"(?i)(?:assiste(?:r)?|ver|v[eê]|olha(?:r)?)\s+(?:isso\s+|a\s+tela\s+|minha\s+tela\s+)?comigo|"
        + @"(?i)(?:vamos|bora)\s+(?:assistir|ver|olhar)\s+juntos|"
        + @"(?i)olha(?:r)?\s+junto|"
        + @"(?i)fica\s+(?:olhando|de\s+olho)(?:\s+(?:na|a|pra|para)\s+tela)?|"
        + @"(?i)compartilha(?:r)?\s+(?:a\s+|minha\s+)?tela|"
        + @"(?i)watch(?:\s+(?:this|it|the\s+screen|my\s+screen))?\s+with\s+me|"
        + @"(?i)keep\s+looking|"
        + @"(?i)look\s+together|"
        + @"(?i)share\s+(?:my\s+|the\s+)?screen|"
        + @"(?i)start\s+watching\s+(?:the\s+|my\s+)?screen|"
        + @"(?i)一起看(?:我的)?(?:螢幕|屏幕|畫面)|"
        + @"(?i)一緒に見て",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StopCue = new(
        @"(?i)para\s+de\s+(?:olhar|ver(?:\s+a\s+tela)?|assistir\s+a\s+tela)|"
        + @"(?i)pode\s+parar\s+de\s+olhar|"
        + @"(?i)chega\s+de\s+olhar|"
        + @"(?i)stop\s+(?:watching|looking|sharing)(?:\s+(?:the\s+|my\s+)?screen)?|"
        + @"(?i)don'?t\s+look(?:\s+anymore)?|"
        + @"(?i)parei\s+de\s+assistir|"
        + @"(?i)不要再看|"
        + @"(?i)見るのをやめて",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SilenceCue = new(
        @"^(?:silence|sil[eê]ncio|nada|n\/?a|no\s*comment|\.{2,}|…+|沉默|無言|无言)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool LooksLikeStart(string? text)
        => !string.IsNullOrWhiteSpace(text) && StartCue.IsMatch(text.Trim());

    internal static bool LooksLikeStop(string? text)
        => !string.IsNullOrWhiteSpace(text) && StopCue.IsMatch(text.Trim());

    internal static bool IsGlanceTurn(string? text)
        => string.Equals(text, GlanceMarker, StringComparison.Ordinal);

    internal static bool IsSilentReply(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;
        var cleaned = text.Trim().Trim('"', '\'', '`', '*', '.', '!', '?', '。', '！', '？');
        return cleaned.Length == 0 || SilenceCue.IsMatch(cleaned);
    }

    internal static int ClampDurationMinutes(int minutes)
        => Math.Clamp(minutes, 1, 30);

    internal static int ClampCaptureSeconds(int seconds)
        => Math.Clamp(seconds, 2, 30);

    internal static int ClampCommentSeconds(int seconds)
        => Math.Clamp(seconds, 15, 180);

    internal static bool IsExpired(DateTimeOffset now)
        => Current.Active && now >= Current.EndsAt;

    internal static bool ShouldCapture(DateTimeOffset now, int captureSeconds)
    {
        if (!Current.Active)
            return false;
        if (Current.FirstLookPending)
            return true;
        var interval = TimeSpan.FromSeconds(ClampCaptureSeconds(captureSeconds));
        return now - Current.LastCaptureAt >= interval;
    }

    internal static bool ShouldComment(DateTimeOffset now, int commentSeconds, bool frameChanged)
    {
        if (!Current.Active)
            return false;
        if (Current.FirstLookPending)
            return true;
        if (!frameChanged)
            return false;
        var interval = TimeSpan.FromSeconds(ClampCommentSeconds(commentSeconds));
        return now - Current.LastCommentAt >= interval;
    }

    internal static void Begin(long windowHandle, string windowTitle, DateTimeOffset now, int durationMinutes)
    {
        Current.Active = true;
        Current.WindowHandle = windowHandle;
        Current.WindowTitle = windowTitle ?? string.Empty;
        Current.StartedAt = now;
        Current.EndsAt = now.AddMinutes(ClampDurationMinutes(durationMinutes));
        Current.LastCaptureAt = default;
        Current.LastCommentAt = default;
        Current.LastHash = 0;
        Current.HasHash = false;
        Current.FirstLookPending = true;
    }

    internal static void NoteCapture(DateTimeOffset now, ulong hash)
    {
        Current.LastCaptureAt = now;
        Current.LastHash = hash;
        Current.HasHash = true;
    }

    internal static void NoteComment(DateTimeOffset now)
    {
        Current.LastCommentAt = now;
        Current.FirstLookPending = false;
    }

    internal static void End()
        => Current.Reset();

    internal static string GlancePrompt(bool firstLook, string windowTitle, string? recapLabel)
    {
        var window = string.IsNullOrWhiteSpace(windowTitle) ? "the window they asked you to watch" : windowTitle.Trim();
        var recap = string.IsNullOrWhiteSpace(recapLabel)
            ? string.Empty
            : " Recap context: you are also watching " + recapLabel + " together.";
        var first = firstLook
            ? " This is the first look of the session. Say one short in-character line about what you see. Do not reply SILENCE on this first look."
            : " If nothing noteworthy changed, reply with exactly SILENCE and no other text.";
        return "\nWATCH SESSION: The user started an opt-in watch session. A screenshot of \""
            + window
            + "\" is attached. You are watching that window with them, not the whole desktop."
            + recap
            + " Comment only if something on that window is worth a reaction: a beat in a show, a play in a game, a funny or tense moment."
            + " One short in-character sentence maximum."
            + " Do not narrate unrelated windows, notifications, Discord, passwords, banking, or personal data."
            + " Do not recap the whole screen. Do not search the web unless they asked."
            + first;
    }

    internal static string ActiveChatPrompt(string windowTitle)
    {
        var window = string.IsNullOrWhiteSpace(windowTitle) ? "the watched window" : windowTitle.Trim();
        return "\nWATCH SESSION (active): You can see a screenshot of \""
            + window
            + "\" because the user started an opt-in watch session. Use it to answer them. Do not describe passwords, notifications, or unrelated windows. The session ends when they tell you to stop looking, the timer expires, or the PC locks.";
    }
}
