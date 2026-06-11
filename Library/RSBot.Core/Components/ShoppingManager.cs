using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml;
using RSBot.Core.Client.ReferenceObjects;
using RSBot.Core.Event;
using RSBot.Core.Network;
using RSBot.Core.Objects;
using RSBot.Core.Objects.Cos;
using RSBot.Core.Objects.Inventory;
using RSBot.Core.Objects.Spawn;
using static RSBot.Core.Game;

namespace RSBot.Core.Components;

public static class ShoppingManager
{
    public static Dictionary<RefShopGood, int> ShoppingList { get; set; }
    public static bool Finished { get; private set; }
    public static bool Enabled { get; set; }
    public static bool RepairGear { get; set; }
    public static List<string> SellFilter { get; set; }
    public static List<string> StoreFilter { get; set; }
    public static bool Running { get; set; }
    public static bool SellPetItems { get; set; }
    public static bool StorePetItems { get; set; }
    internal static Dictionary<byte, InventoryItem> BuybackList { get; set; }
    public static bool IsGuildStorageOpen { get; private set; }
    public static uint LastGuildStorageNpcId { get; private set; }

    private static bool _guildStorageUsedThisTown = false;
    public static void ResetGuildStorageUsedFlag() => _guildStorageUsedThisTown = false;

    /// <summary>
    /// Получает название гильдии текущего игрока через поиск в спавнах.
    /// </summary>
    private static string GetPlayerGuildName()
    {
        if (SpawnManager.TryGetEntity<SpawnedPlayer>(p => p.UniqueId == Game.Player.UniqueId, out var self))
            return self.Guild?.Name ?? string.Empty;
        return string.Empty;
    }

    /// <summary>
    /// Проверяет, есть ли другой член гильдии в радиусе.
    /// </summary>
    public static bool IsAnyGuildMemberNearby(float radius = 40)
    {
        string myGuild = GetPlayerGuildName();
        if (string.IsNullOrEmpty(myGuild))
            return false;

        return SpawnManager.TryGetEntities<SpawnedPlayer>(p =>
            p.UniqueId != Game.Player.UniqueId &&
            p.Guild?.Name == myGuild &&
            Game.Player.Position.DistanceTo(p.Position) <= radius,
            out _);
    }

    internal static void Initialize()
    {
        ShoppingList = new Dictionary<RefShopGood, int>();
        StoreFilter = new List<string>();
        SellFilter = new List<string>();
        BuybackList = new Dictionary<byte, InventoryItem>();
        Log.Debug("Initialized [ShoppingManager]!");
    }

