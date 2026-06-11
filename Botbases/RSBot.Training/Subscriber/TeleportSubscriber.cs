using RSBot.Core;
using RSBot.Core.Event;
using RSBot.Training.Bundle;

namespace RSBot.Training.Subscriber;

internal class TeleportSubscriber
{
    public static void SubscribeEvents()
    {
        EventManager.SubscribeEvent("OnTeleportStart", OnTeleportStart);
        EventManager.SubscribeEvent("OnTeleportComplete", OnTeleportComplete);
    }

    private static void OnTeleportStart()
    {
        // Просто останавливаем цикл движения, если он активен
        if (Bundles.Loop.Running)
            Bundles.Loop.Stop();
    }

    private static void OnTeleportComplete()
    {
        // Ничего не делаем, основная логика теперь в TrainingBase.Tick
    }
}
