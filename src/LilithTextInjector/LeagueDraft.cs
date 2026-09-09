using System;
using System.Diagnostics;
using System.Text;

namespace LilithTextInjector;

internal static class LeagueDraft
{
    internal static string TryReadBrief()
    {
        try
        {
            var project = (Plugin.LeagueDraftProject.Value ?? string.Empty).Trim();
            if (project.Length == 0)
                project = "/home/nic/projects/lol-champ-select-recommender";
            var quoted = project.Replace("'", "'\\''");
            var command =
                "cd '" + quoted + "' && .venv/bin/python -m lol_champ_select_recommender --once --lilith --no-clear --language pt_BR";
            using var process = Process.Start(new ProcessStartInfo("wsl.exe")
            {
                Arguments = "-e bash -lc \"" + command.Replace("\"", "\\\"") + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            });
            if (process == null)
                return string.Empty;
            if (!process.WaitForExit(25000))
            {
                try { process.Kill(true); } catch { }
                return string.Empty;
            }
            var output = process.StandardOutput.ReadToEnd().Trim();
            var error = process.StandardError.ReadToEnd().Trim();
            if (output.Length > 4000)
                output = output[..4000];
            if (process.ExitCode != 0 && output.Length == 0)
                return error.Length == 0 ? string.Empty : "League client error: " + error;
            return output;
        }
        catch (Exception exception)
        {
            return "League client error: " + exception.Message;
        }
    }
}