    public static void Run(string npcCodeName)
    {
        if (!Enabled)
            return;

        Finished = false;
        Running = true;

        SelectNPC(npcCodeName);

        Log.Status("Selling items");

        var tempItemSellList = Game.Player.Inventory.GetNormalPartItems(item =>
            SellFilter.Any(p => p == item.Record.CodeName)
        );

        foreach (var item in tempItemSellList)
            SellItem(item);

        if (Game.Player.HasActiveAbilityPet && SellPetItems)
        {
            tempItemSellList = Game.Player.AbilityPet.Inventory.GetItems(item =>
                SellFilter.Any(p => p == item.Record.CodeName)
            );

            foreach (var item in tempItemSellList)
            {
                var playerSlot = Game.Player.AbilityPet.MoveItemToPlayer(item.Slot);
                if (playerSlot != 0xFF)
                    SellItem(Game.Player.Inventory.GetItemAt(playerSlot));
            }
        }

        var shopGroup = ReferenceManager.GetRefShopGroup(npcCodeName);
        if (shopGroup == null)
        {
            Log.Warn("Could not buy anything from this NPC - It's not a shop!");
            CloseShop();
            Finished = true;
            Running = false;
            return;
        }

        var shopGoods = ReferenceManager.GetRefShopGoods(shopGroup);

        foreach (var item in ShoppingList)
        {
            if (!Running)
                return;

            var actualItem = shopGoods.FirstOrDefault(x => x.RefPackageItemCodeName == item.Key.RefPackageItemCodeName);
            if (actualItem == null)
                continue;

            var tabIndex = ReferenceManager.GetRefShopGoodTabIndex(npcCodeName, actualItem);
            if (tabIndex == 0xFF)
                continue;

            var refPackageItem = ReferenceManager.GetRefPackageItem(item.Key.RefPackageItemCodeName);
            var holdingAmount = Game.Player.Inventory.GetSumAmount(refPackageItem.RefItemCodeName);
            var totalAmountToBuy = item.Value - holdingAmount;
            var refItem = ReferenceManager.GetRefItem(refPackageItem.RefItemCodeName);
            if (refItem == null)
                continue;

            Log.Status("Buying items");

            while (totalAmountToBuy > 0 && !Game.Player.Inventory.Full)
            {
                var amountStep = totalAmountToBuy;
                if (totalAmountToBuy >= refItem.MaxStack)
                    amountStep = refItem.MaxStack;

                PurchaseItem(tabIndex, actualItem.SlotIndex, (ushort)amountStep);
                totalAmountToBuy -= amountStep;
                Thread.Sleep(500);
            }

            if (refItem.MaxStack > 1)
            {
                IList<InventoryItem> getItems()
                {
                    return Game.Player.Inventory.GetItems(i =>
                        i.Record.CodeName == refPackageItem.RefItemCodeName && i.Amount < refItem.MaxStack);
                }

                var nonFullStacks = getItems();
                while (nonFullStacks.Count >= 2)
                {
                    Game.Player.Inventory.MoveItem(
                        nonFullStacks[1].Slot,
                        nonFullStacks[0].Slot,
                        (ushort)Math.Min(refItem.MaxStack - nonFullStacks[0].Amount, nonFullStacks[1].Amount));
                    nonFullStacks = getItems();
                    Thread.Sleep(500);
                }
            }
        }

        CloseShop();
        Finished = true;
        Running = false;
    }

    public static void SellItem(InventoryItem item, SpawnedBionic cos = null)
    {
        if (SelectedEntity == null)
            return;

        var packet = new Packet(0x7034);
        packet.WriteByte(cos == null ? InventoryOperation.SP_SELL_ITEM : InventoryOperation.SP_SELL_ITEM_COS);
        if (cos != null)
            packet.WriteUInt(cos.UniqueId);

        packet.WriteByte(item.Slot);
        packet.WriteUShort(item.Amount);
        packet.WriteUInt(SelectedEntity.UniqueId);

        var awaitResult = new AwaitCallback(null, 0xB034);
        PacketManager.SendPacket(packet, PacketDestination.Server, awaitResult);
        awaitResult.AwaitResponse();

        Log.Debug("[Shopping manager] - Sold item: " + item.Record.GetRealName());
    }

    public static void PurchaseItem(int tab, int slot, ushort amount)
    {
        if (SelectedEntity == null)
        {
            Log.Debug("Cannot buy items, because no shop is selected!");
            return;
        }

        var packet = new Packet(0x7034);
        packet.WriteByte(InventoryOperation.SP_BUY_ITEM);
        packet.WriteByte(tab);
        packet.WriteByte(slot);
        packet.WriteUShort(amount);
        packet.WriteUInt(SelectedEntity.UniqueId);

        var awaitResult = new AwaitCallback(null, 0xB034);
        PacketManager.SendPacket(packet, PacketDestination.Server, awaitResult);
        awaitResult.AwaitResponse();

        EventManager.FireEvent("OnGainItem");
    }

    public static void PurchaseItem(Cos transport, int tab, int slot, ushort amount)
    {
        if (SelectedEntity == null)
        {
            Log.Debug("Cannot buy items, because no shop is selected!");
            return;
        }

        var packet = new Packet(0x7034);
        packet.WriteByte(InventoryOperation.SP_BUY_ITEM_COS);
        packet.WriteUInt(0);
        packet.WriteByte(tab);
        packet.WriteByte(slot);
        packet.WriteUShort(amount);
        packet.WriteUInt(SelectedEntity.UniqueId);

        var awaitResult = new AwaitCallback(null, 0xB034);
        PacketManager.SendPacket(packet, PacketDestination.Server, awaitResult);
        awaitResult.AwaitResponse();

        EventManager.FireEvent("OnGainItem");
    }

