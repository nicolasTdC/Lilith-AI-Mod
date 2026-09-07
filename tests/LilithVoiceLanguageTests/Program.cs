using LilithTextInjector;

var japanese = AiVoiceLanguagePolicy.Resolve(true, true, "I missed you.");
Assert(japanese.TextLang == AiVoiceLanguagePolicy.Japanese, "Japanese voice mode must keep Japanese TTS.");
Assert(japanese.PromptLang == AiVoiceLanguagePolicy.Japanese, "Japanese voice mode must keep a Japanese prompt.");
Assert(japanese.UseJapaneseService, "Japanese voice mode must use the Japanese voice service.");

var englishUi = AiVoiceLanguagePolicy.Resolve(false, true, "I missed you.");
Assert(englishUi.TextLang == AiVoiceLanguagePolicy.English, "English UI must request English TTS.");
Assert(englishUi.PromptLang == AiVoiceLanguagePolicy.Chinese, "English TTS must keep the Chinese reference prompt.");
Assert(!englishUi.UseJapaneseService, "English TTS must use the Chinese voice service.");

var shortEnglishUi = AiVoiceLanguagePolicy.Resolve(false, true, "OK.");
Assert(shortEnglishUi.TextLang == AiVoiceLanguagePolicy.English, "Short English UI replies must still use English TTS.");

var chineseOnEnglishUi = AiVoiceLanguagePolicy.Resolve(false, true, "我有點想你了。");
Assert(chineseOnEnglishUi.TextLang == AiVoiceLanguagePolicy.Chinese, "CJK replies on English UI must stay on Chinese TTS.");

var englishOnChineseUi = AiVoiceLanguagePolicy.Resolve(false, false, "I missed you a little.");
Assert(englishOnChineseUi.TextLang == AiVoiceLanguagePolicy.English, "English replies must use English TTS even if the UI is Chinese.");

var chineseUi = AiVoiceLanguagePolicy.Resolve(false, false, "我有點想你了。");
Assert(chineseUi.TextLang == AiVoiceLanguagePolicy.Chinese, "Chinese replies must keep Chinese TTS.");
Assert(!chineseUi.UseJapaneseService, "Chinese TTS must use the Chinese voice service.");

Console.WriteLine("Lilith English voice language tests passed (7 assertions).");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
