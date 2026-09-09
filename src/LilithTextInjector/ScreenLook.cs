using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace LilithTextInjector;

internal static class ScreenLook
{
    internal const string ToolName = "look_at_screen";
    internal const string LegacyToolName = "league_champ_select";

    internal const string ToolDescription =
        "Capture the current desktop and attach the image so you can see what the user is looking at. Call this when they ask you to look, check, read, or analyze the screen, a window, an error, a clip, or League champ select. The image is for this turn only. Answer their actual question about the image; do not narrate unrelated windows or personal data. For League pick/draft help, also web-search current patch advice.";

    internal const string LookPrompt =
        "\nScreen look: A screenshot of the user's desktop is attached because they asked you to look at something. Study the image, focus on what they asked about, and answer in character. Keep it short. Do not recite unrelated windows, notifications, passwords, or personal data. If the image is unclear, say so.";

    internal const string LeaguePrompt =
        "\nLeague champ-select help: A screenshot of the user's desktop is attached. Look at the champion select UI: the user's assigned lane/role, their profile/hover, ally picks and hovers, enemy picks, and bans. Ignore unrelated windows. Then you MUST use web search for the current patch meta for that exact lane versus the visible threats. Suggest one main champion they can still pick and one backup. Stay in character, keep it short, and do not recommend banned or already-taken champions. If the screenshot is not champ select, say so and ask what lane they are on.";