    public static void ReceiveSupplies(string npcCodeName)
    {
        Finished = false;
        Running = true;

        uint questId = GetQuestId(npcCodeName);
        CloseShop();

        var currentWeapon = Game.Player.Weapon;
        var excludedItemCodeNames = new List<string>();

        if (currentWeapon.Record.TypeID4 == 6)
            excludedItemCodeNames.Add("ITEM_ETC_LEVEL_BOLT");
        else if (currentWeapon.Record.TypeID4 == 12)
            excludedItemCodeNames.Add("ITEM_ETC_LEVEL_ARROW");
        else
            excludedItemCodeNames.AddRange(["ITEM_ETC_LEVEL_ARROW", "ITEM_ETC_LEVEL_BOLT"]);

        var items = ReferenceManager
            .GetEventRewardItems(questId)
            .Where(r =>
                Game.Player.Level >= r.MinRequiredLevel
                && Game.Player.Level <= r.MaxRequiredLevel
                && !excludedItemCodeNames.Contains(r.ItemCodeName));

        foreach (var item in items)
        {
            ReceiveQuestReward(npcCodeName, questId, item.Item.ID);
        }

        Finished = true;
        Running = false;
    }

    public static uint GetQuestId(string npcCodeName)
    {
        ChooseTalkOption(npcCodeName, TalkOption.Quest);

        var packet = new Packet(0x30D4);
        packet.WriteByte(5);

        uint questId = 0;
        var awaitCallback = new AwaitCallback(
            response =>
            {
                questId = response.ReadUInt();
                return AwaitCallbackResult.Success;
            },
            0x3514);

        PacketManager.SendPacket(packet, PacketDestination.Server, awaitCallback);
        awaitCallback.AwaitResponse();

        return questId;
    }

    public static void ReceiveQuestReward(string npcCodeName, uint questId, uint rewardId)
    {
        GetQuestId(npcCodeName);

        var packet = new Packet(0x7515);
        packet.WriteUInt(questId);
        packet.WriteByte(1);
        packet.WriteUInt(rewardId);
        PacketManager.SendPacket(packet, PacketDestination.Server);

        EventManager.FireEvent("OnGainItem");

        CloseShop();
    }

    public static void RepairItems(string npcCodeName)
    {
        if (!RepairGear)
            return;

        SelectNPC(npcCodeName);

        if (SelectedEntity == null)
        {
            Log.Debug("Cannot repair items because there is no smith selected!");
            return;
        }

        var packet = new Packet(0x703E);
        packet.WriteUInt(SelectedEntity.UniqueId);
        packet.WriteByte(2);

        var awaitCallback = new AwaitCallback(
            response =>
            {
                var result = packet.ReadByte();
                if (result == 2)
                {
                    var errorCode = response.ReadUShort();
                    Log.Debug($"Repair of items at NPC {npcCodeName} failed [code={errorCode}]");
                    return AwaitCallbackResult.Fail;
                }
                return AwaitCallbackResult.Success;
            },
            0xB03E);

        PacketManager.SendPacket(packet, PacketDestination.Server, awaitCallback);
        awaitCallback.AwaitResponse();

        CloseShop();
    }

