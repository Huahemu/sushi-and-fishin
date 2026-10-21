using UnityEngine;
namespace SushiAndFish.ResidentialIsland
{
    // Unity's Humanoid solver preserves segment lengths. No transform writes.
    public sealed class HousingRestIK : MonoBehaviour
    {
        [System.NonSerialized]public HousingRestSession session;
        [System.NonSerialized]public bool apply;
        [System.NonSerialized]public float sit,lie,brace;
        static float Ease(float from,float to,float value)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,value));
        Quaternion handFrame;bool hasHandFrame;
        void OnAnimatorIK(int layer)
        {
            if(session==null||!session.Active)return;
            if(!apply)
            {
                var raw=GetComponent<Animator>();foreach(var goal in new[]{AvatarIKGoal.LeftFoot,AvatarIKGoal.RightFoot,AvatarIKGoal.LeftHand,AvatarIKGoal.RightHand}){raw.SetIKPositionWeight(goal,0);raw.SetIKRotationWeight(goal,0);}return;
            }
            var a=GetComponent<Animator>();var rest=session.Target;
            for(int side=0;side<2;side++)
            {
                var goal=side==0?AvatarIKGoal.LeftFoot:AvatarIKGoal.RightFoot;
                float weight=(rest.kind==HousingRestKind.Toilet?Ease(0,.4f,sit):sit)*(1-Ease(0,.65f,lie));
                var p=rest.seatHip.position+rest.seatHip.forward*.36f+rest.seatHip.right*(side==0?-.12f:.12f);
                var start=session.StartFeet[side];p=Vector3.Lerp(start,p,sit);
                p.y=rest.exit.position.y+(side==0?a.leftFeetBottomHeight:a.rightFeetBottomHeight)+.015f;
                var thigh=a.GetBoneTransform(side==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                var knee=a.GetBoneTransform(side==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                var foot=a.GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                float reach=Vector3.Distance(thigh.position,knee.position)+Vector3.Distance(knee.position,foot.position);
                weight*=1-Ease(.8f,1.02f,Vector3.Distance(thigh.position,p)/reach);
                a.SetIKPositionWeight(goal,weight);a.SetIKRotationWeight(goal,weight);
                a.SetIKPosition(goal,p);a.SetIKRotation(goal,rest.seatHip.rotation);
                var hint=side==0?AvatarIKHint.LeftKnee:AvatarIKHint.RightKnee;
                a.SetIKHintPositionWeight(hint,weight);a.SetIKHintPosition(hint,thigh.position+rest.seatHip.forward*.7f+rest.seatHip.right*(side==0?-.15f:.15f));
            }
            float hand=rest.kind==HousingRestKind.Bed?sit*brace*(1-Ease(.5f,1,lie)):0;
            if(rest.handSupport)
            {
                var shoulder=a.GetBoneTransform(HumanBodyBones.RightUpperArm);var elbow=a.GetBoneTransform(HumanBodyBones.RightLowerArm);var wrist=a.GetBoneTransform(HumanBodyBones.RightHand);
                float reach=Vector3.Distance(shoulder.position,elbow.position)+Vector3.Distance(elbow.position,wrist.position);
                hand*=1-Ease(.95f,1.02f,Vector3.Distance(shoulder.position,rest.handSupport.position)/reach);
                a.SetIKPositionWeight(AvatarIKGoal.RightHand,hand);a.SetIKRotationWeight(AvatarIKGoal.RightHand,hand);
                a.SetIKPosition(AvatarIKGoal.RightHand,rest.handSupport.position);
                if(!hasHandFrame&&hand>.01f)
                {
                    var middle=a.GetBoneTransform(HumanBodyBones.RightMiddleProximal);var index=a.GetBoneTransform(HumanBodyBones.RightIndexProximal);var little=a.GetBoneTransform(HumanBodyBones.RightLittleProximal);
                    var normal=Vector3.Cross(index.position-wrist.position,little.position-wrist.position).normalized;if(Vector3.Dot(normal,Vector3.up)<0)normal=-normal;
                    handFrame=Quaternion.Inverse(wrist.rotation)*Quaternion.LookRotation(middle.position-wrist.position,normal);hasHandFrame=true;
                }
                var rotation=hasHandFrame?Quaternion.LookRotation(-rest.seatHip.forward,Vector3.up)*Quaternion.Inverse(handFrame):rest.handSupport.rotation;
                a.SetIKRotation(AvatarIKGoal.RightHand,rotation);
            }
        }
    }
}
