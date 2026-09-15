using System.Text.Json;
using LilithTextInjector;

using var songDoc = JsonDocument.Parse("{\"musicResponsiveListItemRenderer\":{\"playlistItemData\":{\"videoId\":\"suxP321fM5s\"},\"flexColumns\":[{\"text\":{\"runs\":[{\"text\":\"Example Song\"}]}}]}}");
Assert(YouTubeMusicPlayback.FindFirstVideoId(songDoc.RootElement) == "suxP321fM5s", "First song videoId should be used.");

using var playlistDoc = JsonDocument.Parse("{\"items\":[{\"playlistId\":\"RDAMPLPLskipme\"},{\"playlistId\":\"PLb8jJm2NiWfj6eGRpO9eq9xNYbTpqZ45I\"}]}");
Assert(YouTubeMusicPlayback.FindFirstPlaylistId(playlistDoc.RootElement) == "PLb8jJm2NiWfj6eGRpO9eq9xNYbTpqZ45I",
    "Public playlist IDs should win over radio mix IDs.");

var query = "conexao zona sul racionais mcs ft. claris";
var panico = YouTubeMusicPlayback.ScoreTitle(query, "Panico na Zona Sul");
var conexao = YouTubeMusicPlayback.ScoreTitle(query, "Conexao Zona Sul - Racionais MC's ft. ClariS");
Assert(conexao > panico, "The requested title should outrank a different Zona Sul song.");
Assert(YouTubeMusicPlayback.NormalizeMusicText("Conexão") == "conexao", "Diacritics should be stripped for matching.");

var picked = YouTubeMusicPlayback.PickBestHit(query, new[]
{
    new YouTubeMusicPlayback.SearchHit("DheKLQ0RzOw", "Pânico na Zona Sul"),
    new YouTubeMusicPlayback.SearchHit("B3gAZvncfa0", "Conexão Zona Sul - Racionais MC's ft. ClariS")
});
Assert(picked?.VideoId == "B3gAZvncfa0", "Video-catalog matches should be chosen over the wrong song result.");

Assert(YouTubeMusicPlayback.UserAskedToPlayOrChangeMusic("toca um lofi"), "Portuguese play requests should count as explicit.");
Assert(YouTubeMusicPlayback.UserAskedToPlayOrChangeMusic("play lo-fi playlist"), "English play requests should count as explicit.");
Assert(!YouTubeMusicPlayback.UserAskedToPlayOrChangeMusic("estou cansado hoje"), "Casual chat must not count as a music request.");
Assert(!YouTubeMusicPlayback.UserAskedToPlayOrChangeMusic("that playlist was nice"), "Mentioning a playlist must not start another one.");
Assert(YouTubeMusicPlayback.UserAskedToChangeMusic("toca outra"), "Asking for another track should count as a change.");
Assert(YouTubeMusicPlayback.UserAskedToChangeMusic("trocar as musicas"), "Portuguese plural 'músicas' should count as a change.");
Assert(YouTubeMusicPlayback.UserAskedToPlayOrChangeMusic("eeei vc devia conseguir trocar as musicas agr. tenta ai"),
    "Asking her to switch songs should count as an explicit music request.");
Assert(YouTubeMusicPlayback.UserAskedToPlayOrChangeMusic(
        "tenta de nvo so pra eu ter ctz",
        "eeei vc devia conseguir trocar as musicas agr. tenta ai"),
    "Retrying after a switch request should still count as a music request.");
Assert(!YouTubeMusicPlayback.UserAskedToPlayOrChangeMusic("tenta de nvo so pra eu ter ctz"),
    "A retry with no music context must not start playback on its own.");
Assert(!YouTubeMusicPlayback.ShouldSkipDuplicatePlay("radio", "cutest pair", "song", "cutest pair", true, true),
    "Switching the same title to radio should not be treated as a duplicate.");
Assert(YouTubeMusicPlayback.LooksLikeGenericLofiQuery("lofi girl beats"), "Lofi queries should be recognized.");
Assert(YouTubeMusicPlayback.WithAutoplay("https://music.youtube.com/watch?v=abc") == "https://music.youtube.com/watch?v=abc&autoplay=1",
    "Watch URLs should request autoplay.");
Assert(YouTubeMusicPlayback.WithAutoplay("https://music.youtube.com/playlist?list=LM").Contains("autoplay=1", StringComparison.Ordinal),
    "Playlist URLs should request autoplay.");
Assert(YouTubeMusicPlayback.WithAutoplay("https://music.youtube.com/watch?v=abc&autoplay=1") == "https://music.youtube.com/watch?v=abc&autoplay=1",
    "Existing autoplay flags must not be duplicated.");