    public static void StoreItems(string npcCodeName)
    {
        int firstSlot = 13;
        if (Game.ClientType == GameClientType.Global
            || Game.ClientType == GameClientType.Korean
            || Game.ClientType == GameClientType.VTC_Game
            || Game.ClientType == GameClientType.RuSro
            || Game.ClientType == GameClientType.Turkey
            || Game.ClientType == GameClientType.Taiwan
            || Game.ClientType == GameClientType.Japanese)
            firstSlot = 17;

        var tempInventory = Game.Player.Inventory.GetItems(item =>
            item.Slot >= firstSlot && StoreFilter.Any(p => p == item.Record.CodeName));

        SelectNPC(npcCodeName);
        var npc = SelectedEntity;
        if (npc == null)
        {
            Log.Debug("Cannot store items because there is no storage NPC selected!");
            return;
        }

        // Если уже есть выбранный NPC (диалог) – пробуем закрыть
        if (SelectedEntity != null && SelectedEntity.UniqueId != npc.UniqueId)
        {
            CloseShop();
            SelectNPC(npcCodeName);
            npc = SelectedEntity;
            if (npc == null) return;
        }

        // Сохраняем ID NPC (может пригодиться)
        if (!npc.Record.CodeName.Contains("WAREHOUSE"))
            LastGuildStorageNpcId = npc.UniqueId;

        if (npc.Record.CodeName.Contains("WAREHOUSE"))
        {
            OpenStorage(npc.UniqueId);
            if (Game.Player.Storage == null)
                return;
        }
        else
        {
            // Проверка другого члена гильдии
            if (IsAnyGuildMemberNearby(40))
            {
                Log.Debug("[ShoppingManager] Another guild member nearby, skipping guild storage operations.");
                CloseShop();
                return;
            }

            // Одна попытка открыть склад (таймаут 8 секунд)
            if (!TryOpenGuildStorageWithTimeout(npc.UniqueId, 8000))
            {
                Log.Warn("[ShoppingManager] Could not open guild storage. Skipping store operations.");
                CloseShop();
                return;
            }
            if (Game.Player.GuildStorage == null)
            {
                CloseShop();
                return;
            }
        }

        // Складирование вещей
        Log.Status("Storing items");
        foreach (var item in tempInventory)
            StoreItem(item, npc);

        if (Game.Player.HasActiveAbilityPet && StorePetItems)
        {
            var petItemStoreList = Game.Player.AbilityPet.Inventory.GetItems(item =>
                StoreFilter.Any(p => p == item.Record.CodeName));

            foreach (var item in petItemStoreList)
            {
                var playerSlot = Game.Player.AbilityPet.MoveItemToPlayer(item.Slot);
                if (playerSlot != 0xFF)
                {
                    var movedItem = Game.Player.Inventory.GetItemAt(playerSlot);
                    StoreItem(movedItem, npc);
                }
            }
        }

        // Если это гильдейский склад – ждём 6 секунд (как было) и закрываем склад.
        if (!npc.Record.CodeName.Contains("WAREHOUSE"))
        {
            Log.Debug("[ShoppingManager] Waiting 6 seconds for gold operations...");
            Thread.Sleep(6000);
            CloseGuildStorage(npc.UniqueId);
            _guildStorageUsedThisTown = true;
        }

        // Закрываем NPC
        CloseShop();
    }

