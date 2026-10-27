using System;
using System.IO;
using UnityEngine;

namespace SushiAndFish.FishingIntegration {
    [Serializable]public sealed class BoatHomeRequest {public int version=1;public string id,slotKey;}
    public sealed partial class FishingMigration {
        float nextBoatHomeProbe;string appliedBoatHomeId;
        internal static bool ValidBoatHomeRequest(BoatHomeRequest request,string currentSlot)=>request!=null&&request.version==1&&request.slotKey==currentSlot&&Guid.TryParseExact(request.id,"N",out _);
        void TryApplyBoatHomeRequest(){
            if(!Server||!restored||!Archipelago||!Archipelago.Boat||SavePath==null||Time.unscaledTime<nextBoatHomeProbe)return;
            nextBoatHomeProbe=Time.unscaledTime+1;string path=SavePath+".home-request.json";if(!File.Exists(path))return;
            try{
                var request=JsonUtility.FromJson<BoatHomeRequest>(File.ReadAllText(path));if(!ValidBoatHomeRequest(request,slotKey))return;
                if(appliedBoatHomeId!=request.id){if(!Archipelago.ReturnBoatHome())return;appliedBoatHomeId=request.id;}
                // Archive only after the new boat pose has actually reached disk.
                // A running older session cannot undo the repair permanently:
                // an unprocessed request survives until this version loads it.
                if(SaveWorld()){File.Move(path,path+".applied-"+request.id);Notify(Local!=null?Local.player.OwnerClientId:network.LocalClientId,"船已返回寿司店旁的初始船位");}
            }catch(Exception error){nextBoatHomeProbe=Time.unscaledTime+5;Debug.LogWarning("Boat home request retained for retry: "+error.Message);}
        }
    }
}
