namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Linq;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::MoreMountains.Tools;
using global::UnityEngine;

namespace CasinoLab
{
    public static class CasinoLabSelection
    {
        static GameObject currentCluster;
        public static GameBase SelectedGame {get;private set;}
        public static string[] Names=>SushiAndFish.Casino.Original.CasinoResources.Load<MMLootTableGameObjectSO>("FloorLootTables/Floor 4").LootTable.ObjectsToLoot.Select(e=>e.Loot.name.Replace("_Cluster","")).ToArray();
        public static void Select(int index)
        {
            if(!CasinoLabBootstrap.Instance.ready)return;
            CasinoLabBootstrap.Instance.StartCoroutine(ReplaceCluster(index));
        }
        public static IEnumerator ReplaceCluster(int index)
        {
            var boot=CasinoLabBootstrap.Instance;
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="CasinoScene")yield break;
            var floor=UnityEngine.Object.FindObjectsByType<CasinoFloor>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(f=>f.floorIndex==4);
            if(currentCluster==null)
                currentCluster=floor.GetComponentsInChildren<Transform>(true).Where(t=>t.name.EndsWith("_Cluster(Clone)")).OrderBy(t=>t.position.sqrMagnitude).First().gameObject;
            if(currentCluster.GetComponentsInChildren<GameBase>(true).Any(g=>g.isPlaying))
            {boot.status="Finish the game in the selected test area before replacing it.";yield break;}
            boot.ready=false;
            var pos=currentCluster.transform.position;var rot=currentCluster.transform.rotation;var scale=currentCluster.transform.localScale;var parent=currentCluster.transform.parent;
            var prefab=SushiAndFish.Casino.Original.CasinoResources.Load<MMLootTableGameObjectSO>("FloorLootTables/Floor 4").LootTable.ObjectsToLoot[index].Loot;
            foreach(var identity in currentCluster.GetComponentsInChildren<NetworkIdentity>(true).Reverse())if(identity!=null && identity.gameObject!=currentCluster)NetworkServer.Destroy(identity.gameObject);
            NetworkServer.Destroy(currentCluster);
            yield return null;
            var elevator=NetworkSingleton<ElevatorManager>.Instance;
            if(CasinoLabBootstrap.Field<int>(elevator,"_currentFloorIndex")!=4)
            {
                elevator.ServerForceTeleportPlayers(4);
                while(elevator.isTeleporting)yield return null;
            }
            currentCluster=UnityEngine.Object.Instantiate(prefab,pos,rot);currentCluster.transform.localScale=scale;
            NetworkServer.Spawn(currentCluster);
            foreach(Transform child in currentCluster.transform)
                foreach(Transform item in child)
                    if(item.TryGetComponent<GameStamp>(out var stamp)){stamp.floor=floor;stamp.Initialize();}
            currentCluster.transform.SetParent(parent,true);
            yield return new WaitForSeconds(0.4f);
            // A child game is spawned by GameStamp and parented by its native
            // Mirror RPC. Wait for that hierarchy rather than a fixed frame.
            var hierarchyDeadline=Time.realtimeSinceStartup+8f;
            while(!currentCluster.GetComponentInChildren<GameBase>(true)&&Time.realtimeSinceStartup<hierarchyDeadline)yield return null;
            SelectedGame=currentCluster.GetComponentsInChildren<GameBase>(true).FirstOrDefault();
            if(SelectedGame!=null)
            {
                foreach(var p in MonoSingleton<LocalManager>.Instance.players)
                {
                    var target=SelectedGame.Keypad!=null?SelectedGame.Keypad.transform:SelectedGame.transform;
                    var look=(target.position-currentCluster.transform.position);look.y=0;
                    if(look.sqrMagnitude<0.1f)look=-target.forward;
                    look.Normalize();
                    var start=target.position+look*2.2f;start.y=1.1f;
                    p.controller.ServerTeleport(start);
                    var dir=target.position-start;dir.y=0;p.controller.ServerRotate(new Vector2(Quaternion.LookRotation(dir).eulerAngles.y,0));
                }
            }
            RecoveryShaderSupport.RequestRuntimeRefresh();
            boot.status="Selected original cluster: "+prefab.name+" (floor 4 test position)";boot.ready=true;
            Debug.Log("CASINO_LAB_CLUSTER_SELECTED "+prefab.name);
        }
    }
}

}
