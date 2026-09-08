using System;
using System.IO;

namespace LilithTextInjector;

internal sealed class PersonaOverlay
{
    internal const string PersonaFileName = "persona.txt";
    internal const string LoreFileName = "lore.txt";
    internal const string EmotionFileName = "emotion.txt";
    internal const string StyleFileName = "style.txt";

    internal static PersonaOverlay Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty);

    internal string Persona { get; }
    internal string Lore { get; }
    internal string Emotion { get; }
    internal string Style { get; }
    internal bool HasAny { get; }

    private PersonaOverlay(string persona, string lore, string emotion, string style)
    {
        Persona = persona;
        Lore = lore;
        Emotion = emotion;
        Style = style;
        HasAny = persona.Length > 0 || lore.Length > 0 || emotion.Length > 0 || style.Length > 0;
    }

    internal static PersonaOverlay Load(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return Empty;

        var persona = ReadOptional(Path.Combine(directory, PersonaFileName));
        var lore = ReadOptional(Path.Combine(directory, LoreFileName));
        var emotion = ReadOptional(Path.Combine(directory, EmotionFileName));
        var style = ReadOptional(Path.Combine(directory, StyleFileName));
        return new PersonaOverlay(persona, lore, emotion, style);
    }

    internal string ResolvePersona(string fallback) => FirstNonEmpty(Persona, fallback);
    internal string ResolveLore(string fallback) => FirstNonEmpty(Lore, fallback);
    internal string ResolveEmotion(string fallback) => FirstNonEmpty(Emotion, fallback);

    internal string? ResolveStyleGuide()
        => HasAny ? Style : null;

    private static string FirstNonEmpty(string overlay, string fallback)
        => overlay.Length > 0 ? overlay : fallback ?? string.Empty;

    private static string ReadOptional(string path)
    {
        if (!File.Exists(path))
            return string.Empty;
        try
        {
            return File.ReadAllText(path).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }
}
