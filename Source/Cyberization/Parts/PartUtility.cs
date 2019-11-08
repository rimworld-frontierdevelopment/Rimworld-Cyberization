using System;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using RimWorld;
using Verse;

namespace FrontierDevelopments.Cyberization.Parts
{
    public static class PartUtility
    {
        public static IEnumerable<Hediff_AddedPart> AddedParts(Pawn pawn)
        {
            return pawn.health.hediffSet.hediffs
                .OfType<Hediff_AddedPart>();
        }

        public static IEnumerable<Hediff_AddedPart> PartsWithFeature(Pawn pawn, Type feature)
        {
            return AddedParts(pawn)
                .Where(part => part.comps.Any(comp => comp.GetType() == feature));
        }

        public static IEnumerable<Hediff_AddedPart> PoweredPartsFor(Pawn pawn)
        {
            return PartsWithFeature(pawn, typeof(AddedPartPowerConsumer));
        }

        public static IEnumerable<Hediff_AddedPart> MaintainablePartsFor(Pawn pawn)
        {
            return PartsWithFeature(pawn, typeof(AddedPartMaintenance));
        }

        private static bool PartNeedsMaintenance(Hediff_AddedPart part)
        {
            if (part.TryGetComp<AddedPartMaintenance>()?.NeedsMaintenance ?? false) return true;
            if (part.TryGetComp<AddedPartBreakdownable>()?.IsBrokenDown ?? false) return true;
            if (part.TryGetComp<AddedPartDamageable>()?.NeedsRepair ?? false) return true;
            return false;
        }

        public static IEnumerable<AddedPartBreakdownable> PartsNeedingBreakdownRepair(Pawn pawn)
        {
            return pawn.health.hediffSet.hediffs
                .OfType<HediffWithComps>()
                .SelectMany(hediff => hediff.comps)
                .OfType<AddedPartBreakdownable>()
                .Where(part => part.IsBrokenDown);
        }

        public static IEnumerable<AddedPartDamageable> PartsNeedingDamageRepair(Pawn pawn)
        {
            return AddedParts(pawn)
                .SelectMany(hediff => hediff.comps)
                .OfType<AddedPartDamageable>()
                .Where(part => part.NeedsRepair);
        }

        public static IEnumerable<AddedPartMaintenance> PartsNeedingRoutineMaintenance(Pawn pawn)
        {
            return AddedParts(pawn)
                .SelectMany(hediff => hediff.comps)
                .OfType<AddedPartMaintenance>()
                .Where(part => part.NeedsMaintenance);
        }

        public static IEnumerable<Hediff_AddedPart> PartsNeedingAnyMaintenance(Pawn pawn)
        {
            return AddedParts(pawn).Where(PartNeedsMaintenance);
        }

        public static IEnumerable<Hediff_AddedPart> PartsNeedingAnyMaintenanceUrgent(Pawn pawn)
        {
            return PartsNeedingAnyMaintenance(pawn).Where(RequiresPartToLive<AddedPartMaintenance>);
        }

        public static bool NeedMaintenance(Pawn pawn)
        {
            return PartsNeedingAnyMaintenance(pawn).Any();
        }

        public static bool NeedMaintenanceUrgent(Pawn pawn)
        {
            return PartsNeedingAnyMaintenanceUrgent(pawn).Any();
        }

        public static bool HasPoweredParts(Pawn pawn)
        {
            return PoweredPartsFor(pawn).Any();
        }

        private static bool ToggleAndCheckPart<T>(Hediff_AddedPart part, Func<bool> callback, bool fallback = false)  where T : HediffComp, AddedPartEffectivenessModifier
        {
            var comp = part.TryGetComp<T>();
            if (comp == null) return fallback;
            
            comp.OverrideEffectivenessState(false);
            part.pawn.health.capacities.Notify_CapacityLevelsDirty();

            var result = callback();

            comp.OverrideEffectivenessState(null);
            part.pawn.health.capacities.Notify_CapacityLevelsDirty();

            return result;
        }

        private static bool ToggleAndCheckParts<T>(Pawn pawn, Func<bool> callback) where T : HediffComp, AddedPartEffectivenessModifier
        {
            var parts = PoweredPartsFor(pawn).ToList();
            
            // turn off all parts
            // checks if things such as both kidneys would be impacted
            parts
                .SelectMany(part => part.comps)
                .OfType<T>()
                .Do(comp => comp.OverrideEffectivenessState(false));
            pawn.health.capacities.Notify_CapacityLevelsDirty();

            var result = callback();
            
            // turn on all parts
            parts
                .SelectMany(part => part.comps)
                .OfType<T>()
                .Do(comp => comp.OverrideEffectivenessState(null));
            pawn.health.capacities.Notify_CapacityLevelsDirty();

            return result;
        }

        public static bool RequiresPartsToLive<T>(Pawn pawn) where T : HediffComp, AddedPartEffectivenessModifier
        {
            return ToggleAndCheckParts<T>(pawn, () => pawn.health.ShouldBeDeadFromRequiredCapacity() != null);
        }

        public static bool RequiresPartsForMovement<T>(Pawn pawn) where T : HediffComp, AddedPartEffectivenessModifier
        {
            return ToggleAndCheckParts<T>(pawn, () => !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving));
        }

        public static bool RequiresPartToLive<T>(Hediff_AddedPart part) where T : HediffComp, AddedPartEffectivenessModifier
        {
            return ToggleAndCheckPart<T>(part, () => part.pawn.health.ShouldBeDeadFromRequiredCapacity() != null);
        }

        public static bool RequiresPartToMove<T>(Hediff_AddedPart part) where T : HediffComp, AddedPartEffectivenessModifier
        {
            return ToggleAndCheckPart<T>(part, () => !part.pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving));
        }

        public static IEnumerable<Hediff> GetHediffsForPart(Hediff part)
        {
            // TODO check if hediff is on a child part?
            return part.pawn.health.hediffSet.hediffs.Where(hediff => hediff.Part == part.Part);
        }
    }
}