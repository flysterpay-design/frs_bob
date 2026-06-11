using System;
using System.Threading;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.Core.Objects;
using RSBot.Training.Bundle;

namespace RSBot.Training.Bot;

internal class Botbase
{
    private bool _transportUsedThisTrip = false;   // использовали ли транспорт в текущей поездке
    private bool _arrivedAtDestination = false;    // прибыли ли на точку кача
    private DateTime? _dismountTimer = null;   // таймер для отложенного слезания
    public static Botbase Current { get; private set; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Botbase" /> class.
    /// </summary>
    public Botbase()
    {
        Current = this;
        EventManager.SubscribeEvent("OnSetTrainingArea", Reload);
    }

    /// <summary>
    ///     Gets the area.
    /// </summary>
    /// <value>
    ///     The area.
    /// </value>
    public Area Area { get; private set; }

    /// <summary>
    ///     Reloads this instance by re-reading the configuration.
    /// </summary>
    public void Reload()
    {
        Area = new Area
        {
            Position = new Position(
                PlayerConfig.Get<ushort>("RSBot.Area.Region"),
                PlayerConfig.Get<float>("RSBot.Area.X"),
                PlayerConfig.Get<float>("RSBot.Area.Y"),
                PlayerConfig.Get<float>("RSBot.Area.Z")
            ),
            Radius = Math.Clamp(PlayerConfig.Get("RSBot.Area.Radius", 50), 5, 100),
        };
    }

    /// <summary>
    ///     Resets the transport state for a new trip (e.g., after returning to town).
    /// </summary>
    public void ResetTransportState()
    {
        _transportUsedThisTrip = false;
        _arrivedAtDestination = false;
        _dismountTimer = null;
        Log.Debug("[Transport] State reset for new trip.");
    }

    /// <summary>
    ///     Ticks this instance.
    /// </summary>
    public void Tick()
    {
        if (!Kernel.Bot.Running)
            return;

        //Wait for the pickup manager to finish
        if (PickupManager.RunningPlayerPickup)
            return;

        if (
            Bundles.Loop.Config.UseSpeedDrug
            && Game.Player.State.ActiveBuffs.FindIndex(p => p.Record.Params.Contains(1752396901)) < 0
        )
        {
            var item = Game.Player.Inventory.GetItem(
                new TypeIdFilter(3, 3, 13, 1),
                p => p.Record.Desc1.Contains("_SPEED_")
            );
            item?.Use();
        }

        var noAttack = PlayerConfig.Get("RSBot.Skills.checkBoxNoAttack", false);

        //Check for protection
        Bundles.Protection.Invoke();

        //Resurrect party members if needed
        Bundles.Resurrect.Invoke();

        //Cast buffs
        Bundles.Buff.Invoke();

        // Buff the configured party members if needed
        Bundles.PartyBuff.Invoke();

        //Loot items
        Bundles.Loot.Invoke();

        //Select next target
        if (!noAttack)
            Bundles.Target.Invoke();

        //Check for berzerk
        Bundles.Berzerk.Invoke();

        //Cast skill against enemy
        if (!noAttack)
            Bundles.Attack.Invoke();

        // --- ДВИЖЕНИЕ (обязательно) ---
        Bundles.Movement.Invoke();

        // --- Логика управления транспортом после движения ---
        if (Game.Player.HasActiveVehicle && !_transportUsedThisTrip && !_arrivedAtDestination)
        {
            _transportUsedThisTrip = true;
            Log.Debug("[Transport] Vehicle mounted for this trip.");
        }

        // Если транспорт активен и мы ещё не прибыли
        if (Game.Player.HasActiveVehicle && _transportUsedThisTrip && !_arrivedAtDestination)
        {
            double distanceToTrainingSpot = Game.Player.Position.DistanceTo(Area.Position);

            // Если мы достаточно близко к месту кача (радиус 70)
            if (distanceToTrainingSpot < 70.0)
            {
                // Запускаем таймер, если ещё не запущен
                if (_dismountTimer == null)
                {
                    _dismountTimer = DateTime.Now.AddSeconds(2);
                    Log.Debug("[Transport] Arriving, will dismount in 2 seconds...");
                }
                // Если таймер истёк – слезаем
                else if (DateTime.Now >= _dismountTimer)
                {
                    Game.Player.Vehicle.Dismount();
                    Thread.Sleep(500);
                    _arrivedAtDestination = true;
                    _transportUsedThisTrip = false;
                    _dismountTimer = null;
                    Log.Debug("[Transport] Dismounted after reaching training spot.");
                }
            }
            else
            {
                // Если отошли от зоны – сбрасываем таймер
                _dismountTimer = null;
            }
        }
        else
        {
            // Если транспорт не активен или мы уже прибыли – сбрасываем таймер
            _dismountTimer = null;
        }
    }
}
