using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SushiAndFish.ResidentialIsland;
// Independent physical measurements of the rendered Humanoid hierarchy and mesh.
public sealed class MergedHousingMotionProbe:IDisposable
{
    readonly Animator animator;
    readonly HousingRestSession session;
    readonly Transform[] bones;
    readonly float[] lengths;
    readonly Vector3[] scales;
    readonly Quaternion[] previous;
    readonly Mesh mesh=new Mesh();
    float lastTime=-1;
    public MergedHousingMotionProbe(HousingRestSession value)
    {
        session=value;animator=value.PoseAnimator;
        bones=Enumerable.Range(0,(int)HumanBodyBones.LastBone).Select(i=>animator.GetBoneTransform((HumanBodyBones)i)).Where(t=>t).ToArray();
        lengths=bones.Select(t=>t.localPosition.magnitude).ToArray();scales=bones.Select(t=>t.localScale).ToArray();previous=bones.Select(t=>t.localRotation).ToArray();
    }
    public MergedHousingAudit.MotionRow Measure(string home)
    {
        var row=new MergedHousingAudit.MotionRow{home=home,kind=session.Target.kind.ToString(),phase=session.Phase,time=session.Elapsed,minimumSole=999,minimumMeshY=999};
        float dt=lastTime<0?0:session.Elapsed-lastTime;lastTime=session.Elapsed;
        for(int i=0;i<bones.Length;i++)
        {
            row.scaleError=Mathf.Max(row.scaleError,Vector3.Distance(bones[i].localScale,scales[i]));
            if(bones[i]!=animator.GetBoneTransform(HumanBodyBones.Hips)&&lengths[i]>.01f){float delta=Mathf.Abs(bones[i].localPosition.magnitude/lengths[i]-1);if(delta>row.segmentChange){row.segmentChange=delta;row.segmentBone=bones[i].name;}}
            if(dt>.002f){float speed=Quaternion.Angle(previous[i],bones[i].localRotation)/dt;if(speed>row.angularSpeed){row.angularSpeed=speed;row.angularBone=bones[i].name;}}
            previous[i]=bones[i].localRotation;
        }
        Transform B(HumanBodyBones b)=>animator.GetBoneTransform(b);
        foreach(var side in new[]{0,1})
        {
            var upper=B(side==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);var knee=B(side==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);var foot=B(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
            row.kneeBend=Mathf.Max(row.kneeBend,180-Vector3.Angle(upper.position-knee.position,foot.position-knee.position));
        }
        var feet=new[]{B(HumanBodyBones.LeftFoot).position,B(HumanBodyBones.RightFoot).position};
        foreach(var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.enabled&&s.gameObject.activeInHierarchy&&s.sharedMesh))
        {
            skin.BakeMesh(mesh);var matrix=skin.transform.localToWorldMatrix;
            foreach(var v in mesh.vertices)
            {
                var world=matrix.MultiplyPoint3x4(v);float y=world.y-session.Target.exit.position.y;row.minimumMeshY=Mathf.Min(row.minimumMeshY,y);
                if(feet.Any(f=>new Vector2(f.x-world.x,f.z-world.z).sqrMagnitude<.22f*.22f&&world.y<f.y+.12f))row.minimumSole=Mathf.Min(row.minimumSole,y);
            }
        }
        row.handError=session.Target.handSupport?Vector3.Distance(B(HumanBodyBones.RightHand).position,session.Target.handSupport.position):-1;
        row.lid=session.Target.toilet?session.Target.toilet.OpenAmount:-1;return row;
    }
    public void Dispose(){UnityEngine.Object.Destroy(mesh);}
}
