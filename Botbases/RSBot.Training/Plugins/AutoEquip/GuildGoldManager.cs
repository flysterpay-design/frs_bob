using System;
using System.Threading.Tasks;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.Core.Network;

namespace RSBot.AutoEquip
{
    public static class GuildGoldManager
    {
        private static bool _initialized;
        private static bool _goldAdjustedThisTown;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            EventManager.SubscribeEvent("OnGuildStorageOpened", OnGuildStorageOpened);

            Log.Notify("[GuildGold] Manager initialized.");
        }

        public static void ResetTownFlag()
        {
            _goldAdjustedThisTown = false;
            Log.Debug("[GuildGold] Town flag reset.");
        }

        private static async void OnGuildStorageOpened()
        {
            if (_goldAdjustedThisTown)
            {
                Log.Debug("[GuildGold] Already adjusted gold in this town visit. Skipping.");
                return;
            }

            await Task.Delay(1000);

            if (ShoppingManager.IsGuildStorageOpen && Game.Player.GuildStorage != null)
            {
                AdjustGold();
                _goldAdjustedThisTown = true;
            }
            else
            {
                Log.Debug("[GuildGold] Guild storage not ready after delay. Skipping.");
            }
        }

        public static void AdjustGold()
        {
            try
            {
                if (!PlayerConfig.Get("GuildGold.Enabled", true)) return;

                if (!ShoppingManager.IsGuildStorageOpen || Game.Player.GuildStorage == null)
                {
                    Log.Debug("[GuildGold] Guild storage not ready. Skipping.");
                    return;
                }

                // Проверяем наличие другого члена гильдии в радиусе 40 метров
                if (ShoppingManager.IsAnyGuildMemberNearby(40))
                {
                    Log.Debug("[GuildGold] Another guild member nearby, skipping gold adjustment.");
                    return;
                }

                uint targetGold = PlayerConfig.Get<uint>("GuildGold.TargetAmount", 1_000_000);
                if (targetGold == 0) return;

                ulong playerGold = Game.Player.Gold;
                long delta = (long)targetGold - (long)playerGold;

                if (delta == 0) return;

                bool withdraw = delta > 0;
                uint amount = (uint)(withdraw ? Math.Min(delta, 2_000_000_000L) : Math.Min(-delta, (long)playerGold));
                if (amount == 0) return;

                SendGoldPacket(amount, withdraw);
                Log.Status($"[GuildGold] Sent {(withdraw ? "withdraw" : "deposit")} request for {amount} gold.");
            }
            catch (Exception ex)
            {
                Log.Error($"[GuildGold] Error: {ex.Message}");
            }
        }

        private static void SendGoldPacket(uint amount, bool withdraw)
        {
            // Небольшая задержка для стабильности (уменьшена до 300 мс)
            System.Threading.Thread.Sleep(300);

            var packet = new Packet(0x7034);
            packet.WriteByte(withdraw ? (byte)0x21 : (byte)0x20);
            packet.WriteUInt(amount);
            packet.WriteUInt(0);
            // packet.WriteUInt(Game.Player.UniqueId); // если сервер требует

            Log.Debug($"[GuildGold] Sending packet: opcode=0x7034, operation={(withdraw ? "withdraw" : "deposit")}, amount={amount}");
            PacketManager.SendPacket(packet, PacketDestination.Server);
        }

        /// <summary>
        /// Попытаться открыть склад гильдии (если ещё не открыт) и скорректировать золото.
        /// Используется как резервный вызов, если складирование не открыло склад.
        /// </summary>
        public static void TryAdjustGold(uint npcUniqueId)
        {
            if (!PlayerConfig.Get("GuildGold.Enabled", true)) return;

            // Если склад уже открыт
            if (ShoppingManager.IsGuildStorageOpen && Game.Player.GuildStorage != null)
            {
                AdjustGold();
                return;
            }

            // Если рядом другой член гильдии – не пытаемся
            if (ShoppingManager.IsAnyGuildMemberNearby(40))
            {
                Log.Debug("[GuildGold] Another guild member nearby, not opening storage for gold.");
                return;
            }

            // Одна попытка открыть склад (таймаут 10 сек)
            if (ShoppingManager.TryOpenGuildStorageWithTimeout(npcUniqueId, 10000))
            {
                AdjustGold();
            }
            else
            {
                Log.Debug("[GuildGold] Could not open guild storage for gold adjustment. Skipping.");
            }
        }
    }
}
