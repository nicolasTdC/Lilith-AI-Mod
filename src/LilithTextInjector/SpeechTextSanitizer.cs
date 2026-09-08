using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LilithTextInjector;

internal static class SpeechTextSanitizer
{
    private static readonly Regex MarkdownLink = new(
        @"\[([^\]]+)\]\(https?://[^\s\)]+\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Url = new(
        @"https?://\S+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SourceLabel = new(
        @"(?:來源|资料来源|資料來源|出典|Sources?)\s*[:：]\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex EmojiShortcode = new(
        @":[a-z0-9_+\-]{2,}:",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Emoticon = new(
        @"(?:[tT][-_][tT]|[tT]{2}_[tT]{2}|[xX][dD]|[uU][wW][uU]|[oO][wW][oO]|[uU][mM][uU]|</3|<3|(?<![A-Za-zÀ-ÿ0-9])(?:[sS]2)+(?![A-Za-zÀ-ÿ0-9])|>[._]?<|>[._]<|\^[_ ]?\^|\^\^|-[._]-|\.[_.]\.|grr+|[:;=8][-']?[)(/\\DPpOocC|]|[)(DPp][-']?[:;=8]|(?<!\d):3(?!\d))",
        RegexOptions.Compiled);

    private static readonly Regex Laughter = new(
        @"(?<![A-Za-zÀ-ÿ])(?:k{2,}|o+k{3,}|(?:ha){2,}h?|(?:he){2,}h?|(?:hi){2,}h?|(?:hu){2,}h?|(?:ah){3,}a?|(?:rs)+|(?:ks){2,}|(?:sk){2,}|(?:hue){2,}|(?:ja){3,}j?a?|lol+|lmao+|lmfao|rofl|(?:há[\s,]*){2,}há?|哈{2,}|呵{2,}|嘿{2,}|嘻{2,}|ふ{2,}|うふふ+|w{3,}|233{2,})(?![A-Za-zÀ-ÿ])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Token = new(@"\S+", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"[ \t]+", RegexOptions.Compiled);
    private static readonly Regex SpaceAroundNewline = new(@"[ \t]*\n[ \t]*", RegexOptions.Compiled);
    private static readonly Regex ExtraNewlines = new(@"\n{3,}", RegexOptions.Compiled);
    private static readonly Regex SpaceBeforePunct = new(@" +([,.!?;:…。，、！？])", RegexOptions.Compiled);
    private static readonly Regex RepeatedExclaim = new(@"!+", RegexOptions.Compiled);
    private static readonly Regex RepeatedQuestion = new(@"\?{2,}", RegexOptions.Compiled);
    private static readonly Regex RepeatedLetters = new(@"([A-Za-zÀ-ÿ])\1{2,}", RegexOptions.Compiled);
    private static readonly Regex HypeWordTail = new(
        @"\b(perfeit[ao]|maravilhos[ao]|incr[ií]vel)(?!mente)[a-zà-ÿ]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PunctOnly = new(@"^[\s,.!?;:~…。，、！？～·•\-—]+$", RegexOptions.Compiled);
    private static readonly Regex ExtraPauses = new(@"([.,。，])(?:\s*[.,。，])+", RegexOptions.Compiled);
    private static readonly Regex LeadingPause = new(@"^[.,\s]+", RegexOptions.Compiled);
    private static readonly Regex TrailingPauseLines = new(@"\n[. ,]+$", RegexOptions.Compiled);
    private static readonly Regex NeverMatches = new(@"a^", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> DefaultAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["abs"] = "abraços",
        ["agr"] = "agora",
        ["ajd"] = "ajuda",
        ["aki"] = "aqui",
        ["amg"] = "amigo",
        ["aq"] = "aqui",
        ["bj"] = "beijo",
        ["bjo"] = "beijo",
        ["bjs"] = "beijos",
        ["blz"] = "beleza",
        ["bae"] = "bei",
        ["blza"] = "beleza",
        ["bnt"] = "bonito",
        ["cm"] = "com",
        ["cmg"] = "comigo",
        ["cntg"] = "contigo",
        ["ctg"] = "contigo",
        ["ctz"] = "certeza",
        ["d+"] = "demais",
        ["dms"] = "demais",
        ["dmr"] = "demorou",
        ["dnd"] = "de nada",
        ["dnv"] = "de novo",
        ["dps"] = "depois",
        ["eh"] = "é",
        ["ep"] = "episódio",
        ["fds"] = "fim de semana",
        ["flw"] = "falou",
        ["fml"] = "família",
        ["fmz"] = "firmeza",
        ["ft"] = "foto",
        ["fz"] = "fazer",
        ["fzr"] = "fazer",
        ["glr"] = "galera",
        ["gnt"] = "gente",
        ["hj"] = "hoje",
        ["hrs"] = "horas",
        ["ja"] = "já",
        ["kd"] = "cadê",
        ["krl"] = "caralho",
        ["mddc"] = "meu Deus do céu",
        ["mds"] = "meu Deus",
        ["mlr"] = "melhor",
        ["mn"] = "mano",
        ["msma"] = "mesma",
        ["msc"] = "música",
        ["msm"] = "mesmo",
        ["msg"] = "mensagem",
        ["mt"] = "muito",
        ["mta"] = "muita",
        ["mto"] = "muito",
        ["n"] = "não",
        ["nao"] = "não",
        ["nd"] = "nada",
        ["ndv"] = "nada a ver",
        ["nene"] = "nenê",
        ["ne"] = "né",
        ["neh"] = "né",
        ["ngc"] = "negócio",
        ["ngm"] = "ninguém",
        ["nn"] = "não",
        ["ñ"] = "não",
        ["obg"] = "obrigada",
        ["obgd"] = "obrigada",
        ["oq"] = "o que",
        ["pdc"] = "pode crer",
        ["pf"] = "por favor",
        ["pfr"] = "por favor",
        ["pfrzinho"] = "por favorzinho",
        ["pft"] = "perfeito",
        ["pfv"] = "por favor",
        ["pfvr"] = "por favor",
        ["pfvrzinho"] = "por favorzinho",
        ["pfvzinho"] = "por favorzinho",
        ["pfzinho"] = "por favorzinho",
        ["porfavorzinho"] = "por favorzinho",
        ["plmdds"] = "pelo amor de Deus",
        ["plmns"] = "pelo menos",
        ["pprt"] = "papo reto",
        ["pq"] = "porque",
        ["pse"] = "pode ser",
        ["puq"] = "porque",
        ["q"] = "que",
        ["qd"] = "quando",
        ["qdo"] = "quando",
        ["qm"] = "quem",
        ["qnd"] = "quando",
        ["qnt"] = "quanto",
        ["qnts"] = "quantos",
        ["qq"] = "qualquer",
        ["qqr"] = "qualquer",
        ["qlq"] = "qualquer",
        ["qlqr"] = "qualquer",
        ["qr"] = "quer",
        ["qria"] = "queria",
        ["qser"] = "quiser",
        ["qt"] = "quanto",
        ["qto"] = "quanto",
        ["rlx"] = "relaxa",
        ["s"] = "sim",
        ["sdd"] = "saudade",
        ["sdds"] = "saudades",
        ["sla"] = "sei lá",
        ["slc"] = "cê é louco",
        ["slk"] = "cê é louco",
        ["sm"] = "sem",
        ["smp"] = "sempre",
        ["sqn"] = "só que não",
        ["ss"] = "sim",
        ["tb"] = "também",
        ["tbm"] = "também",
        ["td"] = "tudo",
        ["tds"] = "todos",
        ["tlg"] = "tá ligado",
        ["tlgd"] = "tá ligado",
        ["ta"] = "tá",
        ["tamo"] = "estamos",
        ["tmb"] = "também",
        ["tmj"] = "tamo junto",
        ["to"] = "tô",
        ["tres"] = "três",
        ["trd"] = "tarde",
        ["vamo"] = "vamos",
        ["vao"] = "vão",
        ["vc"] = "você",
        ["voce"] = "você",
        ["vcs"] = "vocês",
        ["vdd"] = "verdade",
        ["vlw"] = "valeu",
        ["vms"] = "vamos",
        ["xau"] = "tchau",
    };

    private static Dictionary<string, string> Abbreviations = DefaultAbbreviations;
    private static Regex AbbreviationPattern = BuildAbbreviationPattern(DefaultAbbreviations);

    internal static string Prepare(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var cleaned = MarkdownLink.Replace(text, "$1");
        cleaned = Url.Replace(cleaned, string.Empty);
        cleaned = SourceLabel.Replace(cleaned, string.Empty);
        cleaned = StripEmojiRunes(cleaned);
        cleaned = EmojiShortcode.Replace(cleaned, ". ");
        cleaned = Emoticon.Replace(cleaned, ". ");
        cleaned = Laughter.Replace(cleaned, ". ");
        cleaned = Token.Replace(cleaned, match => IsKeyboardSmash(match.Value) ? ". " : match.Value);
        cleaned = RepeatedLetters.Replace(cleaned, "$1");
        cleaned = HypeWordTail.Replace(cleaned, "$1");
        cleaned = RepeatedExclaim.Replace(cleaned, ".");
        cleaned = RepeatedQuestion.Replace(cleaned, "?");
        cleaned = ExpandAbbreviations(cleaned);
        cleaned = SpaceAroundNewline.Replace(cleaned, "\n");
        cleaned = ExtraPauses.Replace(cleaned, "$1");
        cleaned = Spaces.Replace(cleaned, " ");
        cleaned = ExtraNewlines.Replace(cleaned, "\n\n");
        cleaned = TrailingPauseLines.Replace(cleaned, string.Empty);
        cleaned = SpaceBeforePunct.Replace(cleaned, "$1");
        cleaned = LeadingPause.Replace(cleaned, string.Empty).Trim();
        if (cleaned.Length == 0 || PunctOnly.IsMatch(cleaned))
            return string.Empty;
        return cleaned;
    }

    internal static bool TryLoadAbbreviations(string? path, out int count, out string? error)
    {
        count = Abbreviations.Count;
        error = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Abbreviations = DefaultAbbreviations;
            AbbreviationPattern = BuildAbbreviationPattern(Abbreviations);
            count = Abbreviations.Count;
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "abbreviation file must be a JSON object of string:string";
                return false;
            }

            var loaded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in DefaultAbbreviations)
                loaded[pair.Key] = pair.Value;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                    continue;
                var key = property.Name.Trim();
                var spoken = property.Value.GetString()?.Trim();
                if (key.Length == 0 || string.IsNullOrEmpty(spoken))
                    continue;
                loaded[key] = spoken;
            }

            Abbreviations = loaded;
            AbbreviationPattern = BuildAbbreviationPattern(loaded);
            count = loaded.Count;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    internal static string ExpandAbbreviations(string text)
    {
        if (string.IsNullOrEmpty(text) || Abbreviations.Count == 0)
            return text;
        return AbbreviationPattern.Replace(text, match =>
            Abbreviations.TryGetValue(match.Value, out var spoken) ? spoken : match.Value);
    }

    private static Regex BuildAbbreviationPattern(Dictionary<string, string> map)
    {
        if (map.Count == 0)
            return NeverMatches;
        var keys = new List<string>(map.Count);
        foreach (var key in map.Keys)
        {
            if (key.Length > 0)
                keys.Add(Regex.Escape(key));
        }
        keys.Sort((left, right) => right.Length.CompareTo(left.Length));
        return new Regex(
            @"(?<![A-Za-zÀ-ÿ0-9])(?:" + string.Join("|", keys) + @")(?![A-Za-zÀ-ÿ0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    private static string StripEmojiRunes(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingPause = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsEmojiRune(rune.Value))
            {
                pendingPause = true;
                continue;
            }
            if (pendingPause)
            {
                AppendPause(builder);
                pendingPause = false;
            }
            builder.Append(rune);
        }
        if (pendingPause)
            AppendPause(builder);
        return builder.ToString();
    }

    private static void AppendPause(StringBuilder builder)
    {
        if (builder.Length == 0)
            return;
        var last = builder[builder.Length - 1];
        if (last is '.' or ',' or '?' or '!' or '。' or '，' or '？' or '！')
            return;
        builder.Append(". ");
    }

    private static bool IsEmojiRune(int value)
    {
        if (value is (>= 0x1F000 and <= 0x1FAFF)
            or (>= 0x2600 and <= 0x27BF)
            or (>= 0x2300 and <= 0x23FF)
            or (>= 0x2B00 and <= 0x2BFF)
            or (>= 0xFE00 and <= 0xFE0F)
            or (>= 0x1F1E6 and <= 0x1F1FF)
            or (>= 0xE0020 and <= 0xE007F)
            or (>= 0x2194 and <= 0x2199)
            or (>= 0x25AA and <= 0x25AB)
            or (>= 0x25FB and <= 0x25FE)
            or (>= 0x2763 and <= 0x2764))
            return true;

        return value is 0x00A9 or 0x00AE or 0x200D or 0x203C or 0x2049 or 0x20E3
            or 0x2122 or 0x2139 or 0x21A9 or 0x21AA or 0x2328 or 0x23CF
            or 0x24C2 or 0x25B6 or 0x25C0 or 0x2614 or 0x2615 or 0x2640 or 0x2642
            or 0x2660 or 0x2663 or 0x2665 or 0x2666 or 0x267B or 0x267F
            or 0x2693 or 0x26A1 or 0x26AA or 0x26AB or 0x26BD or 0x26BE
            or 0x2934 or 0x2935 or 0x2B50 or 0x2B55 or 0x3030 or 0x303D
            or 0x3297 or 0x3299;
    }

    private static bool IsKeyboardSmash(string token)
    {
        var start = 0;
        var end = token.Length;
        while (start < end && IsWrappingPunct(token[start]))
            start++;
        while (end > start && IsWrappingPunct(token[end - 1]))
            end--;
        if (end - start < 5)
            return false;

        var letterCount = 0;
        var kCount = 0;
        var smashHits = 0;
        var prefix = new char[2];
        var prefixLen = 0;
        for (var i = start; i < end; i++)
        {
            var ch = token[i];
            if (ch is '-' or '\'' or '_')
                continue;
            if (char.IsDigit(ch))
                return false;
            if (!IsSmashLetter(ch))
                return false;
            var lower = ch is >= 'A' and <= 'Z' ? (char)(ch + 32) : ch == 'Ç' ? 'ç' : ch;
            if (letterCount < 2)
                prefix[prefixLen++] = lower;
            letterCount++;
            if (lower == 'k')
                kCount++;
            if (lower is 'k' or 'p' or 'd' or 'f' or 'h')
                smashHits++;
        }

        if (letterCount < 5)
            return false;
        if (kCount >= letterCount - 1)
            return true;
        if (prefixLen == 2
            && ((prefix[0] == 'a' && prefix[1] is 's' or 'p' or 'u')
                || (prefix[0] == 's' && prefix[1] == 'k')
                || (prefix[0] == 'k' && prefix[1] == 's'))
            && letterCount - 2 >= 3
            && (kCount > 0 || smashHits >= 2))
            return true;
        return false;
    }

    private static bool IsSmashLetter(char ch)
    {
        var lower = ch is >= 'A' and <= 'Z' ? (char)(ch + 32) : ch;
        return lower is 'a' or 'u' or 'p' or 'o' or 'd' or 'k' or 's' or 'f' or 'h' or 'ç' or 'Ç';
    }

    private static bool IsWrappingPunct(char ch)
        => ch is '.' or ',' or '!' or '?' or ';' or ':' or '~' or '…' or '。' or '，' or '、' or '！' or '？' or '～' or '(' or ')' or '[' or ']' or '"' or '\'' or '“' or '”';
}
