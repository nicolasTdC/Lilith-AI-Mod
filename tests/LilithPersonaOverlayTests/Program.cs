using LilithTextInjector;

var empty = PersonaOverlay.Load("/tmp/does-not-exist-persona-overlay");
Assert(!empty.HasAny, "Missing folders must load as empty.");
Assert(empty.ResolvePersona("fallback") == "fallback", "Empty overlay must keep the fallback persona.");
Assert(empty.ResolveStyleGuide() == null, "Empty overlay must not replace the built-in style guide.");

var dir = Path.Combine(Path.GetTempPath(), "lilith-persona-overlay-tests");
Directory.CreateDirectory(dir);
try
{
    File.WriteAllText(Path.Combine(dir, PersonaOverlay.PersonaFileName), " overlay persona ");
    File.WriteAllText(Path.Combine(dir, PersonaOverlay.StyleFileName), "overlay style");
    var loaded = PersonaOverlay.Load(dir);
    Assert(loaded.HasAny, "A folder with persona.txt must count as an overlay.");
    Assert(loaded.ResolvePersona("fallback") == "overlay persona", "Overlay persona must win and be trimmed.");
    Assert(loaded.ResolveLore("lore-fallback") == "lore-fallback", "Missing lore.txt must keep the fallback.");
    Assert(loaded.ResolveStyleGuide() == "overlay style", "style.txt must replace the built-in style guide.");
}
finally
{
    Directory.Delete(dir, recursive: true);
}

Console.WriteLine("Lilith persona overlay tests passed (7 assertions).");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
