using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Objects;
using RSBot.Core.Plugins;
using RSBot.Training.Bot;
using RSBot.Training.Bundle;
using RSBot.Training.Components;
using RSBot.Training.Subscriber;
using RSBot.AutoEquip;
using System;

namespace RSBot.Training
{
    public class TrainingBase : IBotbase
    {
        public TrainingManager Manager { get; private set; }
        public string Name => "RSBot.Training";
        public Area Area => Container.Bot.Area;

        public void Tick()
        {
            if (!Kernel.Bot.Running)
                return;

            if (Game.Player.Exchanging)
                return;

            if (Game.Player.Untouchable)
                return;

            if (Game.Player.State.LifeState == LifeState.Dead)
                return;

            // Слезаем с лошади при приближении к центру тренировочной зоны
            Game.Player.EnsureDismount(Container.Bot.Area.Position, 30);

            //Begin the loopback if needed
            if (Container.Bot.Area.Position.DistanceToPlayer() > 80)
                Bundles.Loop.Start();

            if (Bundles.Loop.Running)
                return;

            //Nothing if in scroll state!
            if (
                Game.Player.State.ScrollState == ScrollState.NormalScroll
                || Game.Player.State.ScrollState == ScrollState.ThiefScroll
            )
                return;

            try
            {
                Container.Bot.Tick();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex);
            }
        }

        public void Start()
        {
            if (Kernel.Bot.Botbase.Area.Position.X == 0)
            {
                Log.WarnLang("ConfigureTrainingAreaBeforeStartBot");
                Kernel.Bot.Stop();
            }
        }

        public void Stop()
        {
            lock (Container.Lock)
            {
                if (Game.Player.InAction)
                    SkillManager.CancelAction();

                Bundles.Stop();
            }
        }

        public void Register()
        {
            Manager = new TrainingManager();
            Container.Lock = new object();
            Container.Bot = new Botbase();

            BundleSubscriber.SubscribeEvents();
            ConfigSubscriber.SubscribeEvents();
            TeleportSubscriber.SubscribeEvents();

            AutoEquipManager.Initialize();
            GuildGoldManager.Initialize();

            ScriptManager.CommandHandlers.Add(new TrainingAreaScriptCommand());
            Log.Debug("[Training] Botbase registered to the kernel!");
        }
    }
}
