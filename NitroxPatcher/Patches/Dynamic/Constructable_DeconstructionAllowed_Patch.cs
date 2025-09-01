using System.Reflection;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Bases;
using NitroxClient.GameLogic.Settings;
using NitroxClient.GameLogic.Simulation;
using NitroxClient.MonoBehaviours;
using NitroxClient.Unity.Helper;
using NitroxModel.DataStructures;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Prevents deconstruction if the target base is desynced.
/// </summary>
public sealed partial class Constructable_DeconstructionAllowed_Patch : NitroxPatch, IDynamicPatch
{
    private static bool isRemotePlayerSitting = false;
    
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((Constructable t) => t.DeconstructionAllowed(out Reflect.Ref<string>.Field));

    public static void Postfix(Constructable __instance, ref bool __result, ref string reason)
    {
        if (!__result || !BuildingHandler.Main || !__instance.TryGetComponentInParent(out NitroxEntity parentEntity, true))
        {
            return;
        }

        // Prevent deconstruction if a player is sitting on the bench/chair
        if (__instance.TryGetComponent(out Bench bench) && bench.TryGetNitroxEntity(out NitroxEntity chairEntity))
        {
            CheckSimulationLock(chairEntity);
        }
        else if (__instance.TryGetComponent(out Bed bed) && bed.TryGetNitroxEntity(out NitroxEntity bedEntity))
        {
            // Can't prevent deconstruction by checking for a lock, because beds don't currently use simulation locks when a player uses them
        }
        else if (__instance.TryGetNitroxEntity(out NitroxEntity entity) && entity.GetComponentInChildren<Bench>())
        {
            foreach (Bench multiplayerBenchSide in entity.GetComponentsInChildren<Bench>(true))
            {
                multiplayerBenchSide.TryGetNitroxEntity(out NitroxEntity multiplayerBenchSideEntity);
                CheckSimulationLock(multiplayerBenchSideEntity);
            }
        }

        if (isRemotePlayerSitting)
        {
            __result = false;
            reason = Language.main.Get("Nitrox_RemotePlayerObstacle");
        }
        
        DeconstructionAllowed(parentEntity.Id, ref __result, ref reason);
    }

    public static void DeconstructionAllowed(NitroxId baseId, ref bool __result, ref string reason)
    {
        if (BuildingHandler.Main.BasesCooldown.ContainsKey(baseId))
        {
            __result = false;
            reason = Language.main.Get("Nitrox_ErrorRecentBuildUpdate");
        }
        else if (BuildingHandler.Main.EnsureTracker(baseId).IsDesynced() && NitroxPrefs.SafeBuilding.Value)
        {
            __result = false;
            reason = Language.main.Get("Nitrox_ErrorDesyncDetected");
        }
    }

    private static void CheckSimulationLock(NitroxEntity entity)
    {
        NitroxId id = entity.Id;
        
        BenchContext context = new();
        LockRequest<BenchContext> lockRequest = new(id, SimulationLockType.EXCLUSIVE, ReceivedSimulationLockResponse, context);
        Resolve<SimulationOwnership>().RequestSimulationLock(lockRequest);
        
        // Remove lock after checking for it
        Resolve<SimulationOwnership>().RequestSimulationLock(id, SimulationLockType.TRANSIENT);
    }
    
    private static void ReceivedSimulationLockResponse(NitroxId id, bool lockAcquired, BenchContext context)
    {
        isRemotePlayerSitting = !lockAcquired;
    }
    
    internal class BenchContext : LockRequestContext;
}
