using System;
using PaperPixelGames.Core.PlayerTools;
using Unity.Netcode;
using UnityEngine;

namespace SushiAndFish.FishingIntegration
{
    [Serializable]
    sealed class NativeDeathRequest
    {
        public ulong playerObject;
        public string ticket;
    }

    public sealed partial class FishingMigration
    {
        const string NativeDeathChannel = "sushi.fishing.native-death.v1";
        NativeDeathRequest pendingNativeDeath;

        internal void RequestNativeDeath(FishingActor actor, string ticket)
        {
            if (!Server || !actor.player || !actor.player.IsSpawned) return;
            if (actor.player.IsOwner) actor.ReceiveNativeDeath(ticket);
            else SendJson(NativeDeathChannel, actor.player.OwnerClientId,
                JsonUtility.ToJson(new NativeDeathRequest { playerObject = actor.player.NetworkObjectId, ticket = ticket }));
        }

        void ReceiveNativeDeath(ulong sender, FastBufferReader reader)
        {
            if (Server || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out string json);
            if (json.Length > 512) return;
            var request = JsonUtility.FromJson<NativeDeathRequest>(json);
            if (request == null || !Guid.TryParseExact(request.ticket, "N", out _)) return;
            pendingNativeDeath = request;
            ApplyPendingNativeDeath();
        }

        void ApplyPendingNativeDeath()
        {
            if (pendingNativeDeath == null) return;
            var actor = Local;
            if (actor == null || !actor.player || !actor.player.IsSpawned) return;
            var request = pendingNativeDeath;
            pendingNativeDeath = null;
            // An earlier session or another player's message must not affect this player.
            if (actor.player.NetworkObjectId == request.playerObject) actor.ReceiveNativeDeath(request.ticket);
        }

        void ClearNativeDeaths()
        {
            pendingNativeDeath = null;
            foreach (var actor in actors.Values) actor.ClearNativeDeath();
        }
    }

    public sealed partial class FishingActor
    {
        string serverDeathTicket, localDeathTicket, completedDeathTicket;
        DivingEquipment nativeDeathEquipment;
        bool nativeDeathStarted;

        public bool IsDefeated => localDeathTicket != null || (module && module.vitals.Down) ||
            (player && player.DivingEquipment && player.DivingEquipment.IsKnocked);

        void StopDeathEquipment()
        {
            equipped = false;
            pendingEquipment = int.MinValue;
            inputActive = false;
            left = previousLeft = right = previousRight = false;
            message = null;
            if (equipmentWheel) equipmentWheel.CancelPendingSelection();
            if (module)
            {
                module.SetReeling(0);
                if (equipment >= 0) module.combat.guns[equipment].SetAim(false);
                module.Abort();
                ReleaseTools();
                RefreshToolGraphics();
            }
            if (visualRoot) visualRoot.SetActive(false);
            if (replicaLine) replicaLine.enabled = false;
        }

        void BeginNativeDeath()
        {
            if (!world.Server || serverDeathTicket != null) return;
            StopDeathEquipment();
            serverDeathTicket = Guid.NewGuid().ToString("N");
            world.RequestNativeDeath(this, serverDeathTicket);
        }

        internal void ReceiveNativeDeath(string ticket)
        {
            if (!player || !player.IsOwner || !Guid.TryParseExact(ticket, "N", out _)) return;
            if (ticket == completedDeathTicket)
            {
                Send("native-recovered", 1, ticket);
                return;
            }
            if (localDeathTicket != null) return;
            localDeathTicket = ticket;
            StopDeathEquipment();
            var pendingSelection = world.GetComponent<NativeSpearfishingAccess>();
            if (pendingSelection) pendingSelection.Cancel();
            TickNativeDeath();
        }

        void TickNativeDeath()
        {
            if (localDeathTicket == null || nativeDeathStarted || !player || !player.IsOwner ||
                !player.IsSpawned || !player.IsLocalGameplayPreparationReady || !player.DivingEquipment) return;
            if (nativeDeathEquipment != player.DivingEquipment)
            {
                if (nativeDeathEquipment) nativeDeathEquipment.KnockRecoveryCompleted -= OnNativeDeathRecovered;
                nativeDeathEquipment = player.DivingEquipment;
                nativeDeathEquipment.KnockRecoveryCompleted += OnNativeDeathRecovered;
            }
            nativeDeathStarted = nativeDeathEquipment.BeginCombatKnock();
        }

        void OnNativeDeathRecovered()
        {
            if (!nativeDeathStarted || localDeathTicket == null || !player || !player.IsOwner ||
                !player.IsSpawned || !nativeDeathEquipment || nativeDeathEquipment.IsKnocked) return;
            string ticket = localDeathTicket;
            nativeDeathEquipment.KnockRecoveryCompleted -= OnNativeDeathRecovered;
            nativeDeathEquipment = null;
            nativeDeathStarted = false;
            localDeathTicket = null;
            completedDeathTicket = ticket;
            // Native rescue or native return/teleport has finished. No migration respawn timer.
            Send("native-recovered", 1, ticket);
        }

        void CompleteNativeDeath(string ticket)
        {
            if (!world.Server || !module || !module.vitals.Down || serverDeathTicket == null ||
                !string.Equals(serverDeathTicket, ticket, StringComparison.Ordinal)) return;
            serverDeathTicket = null;
            module.vitals.Restore();
            // The player chooses a tool again through the original wheel after recovering.
        }

        internal void ClearNativeDeath()
        {
            if (nativeDeathEquipment) nativeDeathEquipment.KnockRecoveryCompleted -= OnNativeDeathRecovered;
            nativeDeathEquipment = null;
            nativeDeathStarted = false;
            serverDeathTicket = localDeathTicket = completedDeathTicket = null;
        }
    }
}
