using System.Linq;
using HowToFish;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Player;
using UnityEngine;
namespace SushiAndFish.FishingIntegration {
    public static class ArchipelagoBoatSafety {
        static readonly RaycastHit[] hits=new RaycastHit[96];static readonly Collider[] overlaps=new Collider[128];
        static bool Ignore(Collider c,Transform boat)=>!c||c.isTrigger||(boat&&c.transform.IsChildOf(boat))||c.GetComponentInParent<PPG_Player>()||c.GetComponentInParent<FishingItem>()||c.GetComponentInParent<DeadFish>();
        public static bool ClearWater(Vector3 position,Quaternion rotation,float sea,Transform boat=null){
            foreach(float x in new[]{-1.8f,0,1.8f})foreach(float z in new[]{-4.9f,0,4.9f}){var p=position+rotation*new Vector3(x,0,z);int count=Physics.RaycastNonAlloc(new Vector3(p.x,sea+8,p.z),Vector3.down,hits,30,~0,QueryTriggerInteraction.Ignore);if(count==hits.Length)return false;for(int i=0;i<count;i++)if(!Ignore(hits[i].collider,boat)&&hits[i].point.y>sea-1.1f)return false;}
            var center=position+rotation*new Vector3(0,.55f,-.3f);int n=Physics.OverlapBoxNonAlloc(center,new Vector3(1.8f,.85f,4.7f),overlaps,rotation,~0,QueryTriggerInteraction.Ignore);if(n==overlaps.Length)return false;for(int i=0;i<n;i++)if(!Ignore(overlaps[i],boat))return false;return true;
        }
        public static bool NeedsRecovery(OriginalBoatRig boat,float sea){if(!boat)return false;if(boat.transform.position.y>sea+.9f||boat.transform.position.y<sea-1.5f||Vector3.Dot(boat.transform.up,Vector3.up)<.35f)return true;if(boat.Speed>.8f)return false;int count=Physics.RaycastNonAlloc(boat.transform.position+Vector3.up*.15f,Vector3.down,hits,2,~0,QueryTriggerInteraction.Ignore);for(int i=0;i<count;i++)if(!Ignore(hits[i].collider,boat.transform)&&hits[i].point.y>sea-.3f)return true;return false;}
        public static bool TryFind(ArchipelagoSettings settings,Vector3 origin,float yaw,float sea,Transform ignore,out Vector3 position,out Quaternion rotation){
            rotation=Quaternion.Euler(0,yaw,0);position=default;var nearest=settings.regions.OrderBy(r=>Vector3.ProjectOnPlane(origin-r.center,Vector3.up).sqrMagnitude).First();var away=Vector3.ProjectOnPlane(origin-nearest.center,Vector3.up);if(away.sqrMagnitude<1)away=Vector3.ProjectOnPlane(nearest.dock-nearest.center,Vector3.up);float bearing=Mathf.Atan2(away.x,away.z)*Mathf.Rad2Deg;
            foreach(var start in new[]{origin,nearest.dock,settings.boatSpawn})foreach(float radius in new[]{0f,6,10,16,24,36,52,72,100})for(int i=0;i<(radius==0?1:24);i++){float angle=bearing+i*15;var candidate=start+Quaternion.Euler(0,angle,0)*Vector3.forward*radius;candidate.y=sea+.25f;if(Vector3.Distance(candidate,settings.boatSpawn)>2400)continue;if(ClearWater(candidate,rotation,sea,ignore)){position=candidate;return true;}}return false;
        }
        public static bool SavedPoseUsable(Vector3 position,Quaternion rotation,float sea)=>position.y<=sea+.9f&&position.y>=sea-1.5f&&Vector3.Dot(rotation*Vector3.up,Vector3.up)>.35f;
    }
}
