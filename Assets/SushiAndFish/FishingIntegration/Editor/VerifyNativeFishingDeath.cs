using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PaperPixelGames.Core.PlayerTools;
using UnityEditor;
using UnityEngine;

// Read saved source/prefab references only. Never enter Play, knock down a
// player, invoke a recovery callback, send a command or step fishing/physics.
public static class VerifyNativeFishingDeath
{
    const string Out = "D:/UnityProjects/_CodexWork/NativeFishingDeath-20261028/";
    const string Before = "D:/UnityProjects/_CodexBackups/sushi-native-fishing-death-20261028/Project/";
    const string Native = "Assets/Scripts/Assembly-CSharp/PaperPixelGames/";
    const string Runtime = "Assets/SushiAndFish/FishingIntegration/Runtime/";
    static readonly List<string> checks = new List<string>();

    static void Check(bool value, string label)
    {
        checks.Add((value ? "PASS " : "FAIL ") + label);
        File.WriteAllLines(Out + "Checks.txt", checks);
        if (!value) throw new Exception(label);
    }

    static string Read(string path) => File.ReadAllText(path).Replace("\r\n", "\n");
    static string Body(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0) throw new Exception("Missing method " + signature);
        start = source.IndexOf('{', start);
        int end = start + 1, depth = 1;
        for (; end < source.Length && depth != 0; end++)
        {
            if (source[end] == '{') depth++;
            if (source[end] == '}') depth--;
        }
        return source.Substring(start, end - start);
    }

    static UnityEngine.Object Reference(UnityEngine.Object source, string field) =>
        new SerializedObject(source).FindProperty(field)?.objectReferenceValue;

    public static void Run()
    {
        Directory.CreateDirectory(Out);
        Check(!EditorApplication.isPlaying, "Inspection uses saved source and prefab assets only; no gameplay or physics simulation");
        string divePath = Native + "Core/PlayerTools/DivingEquipment.cs";
        string dive = Read(divePath), oldDive = Read(Before + divePath);
        string bridge = Read(Runtime + "NativeFishingDeath.cs"), actor = Read(Runtime + "FishingActor.cs");
        string network = Read(Runtime + "MigrationNetworking.cs"), service = Read(Runtime + "FishingMigration.cs");
        Check(!actor.Contains("recoverAt") && !actor.Contains("3 秒后") && !actor.Contains("vitals.Restore()"), "The former three-second local heal/timer is removed from the actor");
        Check(!bridge.Contains("Time.") && !bridge.Contains("DelayAction") && !bridge.Contains("TeleportPlayer"), "Migration bridge contains no respawn timer, delay or replacement teleport");
        Check(actor.Contains("module.vitals.Died+=BeginNativeDeath"), "Existing fish vitality depletion reaches the native-death bridge");
        Check(bridge.Contains("nativeDeathEquipment.BeginCombatKnock()") && bridge.Contains("KnockRecoveryCompleted += OnNativeDeathRecovered"), "Owner enters the native knockout and observes native recovery completion");
        Check(Body(dive, "public bool BeginCombatKnock()").Contains("!base.IsOwner") && Body(dive, "public bool BeginCombatKnock()").Contains("OnOxygenDepleted();"), "Only the owning prepared player enters the existing native knockout method");
        string enter = Body(dive, "private void OnOxygenDepleted()");
        Check(enter.Replace("GetKnockVolume()", "GetDominantUnderwaterVolume()") == Body(oldDive, "private void OnOxygenDepleted()"), "Original death entry, animation, camera, sound/VFX, equipment cleanup, UI and reporting remain unchanged");
        string update = Body(dive, "private void UpdateKnock()");
        Check(update.Replace("!_isUnderwater && !_combatKnock", "!_isUnderwater").Replace("GetKnockVolume()", "GetDominantUnderwaterVolume()") == Body(oldDive, "private void UpdateKnock()"), "Original rescue/give-up progression and authored duration are retained; only dry-land instant-cancel is excluded for combat");
        Check(dive.Contains("if (_isKnocked && (base.ToolStatus || _combatKnock))"), "Combat knockout progresses on land even without diving equipment enabled");
        string finish = Body(dive, "private void FinishKnockAndTeleport()");
        string finishWithoutHooks = finish.Replace("\n\t\t\t\t_combatKnock = false;", "").Replace("\n\t\t\t\tKnockRecoveryCompleted?.Invoke();", "");
        Check(finishWithoutHooks == Body(oldDive, "private void FinishKnockAndTeleport()"), "Native item drop, death marker, default/boss respawn, oxygen/control restoration and daily reporting are preserved exactly");
        Check(finish.LastIndexOf("KnockRecoveryCompleted?.Invoke()", StringComparison.Ordinal) > finish.LastIndexOf("ServerRecordDailyDrownRpc()", StringComparison.Ordinal), "Fishing health recovery is notified after native return/teleport finishes");
        string rescue = Body(dive, "public void CancelKnockAndRecover(");
        Check(rescue.Replace("\n\t\t\t\t_combatKnock = false;", "").Replace("\n\t\t\t\tKnockRecoveryCompleted?.Invoke();", "") == Body(oldDive, "public void CancelKnockAndRecover("), "Original successful rescue and existing native recovery exits are preserved");
        Check(Body(dive, "public void ApplyOxygen(") == Body(oldDive, "public void ApplyOxygen("), "Native teammate oxygen rescue remains usable for a knocked-out fishing player");
        Check(Body(dive, "private static Transform ResolveKnockRespawnPoint()") == Body(oldDive, "private static Transform ResolveKnockRespawnPoint()"), "Recovery uses the original default/boss spawn resolver");
        Check(bridge.Contains("sender != NetworkManager.ServerClientId") && bridge.Contains("playerObject = actor.player.NetworkObjectId") && bridge.Contains("actor.player.NetworkObjectId == request.playerObject"), "Native death delivery authenticates server sender and exact player object");
        Check(network.Contains("RegisterNamedMessageHandler(NativeDeathChannel,ReceiveNativeDeath)") && network.Contains("UnregisterNamedMessageHandler(NativeDeathChannel)"), "Network session binds and unbinds the targeted death channel");
        Check(network.Contains("command.action!=\"native-recovered\"") && actor.Contains("case \"native-recovered\":CompleteNativeDeath(target)"), "Recovery acknowledgment is accepted while sea equipment is stowed");
        string complete = Body(bridge, "void CompleteNativeDeath(");
        Check(complete.Contains("!world.Server") && complete.Contains("!module.vitals.Down") && complete.Contains("string.Equals(serverDeathTicket, ticket, StringComparison.Ordinal)") && complete.Contains("module.vitals.Restore()"), "Only the server restores downed fishing health after a matching one-time native recovery ticket");
        Check(bridge.Contains("ticket == completedDeathTicket") && bridge.Contains("if (localDeathTicket != null) return;"), "Repeated delivery cannot restart an active or completed death sequence");
        Check(bridge.Contains("equipped = false;") && bridge.Contains("pendingEquipment = int.MinValue;") && bridge.Contains("module.Abort();") && bridge.Contains("ReleaseTools();") && bridge.Contains("left = previousLeft = right = previousRight = false;"), "Death clears queued equipment, held triggers, reeling, hook/line and held sea tools");
        Check(bridge.Contains("CancelPendingSelection()") && Read(Runtime + "FishingEquipmentWheel.cs").Contains("StopAllCoroutines();opening=choosing=null;choiceQueued=true;"), "Queued tool-wheel selection cannot re-equip a dead player");
        Check(actor.Contains("if(IsDefeated&&action!=\"control\"&&action!=\"stow\"&&action!=\"native-recovered\")return;") && network.Contains("if(actor.IsDefeated)return;if(Archipelago)"), "Downed players cannot fire, cast, equip, harvest or drive via stale commands");
        Check(Read(Runtime + "MigrationShotPresentation.cs").Contains("||victim.IsDefeated)return true;"), "Fish contact stops damaging a knocked-out player");
        Check(service.Contains("if(player&&player.DivingEquipment&&player.DivingEquipment.IsKnocked)return false;"), "Migration interaction yields to the native rescue/give-up input before catch or boat interaction");
        string interactor = Read(Native + "Core/Interaction/Interactor.cs");
        Check(interactor.Contains("Player.DivingEquipment.Interact(phase);"), "Original interaction still forwards input to the native knocked-player action");
        Check(Read(Runtime + "FishingHud.cs").Contains("actor.player.IsTabletActive||actor.IsDefeated"), "Sea health/event overlays and compass yield to the original death interface");
        Check(service.Contains("void ResetWorld(){\n            ClearNativeDeaths();") && service.Contains("void OnDestroy(){SaveWorld();ClearNativeDeaths();") && actor.Contains("Dispose(){ClearNativeDeath();") && Body(bridge, "internal void ClearNativeDeath()").Contains("KnockRecoveryCompleted -= OnNativeDeathRecovered"), "Exit, disconnect and actor disposal clear pending messages and native event subscriptions");
        string volume = Read(Native + "Core/Managers/GlobalVolumeManager.cs");
        Check(volume.Contains("source.profile.TryGet<Vignette>") && volume.Contains("UnityEngine.Object.Instantiate(vignette)") && volume.Contains("_combatKnockVolume.isGlobal = true;"), "Shore death reuses the original vignette on a visible global volume without copying underwater colour or blur");
        Check(volume.Contains("if (_combatKnockVolume != null) _combatKnockVolume.weight = 0f;") && volume.Contains("DestroyCombatKnockVolume();"), "Native effect exit fades out the shore overlay and scene teardown releases its temporary resources");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/PlayerNGO.prefab");
        var equipment = prefab.GetComponentInChildren<DivingEquipment>(true);
        Check(equipment && equipment.gameObject.activeSelf && equipment.enabled, "Real player prefab retains enabled native diving/death component independently of current equipped tool");
        var ui = Reference(equipment, "_divingEquipmentUI") as DivingEquipmentUI;
        Check(ui, "Native death UI is wired on the actual player prefab");
        var knockRoot = Reference(ui, "_knockUI") as GameObject;
        var oxygenRoot = Reference(ui, "_visualObject") as GameObject;
        Check(knockRoot && oxygenRoot && !knockRoot.transform.IsChildOf(oxygenRoot.transform), "Original knockout interface remains visible when ordinary diving/oxygen visuals are hidden on shore");
        for (Transform parent = knockRoot.transform.parent; parent; parent = parent.parent)
            Check(parent.gameObject.activeSelf, "Native knockout UI ancestor is active in the authored prefab: " + parent.name);
        foreach (string field in new[] { "_knockRadialImage", "_friendsMeterText" })
            Check(Reference(ui, field), "Original native death UI keeps " + field);
        foreach (string field in new[] { "_drowningSFX", "_drowningVFX", "_deadIconPrefab", "_sillyPufferManager" })
            Check(Reference(equipment, field), "Original death presentation keeps " + field);
        var serialized = new SerializedObject(equipment);
        Check(serialized.FindProperty("_knockDurationSeconds").floatValue == 30f && serialized.FindProperty("_giveUpAcceleration").floatValue == 5f, "Original prefab's 30-second rescue window and 5x hold-to-give-up behavior are unchanged; no migration timer was introduced");
        Debug.Log("NATIVE_FISHING_DEATH_VERIFIED checks=" + checks.Count + " play=false simulation=false");
    }
}
