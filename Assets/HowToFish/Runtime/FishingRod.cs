using UnityEngine;
namespace HowToFish {
    public sealed class FishingRod:MonoBehaviour {
        public FishingModule module;
        public FishingItem item;
        public Transform tip,crank,visualCrank,crankHandle,reel;
        public Transform[] segments,lineGuides;
        Quaternion[] rest,bends;
        Quaternion crankRest,handleRest,reelRest;
        float castPose,bendForce,bendVelocity;
        public Quaternion HandRotation=>Quaternion.Euler(-55*castPose,0,0);
        void Awake(){rest=new Quaternion[segments.Length];bends=new Quaternion[segments.Length];for(int i=0;i<segments.Length;i++){rest[i]=segments[i].localRotation;bends[i]=Quaternion.identity;}if(visualCrank)crankRest=visualCrank.localRotation;if(crankHandle)handleRest=crankHandle.localRotation;if(reel)reelRest=reel.localRotation;}
        public void Pose(float tension,float reeling){
            if(!item.owner)return;
            if(!item.owner.tracked){castPose=Mathf.MoveTowards(castPose,module.State==FishingState.Charging?module.CastPower:0,Time.deltaTime*5);if(!item.ExternalGripPose)transform.localRotation=HandRotation;}
            for(int i=0;i<segments.Length;i++)segments[i].localRotation=rest[i];
            bendForce=Mathf.SmoothDamp(bendForce,tension,ref bendVelocity,.1f);
            for(int i=0;i<segments.Length;i++){
                var part=segments[i];var direction=(i+1<segments.Length?segments[i+1].position:tip.position)-part.position;var targetPoint=module.WorldToTool!=null?module.WorldToTool(module.hookBody.position):module.hookBody.position;var toward=targetPoint-part.position;float portion=module.profile.bendDistribution.Evaluate(i/(float)(segments.Length-1));
                Quaternion target=Quaternion.identity;
                if(direction.sqrMagnitude>.0001f&&toward.sqrMagnitude>.0001f&&bendForce>.001f)target=Quaternion.FromToRotation(direction.normalized,Vector3.RotateTowards(direction.normalized,toward.normalized,10*bendForce*portion*Mathf.Deg2Rad,0));
                bends[i]=Quaternion.Slerp(bends[i],target,Time.deltaTime*10);part.rotation=bends[i]*part.rotation;
            }
            float line=module.LineLength;
            if(visualCrank)visualCrank.localEulerAngles=new Vector3(0,90,-line*100);
            if(crankHandle)crankHandle.localEulerAngles=new Vector3(visualCrank.localEulerAngles.z,90,-90);
            if(reel)reel.localEulerAngles=new Vector3(0,-line*500,0);
        }
        public void ResetSteering(){
            castPose=bendForce=bendVelocity=0;
            if(rest!=null)for(int i=0;i<segments.Length;i++){segments[i].localRotation=rest[i];bends[i]=Quaternion.identity;}
            if(visualCrank)visualCrank.localRotation=crankRest;if(crankHandle)crankHandle.localRotation=handleRest;if(reel)reel.localRotation=reelRest;
            if(item.owner&&!item.owner.tracked)transform.localRotation=Quaternion.identity;
        }
    }
}
