using System;
using System.Collections.Generic;
using RSBot.Core;
using RSBot.Core.Client.ReferenceObjects;
using RSBot.Core.Objects;
using RSBot.Core.Objects.Inventory;

namespace RSBot.AutoEquip
{
    public static class EquipmentChecker
    {
        public enum ArmorType { Light, Heavy, Robe }

        public enum WeaponCategory
        {
            Any,
            OneHandedSword,
            TwoHandedSword,
            Dagger,
            Staff,
            Wand,
            Crossbow,
            Blade,
            Spear,
            Bow,
            Harp,
            DarkStaff,
        }

        public class UpgradeSuggestion
        {
            public byte Slot { get; set; }
            public InventoryItem CurrentItem { get; set; }
            public InventoryItem BetterItem { get; set; }
            public int CurrentDegree { get; set; }
            public int CurrentItemClass { get; set; }
            public int BetterDegree { get; set; }
            public int BetterItemClass { get; set; }
        }

        private static bool IsItemCompatible(RefObjItem item, ObjectCountry playerRace)
        {
            if (playerRace == ObjectCountry.Europe)
                return item.Country == ObjectCountry.Europe;
            if (playerRace == ObjectCountry.Chinese)
                return item.Country == ObjectCountry.Chinese || item.Country == 0;
            return item.Country == playerRace;
        }

        private static WeaponCategory GetIgnoredWeaponCategory()
        {
            string ignored = PlayerConfig.Get("AutoEquip.IgnoredWeaponCategory", "Any");
            return Enum.TryParse(ignored, out WeaponCategory cat) ? cat : WeaponCategory.Any;
        }

        // ------------------------------------------------------------
        //  ЕВРОПЕЕЦ
        // ------------------------------------------------------------
        private static List<UpgradeSuggestion> FindBetterEquipmentEuropean(WeaponCategory weaponCategory, WeaponCategory ignoredCategory)
        {
            var suggestions = new List<UpgradeSuggestion>();
            var inv = Game.Player.Inventory;
            if (inv == null) return suggestions;

            byte[] armorSlots = { 0, 1, 2, 3, 4, 5 };
            byte[] jewelrySlots = { 9, 10, 11, 12 };
            byte weaponSlot = 6;
            byte shieldSlot = 7;

            // ---- Оружие ----
            var currentWeapon = inv.GetItemAt(weaponSlot);
            if (currentWeapon?.Record != null && currentWeapon.Record.IsWeapon)
            {
                if (ignoredCategory == WeaponCategory.Any || GetWeaponCategoryEuropean(currentWeapon.Record) != ignoredCategory)
                {
                    var betterWeapon = FindBetterItem(inv, currentWeapon,
                        item => item.IsWeapon && GetWeaponCategoryEuropean(item) == weaponCategory &&
                                (ignoredCategory == WeaponCategory.Any || GetWeaponCategoryEuropean(item) != ignoredCategory));
                    if (betterWeapon != null)
                        suggestions.Add(CreateSuggestion(weaponSlot, currentWeapon, betterWeapon));
                }
            }
            else
            {
                var bestWeapon = FindBestWeaponEuropean(inv, weaponCategory, ignoredCategory);
                if (bestWeapon != null)
                    suggestions.Add(CreateSuggestion(weaponSlot, null, bestWeapon));
            }

            // ---- Щит (одноручное оружие) ----
            bool oneHanded = weaponCategory == WeaponCategory.OneHandedSword ||
                             weaponCategory == WeaponCategory.Dagger ||
                             weaponCategory == WeaponCategory.Wand ||
                             weaponCategory == WeaponCategory.DarkStaff;
            if (currentWeapon?.Record != null && IsWeaponOneHandedEuropean(currentWeapon.Record))
                oneHanded = true;

            if (oneHanded)
            {
                var currentShield = inv.GetItemAt(shieldSlot);
                if (currentShield?.Record != null && currentShield.Record.IsShield)
                {
                    var betterShield = FindBetterItem(inv, currentShield, item => item.IsShield);
                    if (betterShield != null)
                        suggestions.Add(CreateSuggestion(shieldSlot, currentShield, betterShield));
                }
                else if (currentShield == null)
                {
                    var bestShield = FindBestShieldEuropean(inv);
                    if (bestShield != null)
                        suggestions.Add(CreateSuggestion(shieldSlot, null, bestShield));
                }
            }

            // ---- Броня ----
            foreach (var slot in armorSlots)
            {
                var currentArmor = inv.GetItemAt(slot);
                if (currentArmor?.Record == null || !currentArmor.Record.IsArmor) continue;
                if (!IsItemCompatible(currentArmor.Record, ObjectCountry.Europe)) continue;

                var betterArmor = FindBetterItem(inv, currentArmor,
                    item => item.IsArmor && IsItemCompatible(item, ObjectCountry.Europe) && GetArmorSlot(item) == slot);
                if (betterArmor != null)
                    suggestions.Add(CreateSuggestion(slot, currentArmor, betterArmor));
            }

            // ---- Бижутерия ----
            foreach (var slot in jewelrySlots)
            {
                var currentJewel = inv.GetItemAt(slot);
                if (currentJewel?.Record == null || !currentJewel.Record.IsAccessory) continue;
                if (!IsItemCompatible(currentJewel.Record, ObjectCountry.Europe)) continue;

                byte targetType4 = currentJewel.Record.TypeID4;
                var betterJewel = FindBetterItem(inv, currentJewel,
                    item => item.IsAccessory && IsItemCompatible(item, ObjectCountry.Europe) && item.TypeID4 == targetType4);
                if (betterJewel != null)
                    suggestions.Add(CreateSuggestion(slot, currentJewel, betterJewel));
            }

            return suggestions;
        }

