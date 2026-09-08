using LilithTextInjector;

WatchTogether.Reset();
Assert(!WatchTogether.IsActive, "Fresh state must be inactive.");
Assert(!WatchTogether.ShouldSearch("oi gatinho"), "Casual chat must not force search.");

WatchTogether.Observe("Gabi, quer assistir Netflix?");
Assert(WatchTogether.IsActive, "Asking to watch Netflix must start a session.");
Assert(WatchTogether.Active!.Platform == "Netflix", "Netflix must be recorded as the platform.");
Assert(WatchTogether.ShouldSearch("bota ai"), "An active session must keep search on.");

WatchTogether.Observe("Vamos colocar o três, então.");
Assert(WatchTogether.Active!.Episode == 3, "Episode three must be parsed from Portuguese.");
Assert(WatchTogether.BuildSearchQuery(WatchTogether.Active).Contains("episode 3"), "Search query must include the episode.");

WatchTogether.Reset();
WatchTogether.Observe("quero ver \"Oh My Ghost\" ep 3");
Assert(WatchTogether.Active!.Title.Contains("Oh My Ghost", StringComparison.OrdinalIgnoreCase), "Quoted title must be kept.");
Assert(WatchTogether.Active.Episode == 3, "ep 3 must set the episode.");

WatchTogether.Observe("parei de assistir");
Assert(!WatchTogether.IsActive, "Stop phrase must end the session.");

Assert(WatchTogether.ParseEpisode("bota no tres") == 3, "bota no tres is episode 3.");
Assert(WatchTogether.ParseEpisode("s01e04") == 4, "s01e04 episode.");
Assert(WatchTogether.ParseSeason("s01e04") == 1, "s01e04 season.");
Assert(WatchTogether.ParseTitle("Está havendo spooky love, você já ouviu?") == null, "A passing mention without a watch verb must not become the show title.");

var prompt = WatchTogether.Observe("assistir Netflix") is { } ? WatchTogether.BuildPrompt() : string.Empty;
Assert(prompt.Contains("web-search", StringComparison.OrdinalIgnoreCase), "Watch prompt must require web search.");
Assert(prompt.Contains("Do not download subtitle files", StringComparison.OrdinalIgnoreCase), "Watch prompt must not download subtitles from the web.");

var srtDir = Path.Combine(Path.GetTempPath(), "lilith-watch-subs-" + Guid.NewGuid().ToString("n"));
Directory.CreateDirectory(srtDir);
try
{
    File.WriteAllText(Path.Combine(srtDir, "Oh My Ghost S01E03.srt"),
        "1\n00:00:01,000 --> 00:00:03,000\n<i>Hello there</i>\n\n2\n00:00:04,000 --> 00:00:06,000\nWe should go\n");
    WatchTogether.Reset();
    WatchTogether.SubtitlesDirectory = srtDir;
    WatchTogether.Observe("quero ver \"Oh My Ghost\" ep 3");
    var withSubs = WatchTogether.BuildPrompt();
    Assert(withSubs.Contains("Hello there"), "Local SRT dialogue must be attached.");
    Assert(withSubs.Contains("We should go"), "Later SRT lines must be attached.");
    Assert(!withSubs.Contains("00:00:01"), "SRT timestamps must be stripped.");
}
finally
{
    Directory.Delete(srtDir, recursive: true);
    WatchTogether.SubtitlesDirectory = string.Empty;
    WatchTogether.Reset();
}
Assert(!prompt.Contains("wiki dump", StringComparison.OrdinalIgnoreCase) || prompt.Contains("no wiki dump"), "Watch prompt must keep replies short.");

Console.WriteLine("Lilith watch-together tests passed (16 assertions).");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
