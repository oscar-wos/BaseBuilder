using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using Serilog.Sinks.File;
using CounterStrikeSharp.API.Modules.Commands.Targeting;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using System.Runtime.InteropServices;
using CSTimer = CounterStrikeSharp.API.Modules.Timers;
using System.Data;
using CounterStrikeSharp.API.Core.Translations;

namespace BaseBuilder;

public partial class BaseBuilder
{
    [GameEventHandler(HookMode.Post)]
    public HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (@event == null) return HookResult.Continue;

        var player = @event.Userid;
        if (player == null || !player.CheckValid()) return HookResult.Continue;

        if (player.Team == CsTeam.Terrorist)
        {
            player.RemoveWeapons();
            player.GiveNamedItem("weapon_knife");
        }

        if (player.TeamNum == ZOMBIE)
        {
            switch (PlayerTypes[player].zombieType)
            {
                case "default":
                    Server.NextFrame(() => player.SetHp(2500));
                    break;
                case "tanker":
                    Server.NextFrame(() => player.SetHp(5000));
                    break;

                default: 
                    break;
            }
        }

        return HookResult.Continue;
    }
}