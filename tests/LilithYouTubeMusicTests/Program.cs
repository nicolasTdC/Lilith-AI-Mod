using System.Text.Json;
using LilithTextInjector;

using var songDoc = JsonDocument.Parse("{\"contents\":{\"videoId\":\"suxP321fM5s\",\"playlistId\":\"RDAMVMsuxP321fM5s\"}}");
Assert(YouTubeMusicPlayback.FindFirstVideoId(songDoc.RootElement) == "suxP321fM5s", "First song videoId should be used.");

using var playlistDoc = JsonDocument.Parse("{\"items\":[{\"playlistId\":\"RDAMPLPLskipme\"},{\"playlistId\":\"PLb8jJm2NiWfj6eGRpO9eq9xNYbTpqZ45I\"}]}");
Assert(YouTubeMusicPlayback.FindFirstPlaylistId(playlistDoc.RootElement) == "PLb8jJm2NiWfj6eGRpO9eq9xNYbTpqZ45I",
    "Public playlist IDs should win over radio mix IDs.");

Console.WriteLine("YouTube Music ID parsing tests passed (2 assertions).");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
