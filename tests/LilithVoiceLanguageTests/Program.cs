using LilithTextInjector;

var japanese = AiVoiceLanguagePolicy.Resolve(true, true, false, "I missed you.");
Assert(japanese.TextLang == AiVoiceLanguagePolicy.Japanese, "Japanese voice mode must keep Japanese TTS.");
Assert(japanese.PromptLang == AiVoiceLanguagePolicy.Japanese, "Japanese voice mode must keep a Japanese prompt.");
Assert(japanese.UseJapaneseService, "Japanese voice mode must use the Japanese voice service.");

var englishUi = AiVoiceLanguagePolicy.Resolve(false, true, false, "I missed you.");
Assert(englishUi.TextLang == AiVoiceLanguagePolicy.English, "English UI must request English TTS.");
Assert(englishUi.PromptLang == AiVoiceLanguagePolicy.Chinese, "English TTS must keep the Chinese reference prompt.");
Assert(!englishUi.UseJapaneseService, "English TTS must use the Chinese voice service.");

var shortEnglishUi = AiVoiceLanguagePolicy.Resolve(false, true, false, "OK.");
Assert(shortEnglishUi.TextLang == AiVoiceLanguagePolicy.English, "Short English UI replies must still use English TTS.");

var chineseOnEnglishUi = AiVoiceLanguagePolicy.Resolve(false, true, false, "我有點想你了。");
Assert(chineseOnEnglishUi.TextLang == AiVoiceLanguagePolicy.Chinese, "CJK replies on English UI must stay on Chinese TTS.");

var englishOnChineseUi = AiVoiceLanguagePolicy.Resolve(false, false, false, "I missed you a little.");
Assert(englishOnChineseUi.TextLang == AiVoiceLanguagePolicy.English, "English replies must use English TTS even if the UI is Chinese.");

var chineseUi = AiVoiceLanguagePolicy.Resolve(false, false, false, "我有點想你了。");
Assert(chineseUi.TextLang == AiVoiceLanguagePolicy.Chinese, "Chinese replies must keep Chinese TTS.");
Assert(!chineseUi.UseJapaneseService, "Chinese TTS must use the Chinese voice service.");

var portuguese = AiVoiceLanguagePolicy.Resolve(false, true, true, "Eu senti a sua falta.");
Assert(portuguese.TextLang == AiVoiceLanguagePolicy.English, "Portuguese must use English G2P because SoVITS has no pt mode.");
Assert(portuguese.SplitMethod == AiVoiceLanguagePolicy.CutNone, "Portuguese must not be comma-split.");
Assert(portuguese.PromptLang == AiVoiceLanguagePolicy.Chinese, "Portuguese TTS must keep the Chinese reference prompt.");

var portugueseText = AiVoiceLanguagePolicy.Resolve(false, true, false, "Você não precisa dizer nada agora.");
Assert(portugueseText.TextLang == AiVoiceLanguagePolicy.English, "Portuguese replies must still use English G2P.");
Assert(portugueseText.SplitMethod == AiVoiceLanguagePolicy.CutNone, "Portuguese replies must stay unsplit.");

Console.WriteLine("Lilith voice language tests passed (12 assertions).");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