    public static void SortItems(string npcCodeName)
    {

        SelectNPC(npcCodeName);
        var npc = SelectedEntity;
        if (npc == null)

        // Если это гильдейский склад и он уже использовался в этом городском визите – пропускаем сортировку
        if (!npc.Record.CodeName.Contains("WAREHOUSE") && (_guildStorageUsedThisTown || IsAnyGuildMemberNearby(40)))
        {
            Log.Debug("[ShoppingManager] Skipping guild storage sorting (already used or another guild member nearby).");
            CloseShop();
            return;
        }

        {
            Log.Debug("Cannot sort items because there is no storage NPC selected!");
            return;
        }

        IList<InventoryItem> allStorageItems = null;

        if (npc.Record.CodeName.Contains("WAREHOUSE"))
        {
            OpenStorage(npc.UniqueId);
            Game.Player.Storage.Sort(npc);
            allStorageItems = Game.Player.Storage.GetItems(item => true);
        }
        else
        {
            if (!TryOpenGuildStorageWithTimeout(npc.UniqueId, 8000))
            {
                Log.Warn("[ShoppingManager] Could not open guild storage for sorting. Skipping.");
                return;
            }
            Game.Player.GuildStorage.Sort(npc);
            allStorageItems = Game.Player.GuildStorage.GetItems(item => true);
        }

        if (!npc.Record.CodeName.Contains("WAREHOUSE"))
        {
            if (IsAnyGuildMemberNearby(40))
            {
                Log.Debug("[ShoppingManager] Another guild member nearby, skipping guild storage sorting.");
                CloseShop();
                return;
            }
            else
                CloseGuildShop();
            return;
        }

        byte minSlot = allStorageItems.Min(i => i.Slot);
        byte maxSlot = allStorageItems.Max(i => i.Slot);

        for (byte i = minSlot; i <= maxSlot; i++)
        {
            List<InventoryItem> remaining = null;
            if (npc.Record.CodeName.Contains("WAREHOUSE"))
            {
                remaining = Game.Player.Storage
                    .GetItems(it => it.Slot >= i)
                    .OrderBy(it => it.ItemId)
                    .ThenBy(it => it.Slot)
                    .ToList();
            }
            else
            {
                remaining = Game.Player.GuildStorage
                    .GetItems(it => it.Slot >= i)
                    .OrderBy(it => it.ItemId)
                    .ThenBy(it => it.Slot)
                    .ToList();
            }

            if (remaining == null || remaining.Count == 0)
                continue;

            var groupKey = remaining[0].Record.CodeName;
            var candidateSlots = remaining
                .Where(it => it.Record.CodeName == groupKey)
                .Select(it => it.Slot)
                .OrderBy(s => s)
                .ToList();

            if (candidateSlots == null || candidateSlots.Count == 0)
                break;

            var fromSlot = candidateSlots[0];
            if (fromSlot == i)
                continue;

            Log.Debug($"[ShoppingManager] Reordering storage: moving slot {fromSlot} to slot {i}");

            InventoryItem itemToMove;
            if (npc.Record.CodeName.Contains("WAREHOUSE"))
            {
                itemToMove = Game.Player.Storage.GetItemAt(fromSlot);
                if (itemToMove != null)
                {
                    Game.Player.Storage.MoveItem(fromSlot, i, (ushort)itemToMove.Amount, npc);
                    Thread.Sleep(500);
                }
            }
            else
            {
                itemToMove = Game.Player.GuildStorage.GetItemAt(fromSlot);
                if (itemToMove != null)
                {
                    Game.Player.GuildStorage.MoveItem(fromSlot, i, (ushort)itemToMove.Amount, npc);
                    Thread.Sleep(500);
                }
            }
        }

        if (!npc.Record.CodeName.Contains("WAREHOUSE"))
        {
            Log.Debug("[ShoppingManager] Waiting 6 seconds for gold operations...");
            Thread.Sleep(6000);
            CloseGuildStorage(npc.UniqueId);
        }

        if (Game.Clientless || npc.Record.CodeName.Contains("WAREHOUSE"))
            CloseShop();
        else
            CloseGuildShop();
    }

    public static void CloseShop()
    {
        Running = false;
        if (SelectedEntity != null && SelectedEntity.TryDeselect())
            SelectedEntity = null;
    }

    public static void CloseGuildShop()
    {
        Running = false;
        if (SelectedEntity != null)
            SelectedEntity = null;
    }

    public static void CloseGuildStorage(uint uniqueId)
    {
        var packet = new Packet(0x7251);
        packet.WriteUInt(uniqueId);
        var awaitResult = new AwaitCallback(null, 0xB251);
        PacketManager.SendPacket(packet, PacketDestination.Server, awaitResult);
        awaitResult.AwaitResponse();

        Thread.Sleep(2000);
        IsGuildStorageOpen = false;
        Log.Debug("[ShoppingManager] Guild storage closed.");
    }

    private static void OpenStorage(uint uniqueId)
    {
        if (Game.Player.Storage != null)
            return;

        var packet = new Packet(0x703C);
        packet.WriteInt(uniqueId);
        packet.WriteByte(0);
        var awaitResult = new AwaitCallback(null, 0x3049);
        PacketManager.SendPacket(packet, PacketDestination.Server, awaitResult);
        awaitResult.AwaitResponse();

        packet = new Packet(0x7046);
        packet.WriteUInt(uniqueId);
        packet.WriteUInt(0x04);
        awaitResult = new AwaitCallback(null, 0xB046);
        PacketManager.SendPacket(packet, PacketDestination.Server, awaitResult);
        awaitResult.AwaitResponse();
    }

