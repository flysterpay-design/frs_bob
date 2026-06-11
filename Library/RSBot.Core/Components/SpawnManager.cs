using System;
using System.Collections.Generic;
using System.Linq;
using RSBot.Core.Event;
using RSBot.Core.Network;
using RSBot.Core.Objects.Spawn;

namespace RSBot.Core.Components;

public static class SpawnManager
{
    private static readonly object _lock = new();
    private static List<SpawnedEntity> _entities = new(255);

    public static T GetEntity<T>(uint uniqueId) where T : SpawnedEntity
    {
        return (T)_entities.Find(p => p != null && p.UniqueId == uniqueId);
    }

    public static T GetEntity<T>(Func<T, bool> condition) where T : SpawnedEntity
    {
        return (T)_entities.Find(p => p is T entityT && condition(entityT));
    }

    public static bool TryGetEntity<T>(uint uniqueId, out T entity) where T : SpawnedEntity
    {
        entity = GetEntity<T>(uniqueId);
        return entity != null;
    }

    public static bool TryGetEntityIncludingMe(uint uniqueId, out SpawnedEntity entity)
    {
        entity = null;

        if (uniqueId == Game.Player.UniqueId)
            entity = Game.Player;
        else if (Game.Player.Transport?.UniqueId == uniqueId)
            entity = Game.Player.Transport;
        else if (Game.Player.JobTransport?.UniqueId == uniqueId)
            entity = Game.Player.JobTransport;
        else if (Game.Player.Growth?.UniqueId == uniqueId)
            entity = Game.Player.Growth;
        else if (Game.Player.Fellow?.UniqueId == uniqueId)
            entity = Game.Player.Fellow;
        else if (!TryGetEntity(uniqueId, out entity))
            return false;

        return entity != null;
    }

    public static bool TryGetEntity<T>(Func<T, bool> condition, out T entity) where T : SpawnedEntity
    {
        entity = GetEntity<T>(p => condition(p));
        return entity != null;
    }

    // Старые методы с аллокациями (оставлены для совместимости)
    public static bool TryGetEntities<T>(out IEnumerable<T> entities) where T : SpawnedEntity
    {
        lock (_lock)
        {
            entities = _entities.FindAll(p => p is T).Cast<T>();
            return entities != null;
        }
    }

    public static bool TryGetEntities<T>(Func<T, bool> predicate, out IEnumerable<T> entities) where T : SpawnedEntity
    {
        lock (_lock)
        {
            entities = _entities.FindAll(p => p is T entityT && predicate(entityT)).Cast<T>();
            return entities != null;
        }
    }

    /// <summary>
    ///     Получает сущности, заполняя переданный список (без аллокаций).
    ///     Возвращает количество добавленных сущностей.
    /// </summary>
    public static int GetEntities<T>(List<T> output, Func<T, bool> predicate = null) where T : SpawnedEntity
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        lock (_lock)
        {
            int count = 0;
            foreach (var entity in _entities)
            {
                if (entity is T t && (predicate == null || predicate(t)))
                {
                    output.Add(t);
                    count++;
                }
            }
            return count;
        }
    }

    public static int Count<T>(Func<T, bool> predicate) where T : SpawnedEntity
    {
        lock (_lock)
        {
            return _entities.Count(p => p is T && predicate(p as T));
        }
    }

    public static bool Any<T>(Func<T, bool> predicate) where T : SpawnedEntity
    {
        lock (_lock)
        {
            return _entities.Any(p => p is T && predicate(p as T));
        }
    }

    public static bool TryRemove(uint uniqueId, out SpawnedEntity removedEntity)
    {
        lock (_lock)
        {
            removedEntity = _entities.Find(p => p.UniqueId == uniqueId);
            if (removedEntity == null)
                return false;

            if (Game.SelectedEntity?.UniqueId == uniqueId)
                Game.SelectedEntity = null;

            removedEntity.Dispose();
            return _entities.Remove(removedEntity);
        }
    }

    public static int Clear<T>()
    {
        lock (_lock)
        {
            return _entities.RemoveAll(p => p is T && p.Dispose());
        }
    }

    public static void Parse(Packet packet, bool isGroup = false)
    {
        lock (_lock)
        {
            var refObjId = packet.ReadUInt();

            if (refObjId == uint.MaxValue)
            {
                _entities.Add(SpawnedSpellArea.FromPacket(packet));
                return;
            }

            if (refObjId == 0xfffffffe)
            {
                packet.ReadUInt();
                packet.ReadUInt();
            }

            var obj = Game.ReferenceManager.GetRefObjCommon(refObjId);
            if (obj == null)
            {
                Log.Debug($"SpawnManager::Parse error while getting RefObjCommon by id {refObjId}");
                return;
            }

            switch (obj.TypeID1)
            {
                case 1:
                    switch (obj.TypeID2)
                    {
                        case 1:
                            {
                                var spawnedPlayer = new SpawnedPlayer(refObjId);
                                spawnedPlayer.Deserialize(packet);
                                _entities.Add(spawnedPlayer);
                                EventManager.FireEvent("OnSpawnPlayer", spawnedPlayer);
                            }
                            break;
                        case 2:
                            switch (obj.TypeID3)
                            {
                                case 1:
                                    {
                                        var spawnedMonster = new SpawnedMonster(refObjId);
                                        spawnedMonster.Deserialize(packet);
                                        _entities.Add(spawnedMonster);
                                        EventManager.FireEvent("OnSpawnMonster", spawnedMonster);
                                    }
                                    break;
                                case 3:
                                    {
                                        var spawnedCos = new SpawnedCos(refObjId);
                                        spawnedCos.Deserialize(packet);
                                        _entities.Add(spawnedCos);
                                        EventManager.FireEvent("OnSpawnCos", spawnedCos);
                                    }
                                    break;
                                case 5:
                                    {
                                        var spawnedFortressStructure = new SpawnedFortressStructure(refObjId);
                                        spawnedFortressStructure.Deserialize(packet);
                                        _entities.Add(spawnedFortressStructure);
                                        EventManager.FireEvent("OnSpawnFortressStructure", spawnedFortressStructure);
                                    }
                                    break;
                                default:
                                    {
                                        var spawnedNpc = new SpawnedNpcNpc(refObjId);
                                        spawnedNpc.ParseBionicDetails(packet);
                                        spawnedNpc.Deserialize(packet);
                                        _entities.Add(spawnedNpc);
                                        EventManager.FireEvent("OnSpawnNpc", spawnedNpc);
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 3:
                    var spawnedItem = SpawnedItem.FromPacket(packet, refObjId);
                    _entities.Add(spawnedItem);
                    EventManager.FireEvent("OnSpawnItem", spawnedItem);
                    break;
                case 4:
                    var spawnedPortal = SpawnedPortal.FromPacket(packet, refObjId);
                    _entities.Add(spawnedPortal);
                    EventManager.FireEvent("OnSpawnPortal", spawnedPortal);
                    break;
            }

            if (!isGroup)
            {
                if (obj.TypeID1 == 1 || obj.TypeID1 == 4)
                {
                    packet.ReadByte();
                }
                else if (obj.TypeID1 == 3)
                {
                    packet.ReadByte();
                    packet.ReadUInt();
                }
            }
        }
    }

    /// <summary>
    ///     Updates all entities every call (each tick, ~500 ms).
    /// </summary>
    public static void Update(int delta)
    {
        lock (_lock)
        {
            foreach (var entity in _entities)
                entity.Update(delta);
        }
    }

    public static void Clear()
    {
        lock (_lock)
        {
            _entities = [];
        }
    }
}
