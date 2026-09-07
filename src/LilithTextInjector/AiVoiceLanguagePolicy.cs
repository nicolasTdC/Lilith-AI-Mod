namespace LilithTextInjector;

internal static class AiVoiceLanguagePolicy
{
    internal const string Chinese = "zh";
    internal const string Japanese = "ja";
    internal const string English = "en";

    internal readonly record struct TtsLanguages(string TextLang, string PromptLang, bool UseJapaneseService);

    internal static TtsLanguages Resolve(bool japaneseVoiceMode, bool englishInterface, string speechText)
    {
        if (japaneseVoiceMode)
            return new TtsLanguages(Japanese, Japanese, UseJapaneseService: true);
        if (LooksLikeEnglish(speechText) || (englishInterface && !LooksLikeCjk(speechText)))
            return new TtsLanguages(English, Chinese, UseJapaneseService: false);
        return new TtsLanguages(Chinese, Chinese, UseJapaneseService: false);
    }

    internal static bool LooksLikeEnglish(string? text)
    {
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