    /// <summary>
    /// Opens the guild storage with a timeout.
    /// </summary>
    /// <param name="uniqueId">NPC unique ID.</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default 8000).</param>
    /// <returns>True if opened successfully within timeout, otherwise false.</returns>
    public static bool TryOpenGuildStorageWithTimeout(uint uniqueId, int timeoutMs = 8000)
    {
        if (IsGuildStorageOpen)
            return true;

        var start = Environment.TickCount;

        var packet = new Packet(0x7046);
        packet.WriteUInt(uniqueId);
        packet.WriteByte(0x0D);
        var awaitResult = new AwaitCallback(null, 0xB046);
        PacketManager.SendPacket(packet, PacketDestination.Server, awaitResult);
        awaitResult.AwaitResponse();

        Thread.Sleep(2000);

        if (Game.Clientless)
        {
            packet = new Packet(0x7250);
            packet.WriteInt(uniqueId);
            awaitResult = new AwaitCallback(null, 0xB250);
            PacketManager.SendPacket(packet, PacketDestination.Server, awaitResult);
            awaitResult.AwaitResponse();

            packet = new Packet(0x7252);
            packet.WriteInt(uniqueId);
            PacketManager.SendPacket(packet, PacketDestination.Server);
        }

        int elapsed;
        do
        {
            Thread.Sleep(100);
            elapsed = Environment.TickCount - start;
            if (Game.Player?.GuildStorage != null)
            {
                IsGuildStorageOpen = true;
                EventManager.FireEvent("OnGuildStorageOpened");
                Log.Debug("[ShoppingManager] Guild storage opened successfully.");
                return true;
            }
        } while (elapsed < timeoutMs);

        Log.Warn("[ShoppingManager] Failed to open guild storage within timeout.");
        IsGuildStorageOpen = false;
        CloseShop();
        return false;
    }

    private static void OpenGuildStorage(uint uniqueId)
    {
        TryOpenGuildStorageWithTimeout(uniqueId, 5000);
    }

    public static void SelectNPC(string npcCodeName)
    {
        if (SelectedEntity != null && SelectedEntity.Record.CodeName == npcCodeName)
            return;

        if (!SpawnManager.TryGetEntity<SpawnedNpcNpc>(p => p.Record.CodeName == npcCodeName, out var entity))
        {
            Log.Warn("Cannot access the NPC [" + npcCodeName + "] because it does not exist nearby.");
            return;
        }

        entity.TrySelect();
    }

    private static void StoreItem(InventoryItem item, SpawnedBionic npc)
    {
        byte destinationSlot;
        if (npc.Record.CodeName.Contains("WAREHOUSE"))
            destinationSlot = Game.Player.Storage.GetFreeSlot();
        else
            destinationSlot = Game.Player.GuildStorage.GetFreeSlot();

        var packet = new Packet(0x7034);
        packet.WriteByte(npc.Record.CodeName.Contains("WAREHOUSE") ? 0x02 : 0x1E);
        packet.WriteByte(item.Slot);
        packet.WriteByte(destinationSlot);
        packet.WriteUInt(npc.UniqueId);

        var awaitResult = new AwaitCallback(null, 0xB034);
        PacketManager.SendPacket(packet, PacketDestination.Server, awaitResult);
        awaitResult.AwaitResponse();
    }

    public static void LoadFilters()
    {
        var configSell = PlayerConfig.GetArray<string>("RSBot.Shopping.Sell");
        var configStore = PlayerConfig.GetArray<string>("RSBot.Shopping.Store");

        foreach (var item in configSell)
            SellFilter.Add(item);

        foreach (var item in configStore)
            StoreFilter.Add(item);
    }

    public static void SaveFilters()
    {
        PlayerConfig.SetArray("RSBot.Shopping.Sell", SellFilter);
        PlayerConfig.SetArray("RSBot.Shopping.Store", StoreFilter);
    }

    public static void ChooseTalkOption(string npcCodeName, TalkOption option)
    {
        if (!SpawnManager.TryGetEntity<SpawnedNpcNpc>(p => p.Record.CodeName == npcCodeName, out var entity))
        {
            Log.Debug("Cannot access the NPC [" + npcCodeName + "] because it does not exist nearby.");
            return;
        }

        SelectNPC(npcCodeName);
        var packet = new Packet(0x7046);
        packet.WriteUInt(entity.UniqueId);
        packet.WriteByte(option);

        var awaitResult = new AwaitCallback(
            response => response.ReadByte() == 0x01 && response.ReadByte() == (byte)option
                ? AwaitCallbackResult.Success
                : AwaitCallbackResult.ConditionFailed,
            0xB046);

        PacketManager.SendPacket(packet, PacketDestination.Server, awaitResult);
        awaitResult.AwaitResponse(1000);
    }

    public static void Stop()
    {
        Running = false;
        Finished = true;
    }
}
