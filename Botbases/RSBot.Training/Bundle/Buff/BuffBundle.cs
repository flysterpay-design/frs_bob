using System.Linq;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.Core.Objects;

namespace RSBot.Training.Bundle.Buff;

internal class BuffBundle : IBundle
{
    private bool _invoked;
    private bool _buffBetweenAttacks { get; set; }

    public void Invoke()
    {
        if (_invoked)
            return;

        if ((Game.Player.Untouchable || Game.Player.InAction) && !_buffBetweenAttacks)
            return;
        if ((Game.Player.Untouchable || Game.Player.Berzerking) && _buffBetweenAttacks)
            return;

        try
        {
            _invoked = true;

            foreach (var buff in SkillManager.Buffs.Union(new[] { SkillManager.ImbueSkill, SkillManager.ResurrectionSkill }))
            {
                if (buff == null)
                    continue;

                var isActive = Game.Player.State.HasActiveBuff(buff, out var info);
                if (isActive && buff.Isbugged && info.Isbugged)
                {
                    Log.Notify($"[#377] The buff [{buff.Token}-{buff.Record?.GetRealName()}] expired");
                    EventManager.FireEvent("OnRemoveBuff", buff);
                    var playerSkill = Game.Player.Skills.GetSkillInfoById(buff.Id);
                    playerSkill?.Reset();
                    Game.Player.State.TryRemoveActiveBuff(info.Token, out _);
                }
            }

            var buffs = SkillManager.Buffs.FindAll(p => !Game.Player.State.HasActiveBuff(p, out _) && p.CanBeCasted);
            if (buffs == null || buffs.Count == 0)
                return;

            Log.Status("Buffing");

            foreach (var buff in buffs)
            {
                if (Game.Player.State.LifeState != LifeState.Alive || Game.Player.HasActiveVehicle)
                    break;

                if (Game.Player.State.HasActiveBuff(buff, out _) && !buff.HasCooldown)
                    break;

                Log.Debug($"Trying to cast buff: {buff} {buff.Record.Basic_Code}");
                buff.Cast(buff: true);
            }
        }
        finally
        {
            _invoked = false;
        }
    }

    public void Refresh()
    {
        _buffBetweenAttacks = PlayerConfig.Get<bool>("RSBot.Skills.checkCastBuffsBetweenAttacks", false);
        _invoked = false;
    }

    public void Stop()
    {
        _invoked = false;
    }
}
