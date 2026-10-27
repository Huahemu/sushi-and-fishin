using System.Linq;
using HowToFish;
using UnityEngine;

namespace SushiAndFish.FishingIntegration {
    public static class FishingGripPose {
        public static Vector3 Palm(Transform wrist,Transform[] fingers){
            if(!wrist)return Vector3.zero;if(fingers==null||fingers.Length==0)return wrist.TransformPoint(new Vector3(.065f,-.005f,.02f));
            Vector3 center=Vector3.zero;foreach(var finger in fingers)center+=finger.position;return Vector3.Lerp(wrist.position,center/fingers.Length,.65f);
        }
        public static Vector3 ProjectPalm(Camera overlay,Camera world,Vector3 palm,float depth){
            var viewport=overlay.WorldToViewportPoint(palm);return world.ViewportToWorldPoint(new Vector3(viewport.x,viewport.y,depth));
        }
        public static Vector3 ProjectToolPoint(Camera from,Camera to,Vector3 point){
            if(!from||!to||from==to)return point;
            return to.ViewportToWorldPoint(from.WorldToViewportPoint(point));
        }
        public static Vector3 RootPosition(Vector3 palm,Quaternion rotation,Vector3 sourceGrip,Vector3 scale)=>palm-rotation*Vector3.Scale(sourceGrip,scale);
        public static Transform[] NativeFingers(Transform hand)=>hand?hand.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="index_02_r"||t.name=="middle_01_r"||t.name=="thumb_03_r"||t.name=="index_02_l"||t.name=="middle_01_l"||t.name=="thumb_03_l").ToArray():new Transform[0];
        public static Transform[] AuthoredFingers(Transform tool){
            // The rod has no authored hand bones. Its old zero fallback put
            // the handle 7.4 cm to the right, against the back of the hand.
            if(tool.GetComponent<FishingRod>())return tool.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="RodBase").Take(1).ToArray();
            return tool.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="r_index_low"||t.name=="r_middle_low"||t.name=="r_thumb_mid").ToArray();
        }
        public static Vector3 AuthoredGrip(Transform tool,Transform[] fingers){
            if(fingers!=null&&fingers.Length==1&&fingers[0].name=="RodBase"){var axis=tool.InverseTransformPoint(fingers[0].position);axis.z=0;return axis;}
            if(fingers!=null&&fingers.Length>0){Vector3 point=Vector3.zero;foreach(var t in fingers)point+=t.position;return tool.InverseTransformPoint(point/fingers.Length);}
            return Vector3.zero;
        }
    }
}