Assert(YouTubeMusicPlayback.LooksLikePearDesktopProcess("YouTube Music"), "Pear Desktop's Windows process name should match.");
Assert(YouTubeMusicPlayback.LooksLikePearDesktopProcess("youtube-music"), "Pear Desktop's linux-style process name should match.");
Assert(!YouTubeMusicPlayback.LooksLikePearDesktopProcess("firefox"), "Firefox must not be treated as Pear Desktop.");
Assert(YouTubeMusicPlayback.LooksLikePearDesktopShortcut("Pear Desktop"), "Start Menu Pear Desktop shortcuts should match.");
Assert(YouTubeMusicPlayback.LooksLikePearDesktopShortcut("YouTube Music"), "Start Menu YouTube Music shortcuts should match.");
Assert(!YouTubeMusicPlayback.LooksLikePearDesktopShortcut("YouTube"), "Plain YouTube shortcuts must not match.");
Assert(YouTubeMusicPlayback.QuoteArgument("https://music.youtube.com/watch?v=abc") == "https://music.youtube.com/watch?v=abc",
    "URLs without spaces should stay unquoted.");
Assert(YouTubeMusicPlayback.QuoteArgument(@"C:\Program Files\YouTube Music.exe") == "\"C:\\Program Files\\YouTube Music.exe\"",
    "Paths with spaces should be quoted.");
Assert(YouTubeMusicPlayback.SearchBarQuery("song", "Example Song", "example song") == "example song",
    "Pear Desktop search should keep a song-and-artist query instead of a watch URL.");
Assert(YouTubeMusicPlayback.SearchBarQuery("song", "https://music.youtube.com/watch?v=Gg5Hv08w82Q&autoplay=1", "example song") == "example song",
    "Watch URLs must not be pasted into the YouTube Music search bar.");
Assert(YouTubeMusicPlayback.SearchBarQuery("liked", "Liked music", "") == string.Empty,
    "Liked-music opens should not type into the search bar.");
Assert(YouTubeMusicPlayback.SearchBarQuery("song", "Butterfly", "butterfly loona") == "butterfly loona",
    "A title-only catalog hit must not drop the artist from search.");
Assert(YouTubeMusicPlayback.FormatSearchQuery("Butterfly", "LOONA") == "Butterfly LOONA",
    "Song searches should include the artist.");
Assert(YouTubeMusicPlayback.HasSongAndArtist("butterfly loona"), "Song plus artist should count as a complete search.");
Assert(!YouTubeMusicPlayback.HasSongAndArtist("butterfly"), "A title alone should not count as song-plus-artist.");
Assert(YouTubeMusicPlayback.ScoreTitle("butterfly loona", "Butterfly", "LOONA")
    > YouTubeMusicPlayback.ScoreTitle("butterfly loona", "Butterfly"),
    "The LOONA recording should outrank a title-only Butterfly hit.");
Assert(YouTubeMusicPlayback.PickBestHit("butterfly loona", new[]
{
    new YouTubeMusicPlayback.SearchHit("genericId123", "Butterfly"),
    new YouTubeMusicPlayback.SearchHit("loonaId12345", "Butterfly", "LOONA")
})?.VideoId == "loonaId12345", "Search should pick the requested artist, not the first Butterfly.");
Assert(YouTubeMusicPlayback.LooksLikeMusicUrl("https://music.youtube.com/watch?v=abc&autoplay=1"),
    "Watch URLs should be recognized.");
Assert(!YouTubeMusicPlayback.ShouldSkipDuplicatePlay("song", "new song", "song", "old song", true, false),
    "A different song request should switch even if music is already playing.");
Assert(YouTubeMusicPlayback.ShouldSkipDuplicatePlay("song", "same song", "song", "same song", true, false),
    "Repeating the same song should not reopen it.");
Assert(!YouTubeMusicPlayback.ShouldSkipDuplicatePlay("song", "new song", "song", "old song", false, false),
    "The first play request should not be treated as a duplicate.");
Assert(YouTubeMusicPlayback.ShouldSkipDuplicatePlay("song", "lofi beats", "playlist", "lofi girl", true, false),
    "A second generic lofi request should stay skipped.");
Assert(!YouTubeMusicPlayback.ShouldSkipDuplicatePlay("song", "lofi beats", "playlist", "lofi girl", true, true),
    "An explicit change request should replace even generic lofi.");

Console.WriteLine("YouTube Music ID parsing tests passed (43 assertions).");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
