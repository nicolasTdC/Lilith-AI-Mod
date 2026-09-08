using LilithTextInjector;

AssertEqual("see x now", SpeechTextSanitizer.Prepare("see [x](https://example.com) now"));
AssertEqual("hello", SpeechTextSanitizer.Prepare("hello https://example.com/path"));
AssertEqual("que foto é essa", SpeechTextSanitizer.Prepare("q foto é essa kkkkkkkkk"));
AssertEqual("nem sei o que é", SpeechTextSanitizer.Prepare("KKKKKKKKKK nem sei oq é t-t"));
AssertEqual("sua foto muito hype\ntendencia", SpeechTextSanitizer.Prepare("asuhduasd sua foto mto hype asopdkaspd\ntendencia"));
AssertEqual("sim\nsó o cria da korea", SpeechTextSanitizer.Prepare("sim kkkkkk\nsó o cria da korea\napsodkpoasf"));
AssertEqual("oi gatinho", SpeechTextSanitizer.Prepare("oi gatinho"));
AssertEqual("eu te amo", SpeechTextSanitizer.Prepare("eu te amo ❤️"));
AssertEqual("te amo", SpeechTextSanitizer.Prepare("te amo s2"));
AssertEqual("te amo", SpeechTextSanitizer.Prepare("te amo S2"));
AssertEqual(string.Empty, SpeechTextSanitizer.Prepare("s2"));
AssertEqual("atapo\nkj", SpeechTextSanitizer.Prepare("atapo 🤠\nkj\naspodkaposf"));
AssertEqual("que isso", SpeechTextSanitizer.Prepare("haha que isso"));
AssertEqual("ok", SpeechTextSanitizer.Prepare("ok"));
AssertEqual("adeus", SpeechTextSanitizer.Prepare("adeus"));
AssertEqual("aspas", SpeechTextSanitizer.Prepare("aspas"));
AssertEqual("caralho", SpeechTextSanitizer.Prepare("krl"));
AssertEqual("já voltou", SpeechTextSanitizer.Prepare("já voltou"));
AssertEqual("depois pessoal simples", SpeechTextSanitizer.Prepare("depois pessoal simples"));
AssertEqual(string.Empty, SpeechTextSanitizer.Prepare("kkkkkk 😂"));
AssertEqual(string.Empty, SpeechTextSanitizer.Prepare("t-t"));
AssertEqual("I missed you", SpeechTextSanitizer.Prepare("I missed you :D uwu"));
AssertEqual("我有點想你了。", SpeechTextSanitizer.Prepare("我有點想你了。哈哈哈哈"));
AssertEqual("keep going", SpeechTextSanitizer.Prepare("keep going :smile:"));
AssertEqual("oi", SpeechTextSanitizer.Prepare("oi rsrs"));
AssertEqual("meet at 12:30", SpeechTextSanitizer.Prepare("meet at 12:30"));
AssertEqual("ok", SpeechTextSanitizer.Prepare("ok :c"));
AssertEqual("por favor relaxa", SpeechTextSanitizer.Prepare("pfvr rlx"));
AssertEqual("não sei", SpeechTextSanitizer.Prepare("n sei"));
AssertEqual("você é muito legal agora", SpeechTextSanitizer.Prepare("vc eh mt legal agr"));
AssertEqual("mesmo mesma", SpeechTextSanitizer.Prepare("msm msma"));
AssertEqual("por favor por favor por favor", SpeechTextSanitizer.Prepare("pf pfv pfvr"));
AssertEqual("por favorzinho", SpeechTextSanitizer.Prepare("pfvrzinho"));
AssertEqual("por favorzinho", SpeechTextSanitizer.Prepare("pfvzinho"));
AssertEqual("por favorzinho", SpeechTextSanitizer.Prepare("pfzinho"));
AssertEqual("por favorzinho", SpeechTextSanitizer.Prepare("pfvrzinhoooo"));
AssertEqual("agora.", SpeechTextSanitizer.Prepare("agr!!"));
AssertEqual("não.", SpeechTextSanitizer.Prepare("nao!!!"));
AssertEqual("perfeito", SpeechTextSanitizer.Prepare("perfeitooooo"));
AssertEqual("a gente", SpeechTextSanitizer.Prepare("a gnt"));
AssertEqual("tô", SpeechTextSanitizer.Prepare("to"));
AssertEqual("nicolas", SpeechTextSanitizer.Prepare("nicolas"));
AssertEqual("quando", SpeechTextSanitizer.Prepare("quando"));
AssertEqual("agora você não", SpeechTextSanitizer.Prepare("agr vc n"));
AssertEqual("porque também muito", SpeechTextSanitizer.Prepare("pq tbm mto"));
AssertEqual("hoje depois cadê", SpeechTextSanitizer.Prepare("hj dps kd"));
AssertEqual("você é demais", SpeechTextSanitizer.Prepare("vc é d+"));
AssertEqual("sim não", SpeechTextSanitizer.Prepare("s n"));
AssertEqual("sempre me quebra", SpeechTextSanitizer.Prepare("smp me quebra"));
AssertEqual("maravilhosa demais", SpeechTextSanitizer.Prepare("maravilhosa dms"));
AssertEqual("de nada", SpeechTextSanitizer.Prepare("dnd"));
AssertEqual("um dois três.", SpeechTextSanitizer.Prepare("um dois tres!!"));
AssertEqual("perfeito. da o play ai bei", SpeechTextSanitizer.Prepare("perfeitommm!!! da o play ai bae"));
AssertEqual("perfeitamente", SpeechTextSanitizer.Prepare("perfeitamente"));

var extra = Path.Combine(Path.GetTempPath(), "lilith-abreviations-test.json");
File.WriteAllText(extra, "{\"tq\":\"tá quieto\",\"n\":\"não\"}");
if (!SpeechTextSanitizer.TryLoadAbbreviations(extra, out var count, out var loadError))
    throw new InvalidOperationException(loadError ?? "failed to load extra abbreviations");
if (count < 100)
    throw new InvalidOperationException($"Expected defaults plus tq, got {count}.");
AssertEqual("tá quieto", SpeechTextSanitizer.Prepare("tq"));
File.Delete(extra);

Console.WriteLine("Lilith speech sanitize tests passed (57 assertions).");

static void AssertEqual(string expected, string actual)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
}
