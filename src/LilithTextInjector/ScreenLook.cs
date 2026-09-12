using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
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

    internal const string ImageAttachedPrompt =
        "\nA screenshot is attached to this turn. You can see it. Do not say antivirus blocked the print, that the screenshot failed, or that you cannot see the screen.";

    internal const string CaptureFailedPrompt =
        "\nScreenshot capture failed. Do not mention antivirus unless the error text does. Ask the user to describe what is on screen.";

    private static readonly Regex LookCue = new(
        @"(?i)(?:olha(?:r)?|olhe|v[eê](?:ja)?).{0,24}(?:tela|screen|monitor|isso|isto|aqui)|"
        + @"(?i)(?:consegue(?:m)?\s+ver|ver)\s+(?:a|minha|o|na)\s+(?:tela|ecr[aã]|screen)|"
        + @"(?i)o\s+que\s+(?:tem|t[aá]|est[aá])\s+(?:na|no)\s+(?:tela|ecr[aã]|monitor|screen)|"
        + @"(?i)see\s+my\s+screen|"
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
            var directory = ScreenshotDirectory();
            Directory.CreateDirectory(directory);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var pngPath = Path.Combine(directory, $"Lilith_{stamp}.png");

            if (TryCaptureWithGdi(pngPath, out var gdiError))
                return new CaptureResult(true, pngPath, pngPath, string.Empty);

            var powershellError = CaptureWithPowerShell(pngPath);
            if (File.Exists(pngPath) && new FileInfo(pngPath).Length > 0)
                return new CaptureResult(true, pngPath, pngPath, string.Empty);

            return new CaptureResult(false, string.Empty, string.Empty,
                FirstError(gdiError, powershellError) ?? "No screenshot file was created.");
        }
        catch (Exception exception)
        {
            return new CaptureResult(false, string.Empty, string.Empty, exception.Message);
        }
    }

    internal readonly record struct MemoryCapture(bool Success, byte[] Png, ulong Hash, string Title, string Error);

    internal static bool TryReadImageBase64(string path, out string mimeType, out string data)
    {
        mimeType = "image/png";
        data = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;
        try
        {
            return TryReadImageBase64(File.ReadAllBytes(path), out mimeType, out data);
        }
        catch
        {
            return false;
        }
    }

    internal static bool TryReadImageBase64(byte[] png, out string mimeType, out string data)
    {
        mimeType = "image/png";
        data = string.Empty;
        if (png == null || png.Length == 0 || png.Length > 6_000_000)
            return false;
        data = Convert.ToBase64String(png);
        return data.Length > 0;
    }

    internal static bool TryWriteWatchCapture(byte[] png, out string path)
    {
        path = string.Empty;
        if (png == null || png.Length == 0)
            return false;
        try
        {
            var directory = ScreenshotDirectory();
            Directory.CreateDirectory(directory);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            path = Path.Combine(directory, $"Lilith_watch_{stamp}.png");
            File.WriteAllBytes(path, png);
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }
        catch
        {
            path = string.Empty;
            return false;
        }
    }

    internal static bool IsWindowUsable(IntPtr hwnd)
        => hwnd != IntPtr.Zero && IsWindow(hwnd) && IsWindowVisible(hwnd) && !IsIconic(hwnd);

    internal static string WindowTitle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return string.Empty;
        try
        {
            var length = GetWindowTextLength(hwnd);
            if (length <= 0)
                return string.Empty;
            var builder = new StringBuilder(length + 1);
            GetWindowText(hwnd, builder, builder.Capacity);
            return builder.ToString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static MemoryCapture CaptureWindow(IntPtr hwnd, int maxWidth = 1280)
    {
        if (!IsWindowUsable(hwnd))
            return new MemoryCapture(false, Array.Empty<byte>(), 0, string.Empty, "Watched window is gone or minimized.");
        if (!TryGetWindowBounds(hwnd, out var left, out var top, out var width, out var height))
            return new MemoryCapture(false, Array.Empty<byte>(), 0, WindowTitle(hwnd), "Could not read the window bounds.");
        if (!TryCaptureRegion(left, top, width, height, maxWidth, null, out var png, out var hash, out var error))
            return new MemoryCapture(false, Array.Empty<byte>(), 0, WindowTitle(hwnd), error);
        return new MemoryCapture(true, png, hash, WindowTitle(hwnd), string.Empty);
    }

    internal static int HammingDistance(ulong a, ulong b)
    {
        var x = a ^ b;
        var count = 0;
        while (x != 0)
        {
            x &= x - 1;
            count++;
        }
        return count;
    }

    internal static bool IsSimilarFrame(ulong a, ulong b, int maxDistance = 4)
        => HammingDistance(a, b) <= maxDistance;

    internal static ulong AverageHash(byte[] bgra, int width, int height)
    {
        if (bgra == null || width <= 0 || height <= 0)
            return 0;
        var cells = new float[64];
        var cellW = Math.Max(1, width / 8);
        var cellH = Math.Max(1, height / 8);
        for (var cy = 0; cy < 8; cy++)
        {
            for (var cx = 0; cx < 8; cx++)
            {
                var x0 = cx * cellW;
                var y0 = cy * cellH;
                var x1 = cx == 7 ? width : Math.Min(width, x0 + cellW);
                var y1 = cy == 7 ? height : Math.Min(height, y0 + cellH);
                long sum = 0;
                var n = 0;
                for (var y = y0; y < y1; y++)
                {
                    var row = y * width * 4;
                    for (var x = x0; x < x1; x++)
                    {
                        var i = row + x * 4;
                        if (i + 2 >= bgra.Length)
                            continue;
                        sum += (bgra[i] * 19 + bgra[i + 1] * 183 + bgra[i + 2] * 54) >> 8;
                        n++;
                    }
                }
                cells[cy * 8 + cx] = n == 0 ? 0 : sum / (float)n;
            }
        }
        ulong hash = 0;
        for (var i = 0; i < 64; i++)
        {
            if (cells[i] >= 128f)
                hash |= 1UL << i;
        }
        return hash;
    }

    private static string ScreenshotDirectory()
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (string.IsNullOrWhiteSpace(pictures))
            pictures = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures");
        return Path.Combine(pictures, "Lilith Screenshots");
    }

    private static string? FirstError(params string?[] errors)
    {
        foreach (var error in errors)
            if (!string.IsNullOrWhiteSpace(error))
                return error;
        return null;
    }

    private static bool TryCaptureWithGdi(string pngPath, out string error)
    {
        var left = GetSystemMetrics(SmXvirtualscreen);
        var top = GetSystemMetrics(SmYvirtualscreen);
        var width = GetSystemMetrics(SmCxvirtualscreen);
        var height = GetSystemMetrics(SmCyvirtualscreen);
        return TryCaptureRegion(left, top, width, height, 1280, pngPath, out _, out _, out error);
    }

    private static bool TryGetWindowBounds(IntPtr hwnd, out int left, out int top, out int width, out int height)
    {
        left = top = width = height = 0;
        var rect = new WinRect();
        if (DwmGetWindowAttribute(hwnd, DwmwaExtendedFrameBounds, out rect, Marshal.SizeOf<WinRect>()) != 0
            && !GetWindowRect(hwnd, out rect))
            return false;
        left = rect.Left;
        top = rect.Top;
        width = rect.Right - rect.Left;
        height = rect.Bottom - rect.Top;
        return width > 8 && height > 8;
    }

    private static bool TryCaptureRegion(
        int left,
        int top,
        int width,
        int height,
        int maxWidth,
        string? pngPath,
        out byte[] png,
        out ulong hash,
        out string error)
    {
        png = Array.Empty<byte>();
        hash = 0;
        error = string.Empty;
        if (width <= 0 || height <= 0)
        {
            error = $"Capture size was invalid ({width}x{height}).";
            return false;
        }

        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            error = "GetDC failed.";
            return false;
        }

        var memoryDc = CreateCompatibleDC(screenDc);
        var bitmap = CreateCompatibleBitmap(screenDc, width, height);
        var previous = SelectObject(memoryDc, bitmap);
        try
        {
            if (!BitBlt(memoryDc, 0, 0, width, height, screenDc, left, top, Srccopy))
            {
                error = "BitBlt failed.";
                return false;
            }

            var pixels = new byte[width * height * 4];
            var info = new BitmapInfo();
            info.Header.Size = (uint)Marshal.SizeOf<BitmapInfoHeader>();
            info.Header.Width = width;
            info.Header.Height = -height;
            info.Header.Planes = 1;
            info.Header.BitCount = 32;
            info.Header.Compression = 0;
            if (GetDIBits(memoryDc, bitmap, 0, (uint)height, pixels, ref info, 0) == 0)
            {
                error = "GetDIBits failed.";
                return false;
            }

            hash = AverageHash(pixels, width, height);
            png = EncodePng(pixels, width, height, maxWidth);
            if (png.Length == 0)
            {
                error = "PNG encode produced no bytes.";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(pngPath))
            {
                File.WriteAllBytes(pngPath, png);
                return File.Exists(pngPath) && new FileInfo(pngPath).Length > 0;
            }
            return true;
        }
        finally
        {
            SelectObject(memoryDc, previous);
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (memoryDc != IntPtr.Zero) DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static string CaptureWithPowerShell(string pngPath)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "lilith-screen-look.ps1");
        var pngEscaped = pngPath.Replace("'", "''");
        File.WriteAllText(scriptPath,
            "Add-Type -AssemblyName System.Windows.Forms; " +
            "Add-Type -AssemblyName System.Drawing; " +
            "$bounds=[System.Windows.Forms.SystemInformation]::VirtualScreen; " +
            "$bitmap=New-Object System.Drawing.Bitmap($bounds.Width,$bounds.Height); " +
            "$graphics=[System.Drawing.Graphics]::FromImage($bitmap); " +
            "$graphics.CopyFromScreen($bounds.Left,$bounds.Top,0,0,$bitmap.Size); " +
            $"$bitmap.Save('{pngEscaped}',[System.Drawing.Imaging.ImageFormat]::Png); " +
            "$graphics.Dispose(); $bitmap.Dispose();",
            Encoding.UTF8);
        using var process = Process.Start(new ProcessStartInfo("powershell.exe")
        {
            Arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        });
        if (process == null)
            return "The screenshot helper could not be started.";
        if (!process.WaitForExit(12000))
        {
            try { process.Kill(true); } catch { }
            return "The screenshot helper timed out.";
        }
        var error = process.StandardError.ReadToEnd();
        return process.ExitCode == 0 ? string.Empty : (string.IsNullOrWhiteSpace(error) ? $"PowerShell exit {process.ExitCode}." : error.Trim());
    }

    private static byte[] EncodePng(byte[] bgra, int width, int height, int maxWidth)
    {
        var scale = 1;
        while (width / scale > maxWidth)
            scale *= 2;
        var outWidth = Math.Max(1, width / scale);
        var outHeight = Math.Max(1, height / scale);
        var stride = outWidth * 3 + 1;
        var raw = new byte[stride * outHeight];
        for (var y = 0; y < outHeight; y++)
        {
            var dest = y * stride;
            raw[dest] = 0;
            var srcY = Math.Min(height - 1, y * scale);
            for (var x = 0; x < outWidth; x++)
            {
                var srcX = Math.Min(width - 1, x * scale);
                var src = (srcY * width + srcX) * 4;
                var i = dest + 1 + x * 3;
                raw[i] = bgra[src + 2];
                raw[i + 1] = bgra[src + 1];
                raw[i + 2] = bgra[src];
            }
        }

        using var idat = new MemoryStream();
        idat.WriteByte(0x78);
        idat.WriteByte(0x01);
        using (var deflate = new DeflateStream(idat, CompressionLevel.Fastest, true))
            deflate.Write(raw, 0, raw.Length);
        WriteAdler32(idat, raw);

        using var png = new MemoryStream();
        png.Write(PngSignature, 0, PngSignature.Length);
        WriteChunk(png, "IHDR", Ihdr(outWidth, outHeight));
        WriteChunk(png, "IDAT", idat.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static byte[] Ihdr(int width, int height)
    {
        var data = new byte[13];
        WriteInt(data, 0, width);
        WriteInt(data, 4, height);
        data[8] = 8;
        data[9] = 2;
        return data;
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        WriteInt(stream, data.Length);
        stream.Write(typeBytes, 0, 4);
        stream.Write(data, 0, data.Length);
        var crc = Crc32(typeBytes, data);
        WriteInt(stream, (int)crc);
    }

    private static void WriteInt(Stream stream, int value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static void WriteAdler32(Stream stream, byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }
        var adler = (b << 16) | a;
        WriteInt(stream, (int)adler);
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in type)
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        foreach (var value in data)
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    private static readonly uint[] CrcTable = BuildCrcTable();
    private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
    private const int SmXvirtualscreen = 76;
    private const int SmYvirtualscreen = 77;
    private const int SmCxvirtualscreen = 78;
    private const int SmCyvirtualscreen = 79;
    private const uint Srccopy = 0x00CC0020;

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }

    private const int DwmwaExtendedFrameBounds = 9;

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out WinRect rect);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out WinRect rect, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct WinRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr src, int x1, int y1, uint rop);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BitmapInfo info, uint usage);

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }
}
