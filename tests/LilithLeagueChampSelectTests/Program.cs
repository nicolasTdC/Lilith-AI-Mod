using LilithTextInjector;

Assert(LeagueChampSelect.LooksLikeRequest("me sugere um champ no mid"), "Portuguese champ ask.");
Assert(LeagueChampSelect.LooksLikeRequest("qual campeão eu pego de adc?"), "Portuguese ADC ask.");
Assert(LeagueChampSelect.LooksLikeRequest("help me pick in champ select"), "English champ select.");
Assert(LeagueChampSelect.LooksLikeRequest("what champion should I play jungle this patch"), "English jungle ask.");
Assert(LeagueChampSelect.LooksLikeRequest("lol draft, quem eu banho?"), "Draft/ban ask.");
Assert(!LeagueChampSelect.LooksLikeRequest("oi gatinho"), "Casual chat is not draft help.");
Assert(!LeagueChampSelect.LooksLikeRequest("vamos assistir netflix"), "Watch-together is not draft help.");
Assert(LeagueChampSelect.ToolName == "league_champ_select", "Stable tool name.");

Console.WriteLine("Lilith league champ-select tests passed (8 assertions).");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
