using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LilithTextInjector;

internal static class ChatLaughterLimiter
{
    internal const int CooldownModelTurns = 3;

    internal const string Prompt =
        "\nLaughter: Do not use kkk, rsrs, haha, keyboard smash, or similar laughs on every message. Most replies have no laughter at all. Use kkk only when something is actually funny — a joke, a ridiculous situation, or the user is already laughing. Never start a message with kkk. Never send a message that is only kkk. Do not copy kkk from example turns just because they contain it.";

    private static readonly Regex MundaneUser = new(
        @"(?i)^\s*(?:oi+|oii+|ol[aá]|eae+|fala+|hey+|hi+|hello+|bom dia|boa tarde|boa noite|toca\b|play\b|coloca\b|passa\b|troca\b|muda\b|pr[oó]xim[ao]|pause|pausa|para(?: a m[uú]sica)?|olha\b|look\b|me v[eê]|draft\b|pick\b|ban\b|timer\b)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool AllowsLaughter(string? userText, IReadOnlyList<string>? recentModelReplies)
    {
        if (SpeechTextSanitizer.ContainsLaughter(userText))
            return true;
        if (LooksMundane(userText))
            return false;
        return !RecentModelLaughed(recentModelReplies);
    }

    internal static string Apply(string reply, string? userText, IReadOnlyList<string>? recentModelReplies)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return reply ?? string.Empty;

        var capped = SpeechTextSanitizer.CapLaughter(reply);
        if (!SpeechTextSanitizer.ContainsLaughter(capped))
            return capped;
        if (AllowsLaughter(userText, recentModelReplies))
            return capped;

        var stripped = SpeechTextSanitizer.StripLaughter(capped);
        return string.IsNullOrWhiteSpace(stripped) ? "nossa" : stripped;
    }

    internal static bool LooksMundane(string? userText)
        => !string.IsNullOrWhiteSpace(userText) && MundaneUser.IsMatch(userText);

    internal static bool RecentModelLaughed(IReadOnlyList<string>? recentModelReplies)
    {
        if (recentModelReplies == null || recentModelReplies.Count == 0)
            return false;
        var start = Math.Max(0, recentModelReplies.Count - CooldownModelTurns);
        for (var i = start; i < recentModelReplies.Count; i++)
        {
            if (SpeechTextSanitizer.ContainsLaughter(recentModelReplies[i]))
                return true;
        }
        return false;
    }
}
