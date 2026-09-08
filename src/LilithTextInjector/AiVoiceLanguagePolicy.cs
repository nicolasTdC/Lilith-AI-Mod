using System.Text.RegularExpressions;

namespace LilithTextInjector;

internal static class AiVoiceLanguagePolicy
{
    internal const string Chinese = "zh";
    internal const string Japanese = "ja";
    internal const string English = "en";
    internal const string CutNone = "cut0";
    internal const string CutPunctuation = "cut5";

    internal readonly record struct TtsLanguages(
        string TextLang,
        string PromptLang,
        bool UseJapaneseService,
        string SplitMethod);

    internal static TtsLanguages Resolve(
        bool japaneseVoiceMode,
        bool englishInterface,
        bool portugueseInterface,
        string speechText)
    {
        if (japaneseVoiceMode)
            return new TtsLanguages(Japanese, Japanese, UseJapaneseService: true, SplitMethod: CutNone);
        // GPT-SoVITS has no Portuguese G2P. English phonemes + no split is the
        // only path that produces audio; multilingual "auto" crashes on pt.
        if (LooksLikePortuguese(speechText) || (portugueseInterface && !LooksLikeCjk(speechText)))
            return new TtsLanguages(English, Chinese, UseJapaneseService: false, SplitMethod: CutNone);
        if (LooksLikeEnglish(speechText) || (englishInterface && !LooksLikeCjk(speechText) && !LooksLikePortuguese(speechText)))
            return new TtsLanguages(English, Chinese, UseJapaneseService: false, SplitMethod: CutPunctuation);
        return new TtsLanguages(Chinese, Chinese, UseJapaneseService: false, SplitMethod: CutNone);
    }

    internal static bool LooksLikePortuguese(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        var markers = 0;
        foreach (var ch in text)
        {
            if ("ãõâêôçáéíóúàÃÕÂÊÔÇÁÉÍÓÚÀ".IndexOf(ch) >= 0)
                markers++;
        }
        if (markers >= 2)
            return true;
        if (markers >= 1 && text.Length >= 12)
            return true;
        return Regex.IsMatch(
            text,
            @"\b(não|nao|você|voce|vocês|voces|então|entao|também|tambem|hoje|amanhã|amanha|comigo|contigo|pra|pro)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    internal static bool LooksLikeEnglish(string? text)
    {
        if (LooksLikePortuguese(text))
            return false;
        CountLetters(text, out var letters, out var latin, out var cjk);
        return letters >= 8 && latin * 20 >= letters * 14 && cjk * 20 < letters * 3;
    }

    internal static bool LooksLikeCjk(string? text)
    {
        CountLetters(text, out var letters, out _, out var cjk);
        return letters >= 2 && cjk * 2 >= letters;
    }

    private static void CountLetters(string? text, out int letters, out int latin, out int cjk)
    {
        letters = 0;
        latin = 0;
        cjk = 0;
        if (string.IsNullOrEmpty(text))
            return;

        foreach (var ch in text)
        {
            if (!char.IsLetter(ch))
                continue;
            letters++;
            if (ch <= 0x024F)
                latin++;
            else if (IsCjkLetter(ch))
                cjk++;
        }
    }

    private static bool IsCjkLetter(char ch)
        => ch is (>= '\u3040' and <= '\u30FF')
            or (>= '\u3400' and <= '\u4DBF')
            or (>= '\u4E00' and <= '\u9FFF')
            or (>= '\uF900' and <= '\uFAFF');
}