        private static WeaponCategory GetWeaponCategoryEuropean(RefObjItem item)
        {
            int tid4 = item.TypeID4;
            switch (tid4)
            {
                case 7: return WeaponCategory.OneHandedSword;
                case 8: return WeaponCategory.TwoHandedSword;
                case 13: return WeaponCategory.Dagger;
                case 11: return WeaponCategory.Staff;
                case 10: return WeaponCategory.DarkStaff;
                case 15: return WeaponCategory.Wand;
                case 12: return WeaponCategory.Crossbow;
                case 14: return WeaponCategory.Harp;
                case 6: return WeaponCategory.Bow;
                default: return WeaponCategory.Any;
            }
        }

        private static bool IsWeaponOneHandedEuropean(RefObjItem item)
        {
            int tid4 = item.TypeID4;
            return tid4 == 7 || tid4 == 13 || tid4 == 10 || tid4 == 15;
        }

        private static InventoryItem FindBestWeaponEuropean(InventoryItemCollection inventory, WeaponCategory category, WeaponCategory ignoredCategory)
        {
            InventoryItem best = null;
            int bestDegree = -1;
            int bestItemClass = -1;
            foreach (var item in inventory)
            {
                if (item.Slot < 12) continue;
                if (item.Record == null || !item.Record.IsWeapon) continue;
                if (GetWeaponCategoryEuropean(item.Record) != category) continue;
                if (ignoredCategory != WeaponCategory.Any && GetWeaponCategoryEuropean(item.Record) == ignoredCategory) continue;
                if (item.Record.Degree > bestDegree || (item.Record.Degree == bestDegree && item.Record.ItemClass > bestItemClass))
                {
                    bestDegree = item.Record.Degree;
                    bestItemClass = item.Record.ItemClass;
                    best = item;
                }
            }
            return best;
        }

        private static InventoryItem FindBestShieldEuropean(InventoryItemCollection inventory)
        {
            InventoryItem best = null;
            int bestDegree = -1;
            int bestItemClass = -1;
            foreach (var item in inventory)
            {
                if (item.Slot < 12) continue;
                if (item.Record == null || !item.Record.IsShield) continue;
                if (item.Record.Degree > bestDegree || (item.Record.Degree == bestDegree && item.Record.ItemClass > bestItemClass))
                {
                    bestDegree = item.Record.Degree;
                    bestItemClass = item.Record.ItemClass;
                    best = item;
                }
            }
            return best;
        }

