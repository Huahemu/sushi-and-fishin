using System;
using System.Linq;
using HowToFish;
using PaperPixelGames.Core.FishFridge;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SushiAndFish.FishingIntegration
{
    [Serializable] public sealed class FishFreezerPoint { public Vector3 position; public Quaternion rotation=Quaternion.identity; }
    public sealed class SharedFishFreezer : MonoBehaviour { }
    public sealed partial class FishingMigration
    {
        readonly RaycastHit[] interactionHits=new RaycastHit[128];
        GameObject freezerRoot;Fridge mainFishFridge;
        internal Fridge MainFishFridge {get {if(!mainFishFridge)mainFishFridge=FindObjectsByType<Fridge>(FindObjectsSortMode.None).FirstOrDefault();return mainFishFridge;}}
        internal void EnsureFreezers()
        {
            if(freezerRoot||!Archipelago||!Config.freezerPrefab)return;
            freezerRoot=new GameObject("Shared island fish freezers");freezerRoot.transform.SetParent(Archipelago.transform,false);
            foreach(var point in Config.freezerPoints??Array.Empty<FishFreezerPoint>())Instantiate(Config.freezerPrefab,point.position,point.rotation,freezerRoot.transform);
        }
        internal void ClearFreezers(){FishingFreezerPanel.CloseCurrent();if(freezerRoot)Destroy(freezerRoot);freezerRoot=null;mainFishFridge=null;}
        internal bool AimInteraction(FishingActor actor,out MigratedCatchIdentity fish,out Transform freezer)
        {
            fish=null;freezer=null;if(actor==null||!actor.Camera)return false;
            var ray=new Ray(actor.Camera.transform.position,actor.Camera.transform.forward);
            int count=Physics.SphereCastNonAlloc(ray,.14f,interactionHits,4.5f,~0,QueryTriggerInteraction.Collide);
            float closest=float.PositiveInfinity;Collider first=null;
            for(int i=0;i<count;i++)
            {
                var col=interactionHits[i].collider;if(!col||col.GetComponentInParent<PPG_Player>()==actor.player||col.gameObject.layer==LayerMask.NameToLayer("ViewModel"))continue;
                if(actor.player.Interactor.CurrentHeld&&col.GetComponentInParent<PaperPixelGames.Core.Interaction.BaseHoldable>()==actor.player.Interactor.CurrentHeld)continue;
                var id=col.GetComponentInParent<MigratedCatchIdentity>();var native=col.GetComponentInParent<Fridge>();var terminal=col.GetComponentInParent<SharedFishFreezer>();
                if(col.isTrigger&&!id&&!native&&!terminal)continue;
                var tool=col.GetComponentInParent<FishingItem>();if(tool&&tool.kind!=FishingItemKind.Catch)continue;
                if(interactionHits[i].distance<closest){closest=interactionHits[i].distance;first=col;}
            }
            if(!first)return false;fish=first.GetComponentInParent<MigratedCatchIdentity>();
            var fridge=first.GetComponentInParent<Fridge>();var shared=first.GetComponentInParent<SharedFishFreezer>();
            if(fridge&&fridge.IsPhysicallyUsable)freezer=fridge.transform;else if(shared)freezer=shared.transform;
            return fish||freezer;
        }
        internal bool FreezerNear(FishingActor actor)
        {
            if(actor==null||!actor.player)return false;var p=actor.player.transform.position;
            if(MainFishFridge&&MainFishFridge.IsPhysicallyUsable&&(MainFishFridge.transform.position-p).sqrMagnitude<36)return true;
            return Config.freezerPoints!=null&&Config.freezerPoints.Any(f=>(f.position-p).sqrMagnitude<36);
        }
        internal WarehouseLine[] SharedStock()
        {
            var list=SaveData.stock.GroupBy(s=>s.itemId).Select(g=>new WarehouseLine{itemId=g.Key,count=g.LongCount()}).ToList();
            if(MainFishFridge)foreach(var stock in MainFishFridge.Save().FridgeFishes??new System.Collections.Generic.List<PaperPixelGames.Core.SaveSystem.FridgeFishData>())
            {var row=list.Find(r=>r.itemId==stock.Id);if(row==null){row=new WarehouseLine{itemId=stock.Id};list.Add(row);}row.count+=stock.Weights?.Count??0;}
            return list.Where(r=>r.count>0).OrderBy(r=>r.itemId,StringComparer.Ordinal).ToArray();
        }
        public static bool DepositFromNativeFridge(Fridge fridge,PaperPixelGames.Core.Interaction.Interactor interactor)
        {
            if(!Enabled||!fridge||!fridge.IsPhysicallyUsable||!interactor||!interactor.Player.IsOwner)return false;
            var actor=Instance.Local;if(actor==null)return false;actor.Send("freezer-deposit",1);return true;
        }
        internal bool RenderSharedHints(FishingNativeHints hints,FishingActor actor)
        {
            if(!AimInteraction(actor,out var fish,out var freezer))return false;
            if(freezer){hints.RenderWaterAccess(actor.player.Interactor.CurrentHeld is DeadFish?"E 存入手中鱼 · F 打开共享冰柜":"E 打开共享冰柜",null);return true;}
            if(fish&&FishSnapshots.TryGetValue(fish.token,out var snapshot)&&snapshot.health<=0){hints.RenderWaterAccess("E 拾取鱼 · 自动收起海钓装备",null);return true;}
            return false;
        }
        internal void TickFreezerInput(FishingActor actor)
        {
            if(actor==null||!actor.player.IsOwner||actor.IsDefeated||Keyboard.current==null||FishingFreezerPanel.IsOpen||actor.player.IsTabletActive)return;
            if(!actor.ControlsAllowed&&!(Archipelago&&Archipelago.IsDriver(actor.player)))return;
            if(Keyboard.current.f5Key.wasPressedThisFrame)actor.Send("boat-recall",1);
            if(Keyboard.current.fKey.wasPressedThisFrame&&AimInteraction(actor,out _,out var station)&&station)FishingFreezerPanel.Open(actor,station);
        }
    }
}
