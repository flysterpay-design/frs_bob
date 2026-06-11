using System.Windows.Forms;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.Core.Objects;
using RSBot.Core.Plugins;
using RSBot.Lure.Bundle;
using RSBot.Lure.Components;

namespace RSBot.Lure;

public class LureBotbase : IBotbase
{
    private bool _interrupted;
    public string Name => "RSBot.Lure";
    public Area Area => LureConfig.Area;

    public void Tick()
    {
        if (!Kernel.Bot.Running)
            return;

        if (PickupManager.RunningPlayerPickup)
            return;

        // Слезаем с лошади при приближении к центру (расстояние ≤ 30)
        Game.Player.EnsureDismount(Area.Position, 30);

        // Если далеко от центра (>100) – запускаем процесс возврата (городской скрипт, закупки, гильд-золото, скрипт возврата)
        if (Area.Position.DistanceToPlayer() > 100)
        {
            // Не мешаем, если скрипт уже выполняется
            if (ScriptManager.Running)
                return;

            // Запускаем городской цикл (скрипт города, закупки, гильд-золото, скрипт возврата)
            if (!ScriptManager.Running)
                EventManager.FireEvent("Bundle.Loop.Start");

            EventManager.FireEvent("Bundle.Loop.Invoke");
            return;
        }

        // Если выполняется скрипт (например, скрипт лура) – не делаем баффы и не проверяем условия
        if (ScriptManager.Running)
            return;

        // --- Персонаж в зоне, скрипт не активен – обновляем баффы и проверяем условия ---
        EventManager.FireEvent("Bundle.Resurrect.Invoke");
        EventManager.FireEvent("Bundle.Buff.Invoke");
        EventManager.FireEvent("Bundle.PartyBuffing.Invoke");

        var interruptMessage = LoopConditionValidator.CheckLoopConditions();
        if (interruptMessage != null)
        {
            ScriptManager.Stop();

            if (LureConfig.Area.Position.DistanceToPlayer() > 2)
                Game.Player.MoveTo(LureConfig.Area.Position);

            if (!_interrupted)
                Log.Warn(interruptMessage);

            _interrupted = true;
            return;
        }
        _interrupted = false;

        // --- Запускаем лур: спелл, цель, атака, движение ---
        if (Game.Player.HasActiveVehicle)
            Game.Player.Vehicle.Dismount(); // дополнительная страховка

        if (LureConfig.UseHowlingShout)
            HowlingShoutBundle.Tick();

        TargetBundle.Tick();
        AttackBundle.Tick();
        MovementBundle.Tick();

        if (!PickupManager.RunningPlayerPickup && !PickupManager.RunningAbilityPetPickup)
            EventManager.FireEvent("Bundle.Loot.Invoke");
    }

    public void Start()
    {
        Log.Notify("[Lure] bot started!");
    }

    public void Stop()
    {
        EventManager.FireEvent("Bundle.Loop.Stop");
        EventManager.FireEvent("Bundle.Loot.Stop");
        EventManager.FireEvent("Bundle.PartyBuffing.Stop");
        EventManager.FireEvent("Bundle.Buff.Stop");

        Log.Notify("[Lure] bot stopped!");
    }

    public void Register()
    {
        Log.Debug("[Lure] Botbase registered to the kernel!");
    }
}