        // ------------------------------------------------------------
        //  КИТАЕЦ
        // ------------------------------------------------------------
        private static List<UpgradeSuggestion> FindBetterEquipmentChinese(WeaponCategory weaponCategory, WeaponCategory ignoredCategory)
        {
            var suggestions = new List<UpgradeSuggestion>();
            var inv = Game.Player.Inventory;
            if (inv == null) return suggestions;

            byte[] armorSlots = { 0, 1, 2, 3, 4, 5 };
            byte[] jewelrySlots = { 9, 10, 11, 12 };
            byte weaponSlot = 6;
            byte shieldSlot = 7;

            // ---- Оружие ----
            var currentWeapon = inv.GetItemAt(weaponSlot);
            if (currentWeapon?.Record != null && currentWeapon.Record.IsWeapon)
            {
                if (ignoredCategory == WeaponCategory.Any || GetWeaponCategoryChinese(currentWeapon.Record) != ignoredCategory)
                {
                    var betterWeapon = FindBetterItem(inv, currentWeapon,
                        item => item.IsWeapon && GetWeaponCategoryChinese(item) == weaponCategory &&
                                (ignoredCategory == WeaponCategory.Any || GetWeaponCategoryChinese(item) != ignoredCategory));
                    if (betterWeapon != null)
                        suggestions.Add(CreateSuggestion(weaponSlot, currentWeapon, betterWeapon));
                }
            }
            else
            {
                var bestWeapon = FindBestWeaponChinese(inv, weaponCategory, ignoredCategory);
                if (bestWeapon != null)
                    suggestions.Add(CreateSuggestion(weaponSlot, null, bestWeapon));
            }

            // ---- Щит (одноручное: меч, клинок) ----
            bool oneHanded = weaponCategory == WeaponCategory.OneHandedSword ||
                             weaponCategory == WeaponCategory.Blade;
            if (currentWeapon?.Record != null && IsWeaponOneHandedChinese(currentWeapon.Record))
                oneHanded = true;

            if (oneHanded)
            {
                var currentShield = inv.GetItemAt(shieldSlot);
                if (currentShield?.Record != null && currentShield.Record.IsShield)
                {
                    var betterShield = FindBetterItem(inv, currentShield, item => item.IsShield);
                    if (betterShield != null)
                        suggestions.Add(CreateSuggestion(shieldSlot, currentShield, betterShield));
                }
                else if (currentShield == null)
                {
                    var bestShield = FindBestShieldChinese(inv);
                    if (bestShield != null)
                        suggestions.Add(CreateSuggestion(shieldSlot, null, bestShield));
                }
            }

            // ---- Броня ----
            foreach (var slot in armorSlots)
            {
                var currentArmor = inv.GetItemAt(slot);
                if (currentArmor?.Record == null || !currentArmor.Record.IsArmor) continue;
                if (!IsItemCompatible(currentArmor.Record, ObjectCountry.Chinese)) continue;

                var betterArmor = FindBetterItem(inv, currentArmor,
                    item => item.IsArmor && IsItemCompatible(item, ObjectCountry.Chinese) && GetArmorSlot(item) == slot);
                if (betterArmor != null)
                    suggestions.Add(CreateSuggestion(slot, currentArmor, betterArmor));
            }

            // ---- Бижутерия ----
            foreach (var slot in jewelrySlots)
            {
                var currentJewel = inv.GetItemAt(slot);
                if (currentJewel?.Record == null || !currentJewel.Record.IsAccessory) continue;
                if (!IsItemCompatible(currentJewel.Record, ObjectCountry.Chinese)) continue;

                byte targetType4 = currentJewel.Record.TypeID4;
                var betterJewel = FindBetterItem(inv, currentJewel,
                    item => item.IsAccessory && IsItemCompatible(item, ObjectCountry.Chinese) && item.TypeID4 == targetType4);
                if (betterJewel != null)
                    suggestions.Add(CreateSuggestion(slot, currentJewel, betterJewel));
            }

            return suggestions;
        }

        private static WeaponCategory GetWeaponCategoryChinese(RefObjItem item)
        {
            int tid4 = item.TypeID4;
            switch (tid4)
            {
                case 2: return WeaponCategory.OneHandedSword;
                case 3: return WeaponCategory.Blade;
                case 4: return WeaponCategory.Spear;
                case 5: return WeaponCategory.TwoHandedSword;
                case 6: return WeaponCategory.Bow;
                default: return WeaponCategory.Any;
            }
        }

        private static bool IsWeaponOneHandedChinese(RefObjItem item)
        {
            int tid4 = item.TypeID4;
            return tid4 == 2 || tid4 == 3;
        }

