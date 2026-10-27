using UnityEngine;
namespace SushiAndFish.FishingIntegration {
    public static class ArchipelagoWaterMath {
        public static Vector3 EntryVelocity(Vector3 velocity,Vector3 up,float desiredUp,float dt,bool entering){
            float vertical=Vector3.Dot(velocity,up),next=vertical;
            if(entering)next=Mathf.Max(next,-2.2f);
            else if(desiredUp>=-.1f&&vertical<0)next=Mathf.MoveTowards(Mathf.Max(vertical,-2.2f),0,10*Mathf.Max(0,dt));
            return velocity+up*(next-vertical);
        }
        public static Vector3 ClimbPosition(Vector3 start,Vector3 end,float progress){
            progress=Mathf.Clamp01(progress);var lift=new Vector3(start.x,end.y+.16f,start.z);
            return progress<.55f?Vector3.Lerp(start,lift,Mathf.SmoothStep(0,1,progress/.55f)):Vector3.Lerp(lift,end,Mathf.SmoothStep(0,1,(progress-.55f)/.45f));
        }
        public static Bounds WaterBounds(ArchipelagoSettings settings,float sea){var bounds=new Bounds(settings.regions[0].center,new Vector3(1000,0,1000));foreach(var region in settings.regions){bounds.Encapsulate(region.center+new Vector3(region.fishingRadius+180,0,region.fishingRadius+180));bounds.Encapsulate(region.center-new Vector3(region.fishingRadius+180,0,region.fishingRadius+180));}bounds.center=new Vector3(bounds.center.x,sea+.1f-150,bounds.center.z);bounds.size=new Vector3(bounds.size.x,300,bounds.size.z);return bounds;}
    }
}