    private static readonly Regex LookCue = new(
        @"(?i)(?:olha(?:r)?|olhe|v[eê](?:ja)?)\s+(?:isso|isto|aqui|a[ií]|pra\s+(?:mim\s+)?(?:isso|aqui|tela)|a\s+tela|o\s+monitor|nesta|nessa|o\s+que)|"
        + @"(?i)o\s+que\s+(?:tem|t[aá]|est[aá])\s+(?:na|no)\s+(?:tela|ecr[aã]|monitor|screen)|"
        + @"(?i)(?:analisa(?:r)?|l[eê](?:r)?)\s+(?:isso|a\s+tela|a\s+imagem|o\s+print|o\s+erro)|"
        + @"(?i)(?:tira(?:r)?|pega(?:r)?)\s+(?:um\s+)?(?:print|screenshot).{0,24}(?:v[eê]|diz|fala|olha|analisa)|"
        + @"(?i)look\s+at\s+(?:this|that|it|my\s+screen|the\s+screen|the\s+monitor)|"
        + @"(?i)(?:what\s+do\s+you\s+see|can\s+you\s+see\s+(?:this|that|my\s+screen)|check\s+(?:my\s+)?(?:screen|this)|what(?:'s|\s+is)\s+on\s+(?:my\s+)?screen)|"
        + @"(?i)(?:看看|看一下|幫我看|帮我看).{0,8}(?:螢幕|屏幕|畫面|画面|這個|这个)|"
        + @"(?i)(?:画面|スクリーン).{0,6}(?:見て|見てくれ|確認)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LeagueCue = new(
        @"(?i)\b(?:league(?:\s+of\s+legends)?|\blol\b|wild\s*rift|champ(?:ion)?\s*select|draft|pick(?:\/| )?ban|banphase)\b|"
        + @"(?i)\b(?:sugere|sugerir|indica|indicar|qual|which|what)\b.{0,40}\b(?:champ(?:ion)?s?|campe[aã]o(?:es)?|pick|ban)\b|"
        + @"(?i)\b(?:campe[aã]o|champion)\b.{0,24}\b(?:jogar|play|pick|pegar|escolher)\b|"
        + @"(?i)\b(?:minha\s+lane|my\s+lane|top(?:lane)?|jungle|jgl|mid(?:lane)?|adc|bot(?:lane)?|sup(?:port)?)\b.{0,32}\b(?:champ|campe[aã]o|pick|sugest)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool LooksLikeLookRequest(string? text)
        => !string.IsNullOrWhiteSpace(text) && LookCue.IsMatch(text.Trim());

    internal static bool LooksLikeLeagueRequest(string? text)
        => !string.IsNullOrWhiteSpace(text) && LeagueCue.IsMatch(text.Trim());

    internal static bool ShouldCapture(string? text)
        => LooksLikeLookRequest(text) || LooksLikeLeagueRequest(text);

    internal static bool ShouldWebSearch(string? text)
        => LooksLikeLeagueRequest(text);

    internal static string PromptFor(string? text)
        => LooksLikeLeagueRequest(text) ? LeaguePrompt : LookPrompt;

    internal static bool IsLookTool(string? name)
        => string.Equals(name, ToolName, StringComparison.Ordinal)
            || string.Equals(name, LegacyToolName, StringComparison.Ordinal);

    internal readonly record struct CaptureResult(bool Success, string PngPath, string JpegPath, string Error);

    internal static CaptureResult CaptureDesktop()
    {
        try
        {
            var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrWhiteSpace(pictures))
                pictures = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures");
            var directory = Path.Combine(pictures, "Lilith Screenshots");
            Directory.CreateDirectory(directory);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var pngPath = Path.Combine(directory, $"Lilith_{stamp}.png");
            var jpegPath = Path.Combine(directory, $"Lilith_{stamp}_look.jpg");
            var pngEscaped = pngPath.Replace("'", "''");
            var jpegEscaped = jpegPath.Replace("'", "''");
            var script =
                "Add-Type -AssemblyName System.Windows.Forms; " +
                "Add-Type -AssemblyName System.Drawing; " +
                "$bounds=[System.Windows.Forms.SystemInformation]::VirtualScreen; " +
                "$bitmap=New-Object System.Drawing.Bitmap($bounds.Width,$bounds.Height); " +
                "$graphics=[System.Drawing.Graphics]::FromImage($bitmap); " +
                "$graphics.CopyFromScreen($bounds.Left,$bounds.Top,0,0,$bitmap.Size); " +
                $"$bitmap.Save('{pngEscaped}',[System.Drawing.Imaging.ImageFormat]::Png); " +
                "$encoder=[System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }; " +
                "$ep=New-Object System.Drawing.Imaging.EncoderParameters(1); " +
                "$ep.Param[0]=New-Object System.Drawing.Imaging.EncoderParameter([System.Drawing.Imaging.Encoder]::Quality,[int64]72); " +
                $"$bitmap.Save('{jpegEscaped}',$encoder,$ep); " +
                "$graphics.Dispose(); $bitmap.Dispose();";
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            using var process = Process.Start(new ProcessStartInfo("powershell.exe")
            {
                Arguments = $"-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand {encoded}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            });
            if (process == null)
                return new CaptureResult(false, string.Empty, string.Empty, "The screenshot helper could not be started.");
            if (!process.WaitForExit(12000))
            {
                try { process.Kill(true); } catch { }
                return new CaptureResult(false, string.Empty, string.Empty, "The screenshot helper timed out.");
            }
            var error = process.StandardError.ReadToEnd();
            if (process.ExitCode != 0 || !File.Exists(pngPath))
            {
                return new CaptureResult(false, string.Empty, string.Empty,
                    string.IsNullOrWhiteSpace(error) ? "No screenshot file was created." : error.Trim());
            }

            return new CaptureResult(true, pngPath, File.Exists(jpegPath) ? jpegPath : pngPath, string.Empty);
        }
        catch (Exception exception)
        {
            return new CaptureResult(false, string.Empty, string.Empty, exception.Message);
        }
    }

    internal static bool TryReadImageBase64(string path, out string mimeType, out string data)
    {
        mimeType = "image/png";
        data = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0 || bytes.Length > 6_000_000)
                return false;
            mimeType = path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                ? "image/jpeg"
                : "image/png";
            data = Convert.ToBase64String(bytes);
            return data.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
