using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace SomeThingsFloat;

// Prevent downed pawns from crawling into water where they would drown.
[HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.SetupMoveIntoNextCell))]
public static class Pawn_PathFollower_SetupMoveIntoNextCell
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var found = false;
        foreach (var instr in instructions)
        {
            yield return instr;
            if (instr.opcode != OpCodes.Call || !instr.operand.ToString().Contains("WalkableBy"))
            {
                continue;
            }

            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Call, typeof(Pawn_PathFollower_SetupMoveIntoNextCell)
                .GetMethod(nameof(Hook)));
            found = true;
        }

        if (!found)
        {
            Log.Error("SomeThingsFloat: Could not patch Pawn_PathFollower.SetupMoveIntoNextCell()");
        }
    }

    public static bool Hook(bool result, Pawn_PathFollower pather)
    {
        if (!SomeThingsFloatMod.Instance.Settings.DownedPawnsDrown)
        {
            return result;
        }

        if (!result)
        {
            return false;
        }

        var pawn = pather.pawn;
        var nextCell = pather.nextCell;
        if (!pawn.Downed)
        {
            return true;
        }

        if (!SomeThingsFloat.CanDrown(pawn))
        {
            return true;
        }

        if (!nextCell.IsValid || nextCell == pawn.Position)
        {
            return true;
        }

        if (!SomeThingsFloat.FloatingMapComponents.TryGetValue(pawn.Map, out var component))
        {
            return true;
        }

        if (!component.IsCellWithWater(nextCell))
        {
            return true;
        }

        if (SomeThingsFloat.ShallowTerrainDefs.Contains(nextCell.GetTerrain(pawn.Map)))
        {
            return true;
        }

        // Need to reset nextCell to pawn.Position, since the next pathing attempt starts from nextCell.
        // The next path selected will presumably be the same, so this will happen once a tick.
        pather.ResetToCurrentPosition();
        return false;
    }
}