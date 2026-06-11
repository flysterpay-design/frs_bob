using System.IO;
using System.Threading;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Objects;
using RSBot.Training.Bot;      // <-- ДОБАВЛЕНО для доступа к Botbase
using RSBot.AutoEquip;

namespace RSBot.Training.Bundle.Loop;

internal class LoopBundle : IBundle
{
    /// <summary>
    ///     Gets the configuration.
    /// </summary>
    /// <value>
    ///     The configuration.
    /// </value>
    public LoopConfig Config { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether this <see cref="LoopBundle" /> is running.
    /// </summary>
    /// <value>
    ///     <c>true</c> if running; otherwise, <c>false</c>.
    /// </value>
    public bool Running { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether [townscript running].
    /// </summary>
    /// <value>
    ///     <c>true</c> if [townscript running]; otherwise, <c>false</c>.
    /// </value>
    public bool TownscriptRunning { get; private set; }

    /// <summary>
    ///     Invokes this instance.
    /// </summary>
    public void Invoke()
    {
        if (!Running)
            return;

        if (Config.UseVehicle && !Game.Player.HasActiveVehicle && !Game.Player.IsInDungeon)
        {
            Game.Player.SummonVehicle();

            //Wait for the vehicle to spawn
            Thread.Sleep(1000);
        }

        //We don't need to use buffs in town...
        if (Config.CastBuffs && !TownscriptRunning)
            Bundles.Buff.Invoke();
    }

    /// <summary>
    ///     Refreshes this instance.
    /// </summary>
    public void Refresh()
    {
        Config = new LoopConfig
        {
            WalkScript = PlayerConfig.Get<string>("RSBot.Walkback.File"),
            UseSpeedDrug = PlayerConfig.Get<bool>("RSBot.Training.checkUseSpeedDrug", true),
            UseVehicle = PlayerConfig.Get<bool>("RSBot.Training.checkUseMount", true),
            CastBuffs = PlayerConfig.Get<bool>("RSBot.Training.checkCastBuffs", true),
            UseReverse = PlayerConfig.Get<bool>("RSBot.Training.checkBoxUseReverse", false),
        };
    }

    public void Stop()
    {
        if (ScriptManager.Running)
            ScriptManager.Stop();

        if (ShoppingManager.Running)
            ShoppingManager.Stop();

        Running = false;
    }

    /// <summary>
    ///     Starts this instance.
    /// </summary>
    public void Start()
    {
        Running = true;

        Refresh();
        CheckForTownScript();

        Running = false;
    }

    /// <summary>
    ///     Checks for town script.
    /// </summary>
    /// <summary>
    ///     Checks for town script.
    /// </summary>
    public void CheckForTownScript()
    {
        if (ScriptManager.Running)
            return;

        var filename = Path.Combine(
            ScriptManager.InitialDirectory,
            "Towns",
            Game.Player.Movement.Source.Region + ".rbs"
        );

        if (!File.Exists(filename))
        {
            CheckForWalkbackScript();
            return;
        }

        if (PlayerConfig.Get<bool>("RSBot.Protection.checkStopBotOnReturnToTown"))
        {
            Kernel.Bot.Stop();
            return;
        }

        Log.NotifyLang("LoadingTownScript", filename);

        TownscriptRunning = true;
        ScriptManager.IsTownScriptRunning = true; // Установка флага

        int retryCount = 0;
        const int maxRetries = 3;
        bool success = false;

        while (retryCount < maxRetries && !success)
        {
            ScriptManager.Load(filename);
            ScriptManager.RunScript(false);

            if (!ScriptManager.Running)
                success = true;
            else
            {
                retryCount++;
                Log.Warn($"Town script execution issue, retry {retryCount}/{maxRetries}");
                ScriptManager.Stop(true);
                Thread.Sleep(2000);
            }
        }

        ScriptManager.IsTownScriptRunning = false; // Сброс флага

        if (!success)
        {
            Log.Error("Town script failed after multiple retries. Stopping bot.");
            TownscriptRunning = false;
            Kernel.Bot.Stop();
            return;
        }

        if (Running && Config.UseReverse)
        {
            var filter = new TypeIdFilter(3, 3, 3, 3);
            var item = Game.Player.Inventory.GetItem(filter);
            if (item != null)
                if (item.UseTo(3))
                {
                    TownscriptRunning = false;
                    return;
                }
        }

        TownscriptRunning = false;

        // Сброс флага использования гильдейского склада (чтобы в следующий заход он снова был доступен)
        ShoppingManager.ResetGuildStorageUsedFlag();

        GuildGoldManager.ResetTownFlag();
        Botbase.Current?.ResetTransportState();

        Invoke();
        CheckForWalkbackScript(true);
    }

    /// <summary>
    ///     Checks for walkback script.
    /// </summary>
    public void CheckForWalkbackScript(bool startFromTown = false)
    {
        if (ScriptManager.Running || !Kernel.Bot.Running)
            return;

        if (Config.WalkScript == null || !File.Exists(Config.WalkScript))
        {
            Log.Notify("No walkback script found. Attempting to generate a dynamic path...");
            if (NavigationManager.CalculatePathToTrainingArea())
            {
                var dynamicScript = NavigationManager.GenerateRBSFile();
                if (dynamicScript != null)
                {
                    Config.WalkScript = dynamicScript;
                }
            }
        }

        if (Config.WalkScript == null || !File.Exists(Config.WalkScript))
            return;

        Invoke();
        Log.NotifyLang("LoadingWalkScript", Config.WalkScript);

        ScriptManager.Load(Config.WalkScript);
        ScriptManager.RunScript(!startFromTown);
    }
}
