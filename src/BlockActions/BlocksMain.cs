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
using System.Data;
using CounterStrikeSharp.API.Modules.Extensions;
using System.Reflection;
using System.Diagnostics.Tracing;

namespace BaseBuilder;

public partial class BaseBuilder
{
    public List<CBaseProp> UsedBlocks = new List<CBaseProp>();
    public Dictionary<CCSPlayerController, Builder> PlayerHolds = new Dictionary<CCSPlayerController, Builder>();
    public Dictionary<uint, CCSPlayerController> BlocksOwner = new Dictionary<uint, CCSPlayerController>();
    public List<Color> colors = new List<Color>() { Color.AliceBlue, Color.Aqua, Color.Blue, Color.Brown, Color.BurlyWood, Color.Chocolate, Color.Cyan, Color.DarkBlue, Color.DarkGreen, Color.DarkMagenta, Color.DarkOrange, Color.DarkRed, Color.Green, Color.Yellow, Color.Red, Color.Silver, Color.Pink, Color.Purple };

    public void OnGameFrame()
    {
        PrintChatOnFrame();

        //Disable block actions in prep time
        if (isBuildTimeEnd) return;

        foreach (var player in Utilities.GetPlayers().Where(p => p != null && p.PawnIsAlive))
        {
            //Continue if player is zombie
            if (player.TeamNum == ZOMBIE) continue;

            if (player.Buttons.HasFlag(PlayerButtons.Reload))
            {
                if (PlayerHolds.ContainsKey(player) && !PlayerHolds[player].isRotating)
                {
                    PlayerHolds[player].emptyProp.Teleport(null, new QAngle(PlayerHolds[player].emptyProp.AbsRotation!.X, PlayerHolds[player].emptyProp.AbsRotation!.Y + 45, PlayerHolds[player].emptyProp.AbsRotation!.Z));
                    PlayerHolds[player].isRotating = true;
                }
            } else
            {
                if (PlayerHolds.ContainsKey(player))
                {
                    PlayerHolds[player].isRotating = false;
                }
            }

            if (player.Buttons.HasFlag(PlayerButtons.Use))
            {
                if (PlayerHolds.ContainsKey(player))
                {
                    PressRepeat(player, PlayerHolds[player].emptyProp!);
                } 
                else
                {
                    var block = player.GetClientAimTarget();
                    if (block != null) 
                    {
                        if (BlocksOwner.ContainsKey(block.Index) && BlocksOwner[block.Index] != player) return;

                        FirstPress(player, block); 
                    }
                }
            } else
            {
                if (PlayerHolds.ContainsKey(player))
                {
                    var newprop = Utilities.CreateEntityByName<CPhysicsProp>("prop_dynamic");
                    if (newprop != null && newprop.IsValid)
                    {
                        newprop.Teleport(new Vector(-10, -10, -10));
                        CBaseEntity_SetParent(PlayerHolds[player].mainProp, newprop);
                        PlayerHolds[player].emptyProp.Remove();
                        PlayerHolds.Remove(player);
                    }
                }
            }
        }
    }

    public void FirstPress(CCSPlayerController player, CBaseProp prop)
    {
        var hitPoint = TraceShape(new Vector(player.PlayerPawn.Value!.AbsOrigin!.X, player.PlayerPawn.Value!.AbsOrigin!.Y, player.PlayerPawn.Value!.AbsOrigin!.Z + player.PlayerPawn.Value.CameraServices!.OldPlayerViewOffsetZ), player.PlayerPawn.Value!.EyeAngles!, false, true);
        
        if (prop != null && prop.IsValid && hitPoint != null && hitPoint.HasValue)
        {
            //Change block color to player color
            prop.Render = PlayerTypes[player].playerColor;
            Utilities.SetStateChanged(prop, "CBaseModelEntity", "m_clrRender");

            //fixed some bugs
            if (VectorUtils.CalculateDistance(prop.AbsOrigin!, Vector3toVector(hitPoint.Value)) > 150) return;

            var emptyProp = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
            if(emptyProp != null && emptyProp.IsValid)
            {
                emptyProp.Render = Color.Transparent;
                emptyProp.DispatchSpawn();
                emptyProp.Teleport(Vector3toVector(hitPoint.Value));

                CBaseEntity_SetParent(prop, emptyProp);

                //Teleport block if its so far away.
                int distance = (int)VectorUtils.CalculateDistance(emptyProp.AbsOrigin!, player.PlayerPawn.Value!.AbsOrigin!);
                if (distance > 450)
                {
                    emptyProp.Teleport(VectorUtils.GetEndXYZ(player, 400));
                    distance = 400;
                }

                PlayerHolds.Add(player, new Builder() { mainProp = prop, emptyProp = emptyProp, owner = player, isRotating = false, distance = distance});
            }
        }
    }

    public void PressRepeat(CCSPlayerController player, CDynamicProp block)
    {
        //To Remove Block On Prep Time
        if (!UsedBlocks.Contains(PlayerHolds[player].mainProp)) UsedBlocks.Add(PlayerHolds[player].mainProp);

        block.Teleport(VectorUtils.GetEndXYZ(player, PlayerHolds[player].distance), null, player.PlayerPawn.Value!.AbsVelocity!);

        //Checking ATTACK2 & ATTACK buttons for distance.
        if (player.Buttons.HasFlag(PlayerButtons.Attack))
        {
            PlayerHolds[player].distance += 2;
        }
        else if (player.Buttons.HasFlag(PlayerButtons.Attack2) && PlayerHolds[player].distance > 2)
        {
            PlayerHolds[player].distance -= 2;
        }
    }

    public void RemoveNotUsedBlocks()
    {
        foreach (var entity in Utilities.FindAllEntitiesByDesignerName<CBaseProp>("prop_dynamic"))
        {
            //Checking if removing parent empty prop
            if (!UsedBlocks.Contains(entity) && entity.AbsOrigin!.Z > -9) entity.Remove();
        }
    }

    private static MemoryFunctionVoid<CBaseEntity, CBaseEntity, CUtlStringToken?, matrix3x4_t?> CBaseEntity_SetParentFunc
        = new(GameData.GetSignature("CBaseEntity_SetParent"));

    public static void CBaseEntity_SetParent(CBaseEntity childrenEntity, CBaseEntity parentEntity)
    {
        if (!childrenEntity.IsValid || !parentEntity.IsValid) return;

        var origin = new Vector(childrenEntity.AbsOrigin!.X, childrenEntity.AbsOrigin!.Y, childrenEntity.AbsOrigin!.Z);
        CBaseEntity_SetParentFunc.Invoke(childrenEntity, parentEntity, null, null);
        // If not teleported, the childrenEntity will not follow the parentEntity correctly.
        childrenEntity.Teleport(origin, new QAngle(IntPtr.Zero), new Vector(IntPtr.Zero));
        Console.WriteLine("CBaseEntity_SetParent() done!");
    }
}

public class Builder
{
    public CDynamicProp emptyProp = null!;
    public CBaseProp mainProp = null!;
    public Vector offset = null!;
    public CCSPlayerController owner = null!;
    public bool isRotating;
    public int distance;
}