using System;
using System.Threading;
using System.Threading.Tasks;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Event;

namespace RSBot.AutoEquip
{
    public static class AutoEquipManager
    {
        private static CancellationTokenSource _debounceCts;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;

            EventManager.SubscribeEvent("OnInventoryUpdate", OnInventoryUpdate);
            EventManager.SubscribeEvent("OnItemPickup", OnItemPickup);
            EventManager.SubscribeEvent("OnGainItem", OnGainItem);

            _initialized = true;
        }

        private static void OnInventoryUpdate()
        {
            if (!Kernel.Bot.Running) return;
            if (ScriptManager.IsTownScriptRunning) return;
            ScheduleCheck();
        }

        private static void OnItemPickup()
        {
            if (!Kernel.Bot.Running) return;
            if (ScriptManager.IsTownScriptRunning) return;
            ScheduleCheck();
        }

        private static void OnGainItem()
        {
            if (!Kernel.Bot.Running) return;
            if (ScriptManager.IsTownScriptRunning) return;
            ScheduleCheck();
        }

        private static void ScheduleCheck()
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1000, token);
                    if (!token.IsCancellationRequested)
                    {
                        await CheckAndEquipAsync();
                    }
                }
                catch (OperationCanceledException) { }
            }, token);
        }

        private static async Task CheckAndEquipAsync()
        {
            if (!Kernel.Bot.Running) return;
            if (ScriptManager.IsTownScriptRunning) return;

            string weaponStr = PlayerConfig.Get("AutoEquip.WeaponType", "Any");
            var weaponCategory = EquipmentChecker.WeaponCategory.Any;
            if (weaponStr != "Any")
                weaponCategory = weaponStr switch
                {
                    "One‑Handed Sword" => EquipmentChecker.WeaponCategory.OneHandedSword,
                    "Two‑Handed Sword" => EquipmentChecker.WeaponCategory.TwoHandedSword,
                    "Dagger" => EquipmentChecker.WeaponCategory.Dagger,
                    "Staff" => EquipmentChecker.WeaponCategory.Staff,
                    "Wand" => EquipmentChecker.WeaponCategory.Wand,
                    "Crossbow" => EquipmentChecker.WeaponCategory.Crossbow,
                    "Blade" => EquipmentChecker.WeaponCategory.Blade,
                    "Spear" => EquipmentChecker.WeaponCategory.Spear,
                    "Bow" => EquipmentChecker.WeaponCategory.Bow,
                    "Harp" => EquipmentChecker.WeaponCategory.Harp,
                    "DarkStaff" => EquipmentChecker.WeaponCategory.DarkStaff,
                    "Одноручный меч" => EquipmentChecker.WeaponCategory.OneHandedSword,
                    "Двуручный меч" => EquipmentChecker.WeaponCategory.TwoHandedSword,
                    "Кинжал" => EquipmentChecker.WeaponCategory.Dagger,
                    "Посох" => EquipmentChecker.WeaponCategory.Staff,
                    "Жезл" => EquipmentChecker.WeaponCategory.Wand,
                    "Арбалет" => EquipmentChecker.WeaponCategory.Crossbow,
                    "Клинок" => EquipmentChecker.WeaponCategory.Blade,
                    "Копьё" => EquipmentChecker.WeaponCategory.Spear,
                    "Лук" => EquipmentChecker.WeaponCategory.Bow,
                    "Арфа" => EquipmentChecker.WeaponCategory.Harp,
                    "Тёмный посох" => EquipmentChecker.WeaponCategory.DarkStaff,
                    _ => EquipmentChecker.WeaponCategory.Any
                };

            var suggestions = EquipmentChecker.FindBetterEquipment(EquipmentChecker.ArmorType.Heavy, weaponCategory);
            foreach (var sug in suggestions)
            {
                EquipmentChecker.EquipSuggestion(sug);
                await Task.Delay(1500);
            }
        }
    }
}