        private static InventoryItem FindBestWeaponChinese(InventoryItemCollection inventory, WeaponCategory category, WeaponCategory ignoredCategory)
        {
            InventoryItem best = null;
            int bestDegree = -1;
            int bestItemClass = -1;
            foreach (var item in inventory)
            {
                if (item.Slot < 12) continue;
                if (item.Record == null || !item.Record.IsWeapon) continue;
                if (GetWeaponCategoryChinese(item.Record) != category) continue;
                if (ignoredCategory != WeaponCategory.Any && GetWeaponCategoryChinese(item.Record) == ignoredCategory) continue;
                if (item.Record.Degree > bestDegree || (item.Record.Degree == bestDegree && item.Record.ItemClass > bestItemClass))
                {
                    bestDegree = item.Record.Degree;
                    bestItemClass = item.Record.ItemClass;
                    best = item;
                }
            }
            return best;
        }

        private static InventoryItem FindBestShieldChinese(InventoryItemCollection inventory)
        {
            InventoryItem best = null;
            int bestDegree = -1;
            int bestItemClass = -1;
            foreach (var item in inventory)
            {
                if (item.Slot < 12) continue;
                if (item.Record == null || !item.Record.IsShield) continue;
                if (item.Record.Degree > bestDegree || (item.Record.Degree == bestDegree && item.Record.ItemClass > bestItemClass))
                {
                    bestDegree = item.Record.Degree;
                    bestItemClass = item.Record.ItemClass;
                    best = item;
                }
            }
            return best;
        }

        // ------------------------------------------------------------
        //  ОБЩИЕ МЕТОДЫ
        // ------------------------------------------------------------
        public static List<UpgradeSuggestion> FindBetterEquipment(ArmorType armorType = ArmorType.Heavy,
            WeaponCategory weaponCategory = WeaponCategory.Any)
        {
            var playerRace = Game.Player.Race;
            var ignoredCategory = GetIgnoredWeaponCategory();

            if (playerRace == ObjectCountry.Europe)
                return FindBetterEquipmentEuropean(weaponCategory, ignoredCategory);
            else if (playerRace == ObjectCountry.Chinese)
                return FindBetterEquipmentChinese(weaponCategory, ignoredCategory);
            else
                return new List<UpgradeSuggestion>();
        }

        private static byte GetArmorSlot(RefObjItem item)
        {
            byte tid4 = item.TypeID4;
            switch (tid4)
            {
                case 1: return 0;
                case 3: return 1;
                case 2: return 2;
                case 5: return 3;
                case 4: return 4;
                case 6: return 5;
                default: return tid4;
            }
        }

        private static InventoryItem FindBetterItem(InventoryItemCollection inventory, InventoryItem current, Func<RefObjItem, bool> filter)
        {
            InventoryItem best = null;
            int bestDegree = current.Record.Degree;
            int bestItemClass = current.Record.ItemClass;

            foreach (var item in inventory)
            {
                if (item.Slot < 12) continue;
                if (item.Record == null || !filter(item.Record)) continue;

                if (item.Record.Degree > bestDegree ||
                    (item.Record.Degree == bestDegree && item.Record.ItemClass > bestItemClass))
                {
                    bestDegree = item.Record.Degree;
                    bestItemClass = item.Record.ItemClass;
                    best = item;
                }
            }
            return best;
        }

        private static UpgradeSuggestion CreateSuggestion(byte slot, InventoryItem current, InventoryItem better)
        {
            return new UpgradeSuggestion
            {
                Slot = slot,
                CurrentItem = current,
                BetterItem = better,
                CurrentDegree = current?.Record.Degree ?? -1,
                CurrentItemClass = current?.Record.ItemClass ?? -1,
                BetterDegree = better.Record.Degree,
                BetterItemClass = better.Record.ItemClass
            };
        }

        public static bool EquipSuggestion(UpgradeSuggestion suggestion, int maxRetries = 1)
        {
            var newItem = suggestion.BetterItem;
            if (newItem == null || newItem.Record == null) return false;
            if (!IsItemCompatible(newItem.Record, Game.Player.Race)) return false;
            if (!newItem.CanBeEquipped()) return false;

            if (suggestion.CurrentItem != null && suggestion.CurrentItem.Record == newItem.Record)
                return false;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                if (newItem.Equip(suggestion.Slot))
                {
                    Log.Status($"[AutoEquip] Equipped {newItem.Record.GetRealName()} to slot {suggestion.Slot}");
                    return true;
                }
                if (attempt < maxRetries)
                    System.Threading.Thread.Sleep(1000);
            }
            Log.Warn($"[AutoEquip] Failed to equip {newItem.Record.GetRealName()} after {maxRetries} attempt(s)");
            return false;
        }
    }
}
