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
Assert(YouTubeMusicPlayback.LooksLikeGenericLofiQuery("lofi girl beats"), "Lofi queries should be recognized.");

Console.WriteLine("YouTube Music ID parsing tests passed (12 assertions).");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
