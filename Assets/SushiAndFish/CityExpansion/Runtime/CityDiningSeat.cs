using PaperPixelGames.Core.Ai.NPCs;
using UnityEngine;

namespace SushiAndFish.CityExpansion
{
    // Player animation, camera switching, occupancy RPC and exit behavior all
    // belong to the native EmoteArea. NPCs reserve that same occupied flag.
    [RequireComponent(typeof(EmoteArea))]
    public sealed class CityDiningSeat : MonoBehaviour
    {
        public Transform seatedPose,approach;
        public int tableId,seatId;
        public bool barStool;
        public EmoteArea Area {get;private set;}
        public ResidentController Resident {get;private set;}
        Collider interactionCollider;
        bool previousOccupied;
        public bool IsFree=>Area&&Area.IsSpawned&&!Area.IsOccupied&&!Resident;
        void Awake(){Area=GetComponent<EmoteArea>();interactionCollider=GetComponent<Collider>();}
        void Update()
        {
            if(!Area)return;
            bool occupied=Area.IsOccupied;
            if(occupied==previousOccupied)return;
            previousOccupied=occupied;
            if(interactionCollider)interactionCollider.enabled=!occupied;
            if(Area.WorldUIAnchor)Area.WorldUIAnchor.SetVisible(!occupied);
        }
        public bool TryReserve(ResidentController resident)
        {
            if(!IsFree||!resident||!Area.IsServer)return false;
            Resident=resident;
            Area.ServerChangeSomeoneUsingRpc(true);
            return true;
        }
        public void Release(ResidentController resident)
        {
            if(Resident!=resident)return;
            Resident=null;
            if(Area&&Area.IsSpawned&&Area.IsServer)Area.ServerChangeSomeoneUsingRpc(false);
        }
    }
}
