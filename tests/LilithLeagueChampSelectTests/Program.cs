using LilithTextInjector;

Assert(ScreenLook.LooksLikeLeagueRequest("me sugere um champ no mid"), "Portuguese champ ask.");
Assert(ScreenLook.LooksLikeLeagueRequest("qual campeão eu pego de adc?"), "Portuguese ADC ask.");
Assert(ScreenLook.LooksLikeLeagueRequest("help me pick in champ select"), "English champ select.");
Assert(ScreenLook.LooksLikeLeagueRequest("what champion should I play jungle this patch"), "English jungle ask.");
Assert(ScreenLook.LooksLikeLeagueRequest("lol draft, quem eu banho?"), "Draft/ban ask.");
Assert(!ScreenLook.LooksLikeLeagueRequest("oi gatinho"), "Casual chat is not draft help.");
Assert(!ScreenLook.LooksLikeLeagueRequest("vamos assistir netflix"), "Watch-together is not draft help.");

Assert(ScreenLook.LooksLikeLookRequest("olha isso"), "Portuguese look at this.");
Assert(ScreenLook.ShouldCapture("Gabi, eu fiz um negócio. Vê se você consegue ver minha tela."),
    "Ask to see my screen captures.");
Assert(ScreenLook.LooksLikeLookRequest("consegue ver a tela"), "Consegue ver a tela.");
Assert(ScreenLook.LooksLikeLookRequest("vê o que tem na tela"), "Portuguese what's on screen.");
Assert(ScreenLook.LooksLikeLookRequest("look at my screen"), "English look at screen.");
Assert(ScreenLook.LooksLikeLookRequest("what do you see"), "English what do you see.");
Assert(ScreenLook.ShouldCapture("analisa o erro"), "Analyze this captures.");
Assert(!ScreenLook.LooksLikeLookRequest("olha eu te amo"), "Interjection olha is not a look request.");
Assert(!ScreenLook.ShouldWebSearch("olha isso"), "General look does not force web search.");
Assert(ScreenLook.ShouldWebSearch("me sugere um champ"), "League help still searches.");
Assert(ScreenLook.ToolName == "look_at_screen", "Stable tool name.");

Console.WriteLine("Lilith screen-look tests passed (18 assertions).");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
