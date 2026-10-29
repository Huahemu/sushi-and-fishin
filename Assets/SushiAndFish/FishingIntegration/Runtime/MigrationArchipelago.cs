using UnityEngine;
namespace SushiAndFish.FishingIntegration {
    public sealed partial class FishingMigration {
        public ArchipelagoWorld Archipelago{get;private set;}SailingSnapshot pendingSailing;
        void TickArchipelago(){if(actors.Count==0)return;if(!Archipelago){var settings=ArchipelagoSettings.Load();if(!settings||!settings.enabled)return;var root=new GameObject("Original islands and sailing");root.transform.SetParent(transform,false);Archipelago=root.AddComponent<ArchipelagoWorld>();Archipelago.Bind(this,settings);if(pendingSailing!=null)Archipelago.ApplySnapshot(pendingSailing);}
            Archipelago.Tick();
            TryApplyBoatHomeRequest();
        }
        internal void SendSailingInput(FishingActor actor,Vector2 input){var command=new FishingCommand{action="boat-input",position=new Vector3(input.x,input.y,0)};if(Server)HandleCommand(actor,command);else SendJson(CommandChannel,Unity.Netcode.NetworkManager.ServerClientId,JsonUtility.ToJson(command));}
        void ClearArchipelago(){ClearFreezers();if(Archipelago){Archipelago.Dispose();Archipelago.gameObject.SetActive(false);Destroy(Archipelago.gameObject);Archipelago=null;}pendingSailing=null;}
    }
}
