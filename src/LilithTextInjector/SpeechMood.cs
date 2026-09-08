using System;
using System.Text.RegularExpressions;

namespace LilithTextInjector;

internal static class SpeechMood
{
    internal const string Calm = "calm";
    internal const string Excited = "excited";
    internal const string Sad = "sad";
    internal const string Sleepy = "sleepy";

    private static readonly Regex ExcitedCue = new(
        @"(?i)\b(?:feliz|amei|adoro|bora|hype|maravilh|perfeit|quero+|ai{3,}|eba|uhul|d\+|dms|te amo|amo vc|s2|kk+|incr[ií]vel)\b|!{2,}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HardSadCue = new(
        @"(?i)\b(?:triste|saudade|sdds|chorei|chora|sinto muito|desculpa|sozinha|fr[aá]gil|m[aá]goa|d[oó]i|n[aã]o aguento|to mal|to triste)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SoftSadCue = new(
        @"(?i)(?:t-t|:c)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SleepyCue = new(
        @"(?i)\b(?:sono|cansad[ao]|boa noite|to indo dormir|sonolenta|pregui[cç]a)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static string Resolve(string? text, string? poseStyle)
    {
        var raw = text ?? string.Empty;
        var pose = (poseStyle ?? string.Empty).Trim().ToLowerInvariant();
        var excited = Count(ExcitedCue, raw);
        var hardSad = Count(HardSadCue, raw);
        var softSad = Count(SoftSadCue, raw);
        // Faces like t-t are cute in hype lines; ignore them when the reply is already excited.
        var sad = hardSad + (excited > 0 ? 0 : softSad);
        var sleepy = Count(SleepyCue, raw);
        var sleepyPose = pose is "sleepy" or "sleep";

        if (sleepyPose && excited == 0 && hardSad == 0)
            return Sleepy;
        if (excited >= sad && excited > 0)
            return Excited;
        if (sad > 0)
            return Sad;
        if (sleepy > 0 || sleepyPose)
            return Sleepy;
        if (pose is "wronged" or "sad")
            return Sad;
        if (pose is "excited")
            return Excited;
        return Calm;
    }

    private static int Count(Regex cue, string text)
        => cue.Matches(text).Count;
}
