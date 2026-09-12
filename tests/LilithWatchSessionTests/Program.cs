using LilithTextInjector;

WatchSession.End();
Assert(!WatchSession.IsActive, "Fresh session must be off.");
Assert(!WatchSession.LooksLikeStart("quero ver Netflix"), "Netflix recap is not a visual watch grant.");
Assert(!WatchSession.LooksLikeStart("vamos assistir netflix"), "Assistir Netflix alone is not a watch session.");
Assert(!WatchSession.LooksLikeStart("oi gatinho"), "Casual chat must not start watching.");
Assert(WatchSession.LooksLikeStart("assiste comigo"), "assiste comigo starts a session.");
Assert(WatchSession.LooksLikeStart("assiste isso comigo"), "assiste isso comigo starts a session.");
Assert(WatchSession.LooksLikeStart("vamos assistir juntos"), "vamos assistir juntos starts a session.");
Assert(WatchSession.LooksLikeStart("fica de olho na tela"), "fica de olho na tela starts a session.");
Assert(WatchSession.LooksLikeStart("watch this with me"), "English watch-with-me starts a session.");
Assert(WatchSession.LooksLikeStart("share my screen"), "share my screen starts a session.");
Assert(WatchSession.LooksLikeStop("para de olhar"), "para de olhar stops a session.");
Assert(WatchSession.LooksLikeStop("stop looking"), "stop looking stops a session.");
Assert(WatchSession.LooksLikeStop("parei de assistir"), "Recap stop also ends visual watch.");
Assert(WatchSession.IsGlanceTurn(WatchSession.GlanceMarker), "Glance marker is recognized.");
Assert(WatchSession.IsSilentReply("SILENCE"), "SILENCE is a silent glance.");
Assert(WatchSession.IsSilentReply("silêncio"), "Portuguese silêncio is silent.");
Assert(WatchSession.IsSilentReply("..."), "Ellipsis is silent.");
Assert(!WatchSession.IsSilentReply("nossa que jogada"), "A real comment is not silence.");

var now = DateTimeOffset.Parse("2026-09-09T20:00:00-03:00");
WatchSession.Begin(1, "Netflix", now, 15);
Assert(WatchSession.IsActive, "Begin must activate the session.");
Assert(WatchSession.Current.FirstLookPending, "First look must be pending.");
Assert(WatchSession.ShouldCapture(now, 8), "First look must capture immediately.");
Assert(WatchSession.ShouldComment(now, 40, false), "First look must comment even without a change.");
Assert(!WatchSession.IsExpired(now.AddMinutes(14)), "Session lasts the grant.");
Assert(WatchSession.IsExpired(now.AddMinutes(15)), "Session expires at the grant.");

WatchSession.NoteCapture(now, 1);
WatchSession.NoteComment(now);
Assert(!WatchSession.Current.FirstLookPending, "First look is consumed.");
Assert(!WatchSession.ShouldCapture(now.AddSeconds(7), 8), "Capture waits for the interval.");
Assert(WatchSession.ShouldCapture(now.AddSeconds(8), 8), "Capture fires after the interval.");
Assert(!WatchSession.ShouldComment(now.AddSeconds(10), 40, true), "Comments wait longer than captures.");
Assert(WatchSession.ShouldComment(now.AddSeconds(40), 40, true), "Changed frame can comment after the quiet floor.");
Assert(!WatchSession.ShouldComment(now.AddSeconds(40), 40, false), "Unchanged frames stay quiet.");

WatchSession.End();
Assert(!WatchSession.IsActive, "End must clear the session.");

var dark = new byte[32 * 32 * 4];
var light = new byte[32 * 32 * 4];
for (var i = 0; i < light.Length; i += 4)
{
    light[i] = 240;
    light[i + 1] = 240;
    light[i + 2] = 240;
    light[i + 3] = 255;
}
var darkHash = ScreenLook.AverageHash(dark, 32, 32);
var lightHash = ScreenLook.AverageHash(light, 32, 32);
Assert(ScreenLook.IsSimilarFrame(darkHash, darkHash), "Identical frames are similar.");
Assert(!ScreenLook.IsSimilarFrame(darkHash, lightHash), "Black vs white is a real change.");
Assert(ScreenLook.HammingDistance(darkHash, darkHash) == 0, "Identical hashes have distance 0.");

Assert(WatchSession.ClampDurationMinutes(0) == 1, "Duration floor is 1 minute.");
Assert(WatchSession.ClampDurationMinutes(90) == 30, "Duration cap is 30 minutes.");
Assert(WatchSession.GlancePrompt(true, "Netflix", "Oh My Ghost").Contains("first look", StringComparison.OrdinalIgnoreCase),
    "First-look prompt must forbid SILENCE.");
Assert(WatchSession.GlancePrompt(false, "Netflix", null).Contains("SILENCE", StringComparison.Ordinal),
    "Later glances may stay silent.");

Console.WriteLine("Lilith watch-session tests passed.");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
