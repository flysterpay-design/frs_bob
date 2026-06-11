using System;
using System.Collections.Generic;
using System.Linq;
using RSBot.Core;
using RSBot.Core.Event;
using RSBot.Core.Objects;
using RSBot.Core.Objects.Party;
using RSBot.Core.Components;
using RSBot.Core.Objects.Spawn;

namespace RSBot.Training.Bundle.PartyBuffing;

internal class PartyBuffingBundle : IBundle
{
    private bool _refreshing;
    private List<BuffingPartyMember> BuffingPartyMembers;

    private static readonly HashSet<ushort> TownRegionIds = new HashSet<ushort>
    {
        1, 9, 14, 15, 16, 22, 23, 38
    };

    public PartyBuffingBundle()
    {
        EventManager.SubscribeEvent("OnPartyBuffSettingsChanged", OnPartyBuffSettingsChanged);
    }

    public void Invoke()
    {
        if (_refreshing)
            return;

        if (TownRegionIds.Contains(Game.Player.Movement.Source.Region))
            return;

        if (Game.Player.HasActiveVehicle)
            return;

        if (!Kernel.Bot.Running)
            return;

        var selectedGroup = PlayerConfig.Get("RSBot.Party.Buffing.SelectedGroup", "Default");

        SpawnManager.TryGetEntities<SpawnedPlayer>(p =>
            BuffingPartyMembers.Any(s => s.Group == selectedGroup && s.Name == p.Name),
            out var members
        );

        // Получаем радиус баффов партии (по умолчанию 50)
        int partyBuffRange = PlayerConfig.Get("RSBot.Party.BuffRange", 50);

        foreach (var member in members)
        {
            var buffingMember = BuffingPartyMembers.Find(p => p.Name == member.Name);
            if (buffingMember == null)
                continue;

            if (buffingMember.Buffs.Count == 0)
                continue;

            if (member.State.LifeState == LifeState.Dead)
                continue;

            // Проверка дистанции до члена партии
            float distance = (float)Game.Player.Position.DistanceTo(member.Position);
            if (distance > partyBuffRange)
            {
                Log.Debug($"Party member {member.Name} is too far ({distance:F1} > {partyBuffRange}) for buffs");
                continue;
            }

            Log.Status($"Buffing party");

            var activeBuffs = member.State.ActiveBuffs;

            foreach (var buff in buffingMember.Buffs)
            {
                var skill = Game.Player.Skills.GetSkillInfoById(buff);

                if (skill == null || skill.HasCooldown)
                    continue;

                if (!skill.Record.TargetGroup_Ally &&
                    skill.Record.TargetGroup_Party &&
                    !(Game.Party?.Members?.Any(p => p.Name == member.Name) ?? false))
                {
                    continue;
                }

                var isActive = member.State.HasActiveBuff(skill, out var info);
                if (isActive && skill.Isbugged && info.Isbugged)
                {
                    Log.Notify($"The buff on {member.Name} [{skill.Token}-{skill.Record?.GetRealName()}] expired");
                    skill?.Reset();
                    continue;
                }

                if (isActive)
                    continue;

                Log.Status($"Buffing {skill.Record?.GetRealName()} party member {member.Name}");
                skill.Cast(member.UniqueId, true);
            }
        }
    }

    public void Refresh()
    {
        _refreshing = true;
        BuffingPartyMembers = new List<BuffingPartyMember>();
        var settings = PlayerConfig.Get("RSBot.Party.Buffing", string.Empty);
        var collection = settings.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in collection)
            BuffingPartyMembers.Add(new BuffingPartyMember(item));
        _refreshing = false;
    }

    public void Stop() { }

    private void OnPartyBuffSettingsChanged()
    {
        Refresh();
    }
}
