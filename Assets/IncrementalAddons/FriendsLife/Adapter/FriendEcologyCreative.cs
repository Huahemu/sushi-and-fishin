using System;
using System.Linq;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class EcologyCreativeActivity
    {
        public string ruleLayer = "RCompatibleSource", worldId, sourceGoodsId, sourceReference, action;
        public string originalInputOwner, selectedBy, selectionOpId, definitionId, sessionId;
        public bool originalTravelVoucher, selectionAccepted;
    }

    public sealed partial class FriendsLifeRuntime
    {
        private EcologyCreativeActivity CaptureCreativeActivity(LifeSimulationRuntime kernel, PersonBinding person, string action)
        {
            var personal = kernel.State.creative?.residents.FirstOrDefault(r => r.residentId == person.nativeResidentId);
            var item = string.IsNullOrEmpty(personal?.heldGoodsId) ? null : kernel.CreativeCatalog.Goods(personal.heldGoodsId);
            return new EcologyCreativeActivity { worldId = State.worldId, action = action, sourceGoodsId = item?.id,
                sourceReference = item?.sourceReference, originalTravelVoucher = action == "Travel" };
        }

        private void LinkCreativeSourceActivity(string actorPersonId, string personId, string action, string definitionId, string sessionId, string operationId)
        {
            var episode = State.episodes.FirstOrDefault(e => e.sourceKind == "CreativeActivity" && !e.committed &&
                e.focusPersonId == personId && e.sourceId == action && !e.inputAccepted &&
                (string.IsNullOrEmpty(e.inputOwnerPersonId) || e.inputOwnerPersonId == actorPersonId ||
                 GetPerson(e.inputOwnerPersonId)?.controllerKind == PersonControllerKind.Npc));
            if (episode == null) return;
            // Older intents are linked only by an actual successful source input;
            // no old voucher, goods effect or automatic outcome is replayed.
            EcologyCreativeActivity data;
            if (string.IsNullOrEmpty(episode.optionId)) data = new EcologyCreativeActivity { worldId = State.worldId, action = action };
            else
            {
                if (!episode.optionId.TrimStart().StartsWith("{", StringComparison.Ordinal)) return;
                try { data = JsonUtility.FromJson<EcologyCreativeActivity>(episode.optionId); }
                catch (Exception) { return; }
            }
            if (data == null || data.worldId != State.worldId) return;
            data.originalInputOwner = episode.inputOwnerPersonId;
            data.selectedBy = actorPersonId; data.selectionOpId = operationId;
            data.definitionId = definitionId; data.sessionId = sessionId; data.selectionAccepted = true;
            episode.optionId = JsonUtility.ToJson(data); episode.inputAccepted = true;
            episode.inputOwnerPersonId = actorPersonId; episode.status = "SourceActivity";
            NotifyChanged();
        }

        // Photography calls this only after real capture and original
        // RecordActivityPhoto have already produced the persistent record.
        public bool LinkEcologyPhotoRecord(string actorPersonId, string personId, string photoRecordId, string operationId)
        {
            if (!Ready || !IsAuthority) return false;
            var actor = GetPerson(actorPersonId); var person = GetPerson(personId);
            var record = Core.State.activities?.photos.FirstOrDefault(p => p.id == photoRecordId && p.residentId == person?.nativeResidentId);
            if (actor == null || person == null || record == null ||
                actor.controllerKind == PersonControllerKind.Human && person.controllerKind == PersonControllerKind.Human && actorPersonId != personId) return false;
            LinkCreativeSourceActivity(actorPersonId, personId, "Photo", record.presetId, record.id, operationId);
            AdvanceEcologyCreativeActivities(); SaveNow(); return true;
        }

        private void AdvanceEcologyCreativeActivities()
        {
            foreach (var episode in State.episodes.Where(e => e.sourceKind == "CreativeActivity" && !e.committed && e.inputAccepted).ToArray())
            {
                if (string.IsNullOrEmpty(episode.optionId)) continue;
                if (!episode.optionId.TrimStart().StartsWith("{", StringComparison.Ordinal)) continue;
                EcologyCreativeActivity data;
                try { data = JsonUtility.FromJson<EcologyCreativeActivity>(episode.optionId); }
                catch (Exception) { continue; }
                if (data == null || data.worldId != State.worldId || !data.selectionAccepted || string.IsNullOrEmpty(data.sessionId)) continue;
                bool done = false;
                switch (data.action)
                {
                    case "Travel": done = Core.State.activities?.travels.Any(t => t.id == data.sessionId && t.returned) == true; break;
                    case "Game":
                        var game = Core.State.activities?.minigame;
                        done = game == null || game.id != data.sessionId || game.completed;
                        break;
                    case "Photo": done = Core.State.activities?.photos.Any(p => p.id == data.sessionId) == true; break;
                }
                if (!done) continue;
                // Finish/Return/Record already settled in the original source.
                // This closes only the original goods caller's workflow handle.
                episode.committed = true; episode.status = "Done"; episode.fault = "";
                NotifyChanged(); SaveNow();
            }
        }
    }
}
