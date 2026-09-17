using LilithTextInjector;

AssertEqual("KKKKKK", SpeechTextSanitizer.CapLaughter("KKKKKKKKKKKKKKKK"));
AssertEqual("kkkkkk", SpeechTextSanitizer.CapLaughter("kkkkkkkkkk"));
AssertEqual("KKKKKK", SpeechTextSanitizer.CapLaughter("KKKKKKKKKK"));
Assert(SpeechTextSanitizer.ContainsLaughter("sim kkkkkk"), "kkk in a reply must count as laughter.");
Assert(!SpeechTextSanitizer.ContainsLaughter("ok"), "ok must not count as laughter.");
AssertEqual("que foto é essa", SpeechTextSanitizer.StripLaughter("que foto é essa kkkkkkkkk"));

var funnyUser = "KKKKKKKK nem sei oq é";
AssertEqual(
    "nossa KKKKKK que isso",
    ChatLaughterLimiter.Apply("nossa KKKKKKKKKKKK que isso", funnyUser, new[] { "oi gatinho" }),
    "Keep laughter when the user is already laughing, but cap the k-run.");

AssertEqual(
    "coloquei butterfly",
    ChatLaughterLimiter.Apply("coloquei butterfly kkkkkkk", "toca butterfly da loona", Array.Empty<string>()),
    "Strip kkk from music and other mundane requests.");

AssertEqual(
    "oi gatinho",
    ChatLaughterLimiter.Apply("oi gatinho kkkkkkk", "oi", Array.Empty<string>()),
    "Strip kkk from greetings.");

AssertEqual(
    "nossa que isso",
    ChatLaughterLimiter.Apply("nossa que isso kkkkkkk", "essa foto ridicula do nada", new[] { "sim kkkkkk" }),
    "Strip kkk when she already laughed in a recent reply.");

AssertEqual(
    "nossa que isso kkkkkk",
    ChatLaughterLimiter.Apply("nossa que isso kkkkkkkkkk", "essa foto ridicula do nada", Array.Empty<string>()),
    "Keep a capped kkk when it is not a mundane request and she has not just laughed.");

AssertEqual(
    "nossa",
    ChatLaughterLimiter.Apply("KKKKKKKKKKKK", "toca a musica", Array.Empty<string>()),
    "A kkk-only reply to a mundane request must not stay on screen.");

Assert(ChatLaughterLimiter.Prompt.Contains("actually funny", StringComparison.Ordinal),
    "The system prompt must tell her to laugh only when something is actually funny.");

Console.WriteLine("Lilith chat laughter tests passed (12 assertions).");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void AssertEqual(string expected, string actual, string? message = null)
{
    if (expected != actual)
        throw new InvalidOperationException($"{message ?? "values differ"}: expected '{expected}', got '{actual}'");
}
