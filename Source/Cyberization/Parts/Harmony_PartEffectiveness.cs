using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace FrontierDevelopments.Cyberization.Parts
{
    class Harmony_PartEffectiveness
    {
        private static float CalculatePartEfficiency(Hediff hediff)
        {
            switch (hediff)
            {
                case HediffWithComps hediffWithComps:
                    return hediffWithComps.comps
                        .OfType<AddedPartEffectivenessModifier>()
                        .Aggregate(hediff.def.addedPartProps.partEfficiency, (multiplier, modifier) => multiplier * modifier.PartEffectiveness);
                default:
                    return hediff.def.addedPartProps.partEfficiency;
            }
        }

        private static bool InstructionsForPartEfficiency(int i, IList<CodeInstruction> instructions)
        {
            if (i + 2 >= instructions.Count) return false;
            
            var instruction = instructions[i];
            var nextInstruction = instructions[i + 1];
            var nextNextInstruction = instructions[i + 2];

            // search for:
            // ldfld class Verse.HediffDef Verse.Hediff::def
            // ldfld class Verse.AddedBodyPartProps Verse.HediffDef::addedPartProps
            // ldfld float32 Verse.AddedBodyPartProps::partEfficiency
            return instruction.opcode == OpCodes.Ldfld
                   && (FieldInfo)instruction.operand == AccessTools.Field(typeof(Hediff), nameof(Hediff.def))
                   && nextInstruction.opcode == OpCodes.Ldfld
                   && (FieldInfo)nextInstruction.operand == AccessTools.Field(typeof(HediffDef), nameof(HediffDef.addedPartProps))
                   && nextNextInstruction.opcode == OpCodes.Ldfld
                   && (FieldInfo)nextNextInstruction.operand == AccessTools.Field(typeof(AddedBodyPartProps), nameof(AddedBodyPartProps.partEfficiency));
        }

        private static CodeInstruction CallCalculatePartEfficiency()
        {
            return new CodeInstruction(
                OpCodes.Call, 
                AccessTools.Method(
                    typeof(Harmony_PartEffectiveness), 
                    nameof(CalculatePartEfficiency)));
        }

        private static IEnumerable<CodeInstruction> ReplaceAllPartEfficiency(List<CodeInstruction> instructions, string patchedMethod)
        {
            var success = false;
            for (int i = 0; i < instructions.Count; i++)
            {
                if (InstructionsForPartEfficiency(i , instructions))
                {
                    success = true;
                    i += 2;
                    yield return CallCalculatePartEfficiency();
                }
                else
                {
                    yield return instructions[i];
                }
            }

            if (!success)
            {
                Log.Error("Failed to patch " + patchedMethod);
            }
        }
        
        [HarmonyPatch(typeof(PawnCapacityUtility), nameof(PawnCapacityUtility.CalculatePartEfficiency))]
        static class Patch_PawnCapacityUtility_CalculatePartEfficiency
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
            {
                return ReplaceAllPartEfficiency(instructions.ToList(), "PawnCapacityUtility.CalculatePartEfficiency");
            }
        }

        [HarmonyPatch(typeof(Hediff_AddedPart), nameof(Hediff_AddedPart.TipStringExtra), MethodType.Getter)]
        static class Patch_Hediff_AddedPart_TipStringExtra
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
            {
                return ReplaceAllPartEfficiency(instructions.ToList(), "Hediff_AddedPart.TipStringExtra");
            }
        }
    }
}