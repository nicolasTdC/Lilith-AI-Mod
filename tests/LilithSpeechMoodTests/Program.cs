using LilithTextInjector;

Assert(SpeechMood.Resolve("oi", "Calm") == SpeechMood.Calm, "Neutral chat stays calm.");
Assert(SpeechMood.Resolve("perfeitommm!!! da o play ai bae", "Calm") == SpeechMood.Excited, "Hype reply is excited.");
Assert(SpeechMood.Resolve("to triste t-t", "Calm") == SpeechMood.Sad, "Sad reply is fragile.");
Assert(SpeechMood.Resolve("oi", "Sleepy") == SpeechMood.Sleepy, "Sleepy pose wins.");
Assert(SpeechMood.Resolve("to mal", "Excited") == SpeechMood.Sad, "Hard sad text beats a weak excited pose.");
Assert(SpeechMood.Resolve("tá bom amor!!! t-t come seu sanduíche kkkkk s2", "Calm") == SpeechMood.Excited,
    "t-t must not override an excited reply.");
Assert(SpeechMood.Resolve("to triste t-t saudade", "Calm") == SpeechMood.Sad,
    "Hard sad plus t-t stays sad.");

Console.WriteLine("Lilith speech mood tests passed (7 assertions).");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
