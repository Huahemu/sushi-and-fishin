using System;
using System.Collections.Generic;
using System.Linq;
using TomodachiLife;
using TomodachiNative;
using TomodachiPortData;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    /// <summary>Samples copied source curve/timing functions and projects rotations onto the host humanoid.
    /// Only a skeleton and motion data are used. No source character mesh, face or clothing is instantiated.</summary>
    [DefaultExecutionOrder(900)]
    public sealed class OriginalMotionRetargeter:MonoBehaviour
    {
        [Serializable] sealed class LibraryIndex{public ClipInfo[] timings;public Ambiguity[] ambiguous;}
        [Serializable] sealed class ClipInfo{public string animation,source,archive,resource;public int frames;public bool loop;}
        [Serializable] sealed class Ambiguity{public string animation;}
        sealed class BoneMap{public Transform source,target,photoAxisChild;public Quaternion sourceBind,targetBind;public int depth;public Vector3 photoBindAxis;public bool photoAxisReady;}
        static NativeAnimationStateGraph sourceGraph;
        static LibraryIndex library;
        static readonly Dictionary<string,NativeAnimationAsset> banks=new Dictionary<string,NativeAnimationAsset>(StringComparer.Ordinal);
        static NativeModel skeleton;
        Animator targetAnimator;
        NativeRig rig;
        NativeAnimationPlayer sampler;
        NativeStateGraphRealtimeSession clock;
        NativeAnimationPlayer.PreparedBlend prepared;
        readonly List<BoneMap> mapping=new List<BoneMap>();
        float[] sampleFrames;
        int[] manualNodes;
        bool manualMode;
        bool photoMode,savedPhotoRootProjection;
        static NativeAnimationAsset photoPreviewBank;
        static NativeAnimationStateGraph photoUiGraph;
        bool animatorWasEnabled;
        Vector3 sourceHipBind,targetHipBind;
        Vector3 savedHipLocal;
        float rootScale=1;
        public bool ProjectRootPose;
        string key;
        public string Command{get;private set;}
        public string ExecutionKey=>key;
        public string LastError{get;private set;}
        public bool Finished=>clock!=null&&clock.ClipRangesFinished;
        public double SourceSeconds=>clock==null?0:clock.ElapsedSourceFrames/clock.SourceFramesPerSecond;
        public bool IsPlaying=>clock!=null||manualMode;
        public static NativeAnimationStateGraph SourceGraph{get{Load();return sourceGraph;}}
        static readonly Dictionary<string,HumanBodyBones> boneNames=new Dictionary<string,HumanBodyBones>
        {
            {"Skl_Root",HumanBodyBones.Hips},{"Spine_1",HumanBodyBones.Spine},{"Spine_2",HumanBodyBones.Chest},
            {"Neck",HumanBodyBones.Neck},{"Head",HumanBodyBones.Head},
            {"Clavicle_L",HumanBodyBones.LeftShoulder},{"Arm_1_L",HumanBodyBones.LeftUpperArm},{"Arm_2_L",HumanBodyBones.LeftLowerArm},{"Wrist_L",HumanBodyBones.LeftHand},
            {"Clavicle_R",HumanBodyBones.RightShoulder},{"Arm_1_R",HumanBodyBones.RightUpperArm},{"Arm_2_R",HumanBodyBones.RightLowerArm},{"Wrist_R",HumanBodyBones.RightHand},
            {"Leg_1_L",HumanBodyBones.LeftUpperLeg},{"Leg_2_L",HumanBodyBones.LeftLowerLeg},{"Ankle_L",HumanBodyBones.LeftFoot},{"Toe_L",HumanBodyBones.LeftToes},
            {"Leg_1_R",HumanBodyBones.RightUpperLeg},{"Leg_2_R",HumanBodyBones.RightLowerLeg},{"Ankle_R",HumanBodyBones.RightFoot},{"Toe_R",HumanBodyBones.RightToes}
        };
        static void Load()
        {
            if(sourceGraph!=null)return;
            var graph=Resources.Load<TextAsset>("FriendsLife/SourceMotion/Mii.graph");var meta=Resources.Load<TextAsset>("FriendsLife/SourceMotion/index");var body=Resources.Load<TextAsset>("FriendsLife/SourceMotion/Skeleton");
            if(!graph||!meta||!body)throw new OriginalBindingException("Original motion data is not installed");
            sourceGraph=JsonUtility.FromJson<NativeAnimationStateGraph>(graph.text);library=JsonUtility.FromJson<LibraryIndex>(meta.text);skeleton=JsonUtility.FromJson<NativeModel>(body.text);
        }
        static NativeAnimationAsset Bank(string clip,string archive)
        {
            var candidates=(library.timings??Array.Empty<ClipInfo>()).Where(c=>c.animation==clip).ToArray();
            var exact=candidates.Where(c=>c.archive==archive).ToArray();
            var binding=exact.Length==1?exact[0]:candidates.Length==1?candidates[0]:throw new OriginalBindingException("Original motion bank requires its actual DemoParam archive: "+clip+" / "+archive);
            if(banks.TryGetValue(binding.resource,out var bank))return bank;
            var text=Resources.Load<TextAsset>("FriendsLife/SourceMotion/"+binding.resource);if(!text)throw new OriginalBindingException("Original person motion clip is unavailable: "+binding.resource);
            bank=ScriptableObject.CreateInstance<NativeAnimationAsset>();bank.name=clip;bank.data=JsonUtility.FromJson<NativeAnimationBank>(text.text);banks.Add(binding.resource,bank);return bank;
        }
        public bool PlaySource(string command,string executionKey,double resumeSeconds,out string error)
            =>PlaySource(command,executionKey,resumeSeconds,"",out error);
        public bool PlaySource(string command,string executionKey,double resumeSeconds,string sourceArchive,out string error)
        {
            error=LastError=null;
            if(key==executionKey&&clock!=null)return true;
            try
            {
                Load();EnsureRig();
                var values=new NativeStateGraphParameters();
                if(!new NativeAnimationStateGraphEvaluator(sourceGraph).TrySelectCommand(command,values,out var selection))throw new OriginalBindingException("Original AS command selection unsupported: "+command+"; "+string.Join("; ",selection.issues.Select(i=>i.code+":"+i.detail)));
                var selected=selection.clips.Select(c=>Bank(c.animation,sourceArchive)).Distinct().ToArray();
                var options=new NativeStateGraphRealtimeOptions{sourceFramesPerSecond=60,clockProvenance="Copied NativeAnimationPlayer.framesPerSecond=60 caller clock; no original-platform clock parity claim",initialClock=new NativeStateGraphSampleClock{elapsedFrames=resumeSeconds*60}};
                if(!NativeAnimationStateGraphPlayback.TryCreateRealtimeSession(sourceGraph,selection,values,selected,options,out var session,out var samples,out var plan,out var issue))throw new OriginalBindingException(command+": "+issue);
                if(!sampler.TryPrepareBlend(samples,out var blend,out issue))throw new OriginalBindingException(command+": "+issue);
                Stop();prepared=blend;clock=session;sampleFrames=new float[session.SampleCount];key=executionKey;Command=command;
                animatorWasEnabled=targetAnimator.enabled;targetAnimator.enabled=false;
                savedHipLocal=targetAnimator.GetBoneTransform(HumanBodyBones.Hips).localPosition;
                return true;
            }
            catch(Exception e){error=LastError=e.Message;return false;}
        }
        public bool PlaySourcePoseNode(int sourceNode,string executionKey,double resumeSeconds,string sourceArchive,out string error)
        {
            error=LastError=null;if(key==executionKey&&clock!=null)return true;
            try
            {
                Load();EnsureRig();var values=new NativeStateGraphParameters();
                if(!new NativeAnimationStateGraphEvaluator(sourceGraph).TrySelectNode(sourceNode,values,out var selection))
                    throw new OriginalBindingException("Original pose node unsupported: "+sourceNode+"; "+string.Join("; ",selection.issues.Select(i=>i.code+":"+i.detail)));
                var selected=selection.clips.Select(c=>Bank(c.animation,sourceArchive)).Distinct().ToArray();
                var options=new NativeStateGraphRealtimeOptions{sourceFramesPerSecond=60,
                    clockProvenance="Host visual projection of one explicit authored source pose leaf, not native selector execution",
                    initialClock=new NativeStateGraphSampleClock{elapsedFrames=resumeSeconds*60}};
                if(!NativeAnimationStateGraphPlayback.TryCreateRealtimeSession(sourceGraph,selection,values,selected,options,out var session,out var samples,out var plan,out var issue))throw new OriginalBindingException("Source pose "+sourceNode+": "+issue);
                if(!sampler.TryPrepareBlend(samples,out var blend,out issue))throw new OriginalBindingException("Source pose "+sourceNode+": "+issue);
                Stop();prepared=blend;clock=session;sampleFrames=new float[session.SampleCount];key=executionKey;Command="SourcePoseNode/"+sourceNode;
                animatorWasEnabled=targetAnimator.enabled;savedHipLocal=targetAnimator.GetBoneTransform(HumanBodyBones.Hips).localPosition;targetAnimator.enabled=false;return true;
            }
            catch(Exception e){error=LastError=e.Message;return false;}
        }
        // Copied R ResidentWorldBehaviour.PlayPhotoPose/SetAnimation mapping.
        // Direct named clips are preview-bank samples, not invented AS commands.
        public bool PlayPhotoSourcePose(int pose,string executionKey,out string error)
        {
            error=LastError=null;
            try
            {
                if(string.IsNullOrEmpty(executionKey))throw new OriginalBindingException("Owned photo preview key is required");
                var runtime=FriendsLifeRuntime.Instance;
                if(runtime&&runtime.Ready&&runtime.State.people.Any(p=>{var body=runtime.TransformOf(p.personId);return body&&(body==transform||transform.IsChildOf(body));}))
                    throw new OriginalBindingException("Photo pose requires an owned preview clone; actual world actors cannot be overridden");
                Load();EnsureRig();PreparePhotoBindAxes();
                if(!photoPreviewBank)
                {
                    var text=Resources.Load<TextAsset>("FriendsLife/C004Native/Photo/ResidentPreviewSubset");
                    if(!text)throw new OriginalBindingException("Exact R photo preview bank resource is missing: FriendsLife/C004Native/Photo/ResidentPreviewSubset");
                    var data=JsonUtility.FromJson<NativeAnimationBank>(text.text);
                    if(data==null||data.source!="Model/AnimationProduct.bfres"||data.clips==null)throw new OriginalBindingException("Original R photo preview bank identity/schema is unavailable");
                    photoPreviewBank=ScriptableObject.CreateInstance<NativeAnimationAsset>();photoPreviewBank.name="Original R photo preview subset";photoPreviewBank.data=data;
                }
                string clip=pose==1?"WaitCool":pose==2?"SitChair":pose==3?"AngryWait":pose==4?"DroopSitChair":"Wait";
                NativeAnimationBlendSample[] samples;float[] frames;
                if(pose>=1&&pose<=4)
                {
                    if(photoPreviewBank.data.clips.Count(c=>c.name==clip)!=1)throw new OriginalBindingException("Exact original R photo clip is absent or ambiguous: "+clip);
                    samples=new[]{new NativeAnimationBlendSample{bank=photoPreviewBank,clipName=clip,frame=0,weight=1}};frames=new[]{0f};
                }
                else
                {
                    if(photoUiGraph==null)
                    {
                        var text=Resources.Load<TextAsset>("FriendsLife/C004Native/Photo/UIMii.root.asb");if(!text)throw new OriginalBindingException("Exact original UIMii ordinary Wait graph resource is missing");
                        photoUiGraph=JsonUtility.FromJson<NativeAnimationStateGraph>(text.text);
                        if(photoUiGraph==null||photoUiGraph.schema!=2||photoUiGraph.source!="AS/UIMii.root.asb")throw new OriginalBindingException("Original UIMii graph identity differs");
                    }
                    // Verbatim input values of the reference helper; that thin
                    // helper class is not included in the host's frozen DLL.
                    var parameters=new NativeStateGraphParameters();parameters.SetInt("HabitStandType",0);parameters.SetInt("StayType",0);parameters.SetInt("CharacterType",1);
                    if(!new NativeAnimationStateGraphEvaluator(photoUiGraph).TrySelectCommand("EditHabit_Stand_Lp",parameters,out var selection))throw new OriginalBindingException("Original R ordinary Wait selection is unavailable");
                    const string provenance="Copied R PlayPhotoPose idle -> TryPlayOrdinaryWaitFresh(1): UIMii EditHabit_Stand_Lp, initialFrame82=0; static preview only";
                    var initial=new NativeStateGraphSampleClock();initial.SetInitialFrame(82,0,provenance);
                    var options=new NativeStateGraphRealtimeOptions{sourceFramesPerSecond=60,clockProvenance=provenance,initialClock=initial};
                    if(!NativeAnimationStateGraphPlayback.TryCreateRealtimeSession(photoUiGraph,selection,parameters,new[]{photoPreviewBank},options,out var session,out samples,out var plan,out var issue))throw new OriginalBindingException("Original R fresh Wait preview: "+issue);
                    frames=new float[session.SampleCount];if(!session.TryCopyFrames(frames,out issue))throw new OriginalBindingException("Original R fresh Wait frame0: "+issue);
                }
                if(!sampler.TryPrepareBlend(samples,out var blend,out var failure))throw new OriginalBindingException("Original R photo sample: "+failure);
                Stop();savedPhotoRootProjection=ProjectRootPose;ProjectRootPose=true;photoMode=true;manualMode=true;manualNodes=null;
                prepared=blend;sampleFrames=frames;key=executionKey;Command="RPhotoPose/"+pose+"/"+clip;
                animatorWasEnabled=targetAnimator.enabled;savedHipLocal=targetAnimator.GetBoneTransform(HumanBodyBones.Hips).localPosition;targetAnimator.enabled=false;
                if(!SampleOnce(out failure)){Stop();throw new OriginalBindingException("Original R photo frame0 projection: "+failure);}return true;
            }
            catch(Exception errorValue){error=LastError=errorValue.Message;return false;}
        }
        public bool SampleOnce(out string error)
        {
            error=null;if(!IsPlaying||sampler==null||prepared==null||sampleFrames==null){error="No prepared original source sample is owned";return false;}
            if(!sampler.TrySamplePreparedBlend(prepared,sampleFrames,out error)){LastError=error;return false;}
            ProjectPreparedBones();return true;
        }
        // Source actor AS owns selection/timing/RNG. This port only samples
        // its explicit body leaf frames through the existing host bone map.
        public bool PlayManualSourcePoseNode(int sourceNode,string executionKey,float rawHeight,float rawBuild,string sourceArchive,out string error)
        {
            error=LastError=null;if(key==executionKey&&manualMode)return true;
            try
            {
                Load();EnsureRig();var values=NativeDateAsRuntime.Parameters(rawHeight,rawBuild);
                if(!new NativeAnimationStateGraphEvaluator(sourceGraph).TrySelectNode(sourceNode,values,out var selection))throw new OriginalBindingException("Unsupported manual Date source pose: "+sourceNode);
                var selected=selection.clips.Select(c=>Bank(c.animation,sourceArchive)).Distinct().ToArray();
                if(!NativeAnimationStateGraphPlayback.TryCreateSamples(selection,selected,0,out var samples,out var issue)||!sampler.TryPrepareBlend(samples,out var blend,out issue))throw new OriginalBindingException("Source Date pose "+sourceNode+": "+issue);
                Stop();prepared=blend;sampleFrames=new float[samples.Length];manualNodes=selection.clips.Select(c=>c.node).ToArray();manualMode=true;key=executionKey;Command="NativeDateAS/"+sourceNode;
                animatorWasEnabled=targetAnimator.enabled;savedHipLocal=targetAnimator.GetBoneTransform(HumanBodyBones.Hips).localPosition;targetAnimator.enabled=false;return true;
            }
            catch(Exception e){error=LastError=e.Message;return false;}
        }
        public bool SetManualSourceFrames(NativeDateAsState state,out string error)
        {
            error=null;if(!manualMode||manualNodes==null||state?.body==null){error="No manual Date source frame owner";return false;}
            for(int i=0;i<manualNodes.Length;i++)
            {
                var body=state.body.FirstOrDefault(b=>b.sourceNode==manualNodes[i]);
                if(body==null){error="Saved Date AS lost a selected source body leaf: "+manualNodes[i];return false;}
                sampleFrames[i]=body.frame.Frame;
            }
            return true;
        }
        void EnsureRig()
        {
            if(rig)return;
            targetAnimator=GetComponentsInChildren<Animator>(true).FirstOrDefault(a=>a.avatar&&a.avatar.isHuman);
            if(!targetAnimator)throw new OriginalBindingException("Host actor has no humanoid Avatar for source motion projection");
            var go=new GameObject("Source Motion Skeleton");go.transform.SetParent(transform,false);rig=go.AddComponent<NativeRig>();
            rig.source=ScriptableObject.CreateInstance<NativeModelAsset>();rig.source.data=skeleton;
            rig.bones=new NativeBonePose[skeleton.bones.Length];
            for(int i=0;i<skeleton.bones.Length;i++)
            {
                var data=skeleton.bones[i];var bone=new GameObject(data.name).AddComponent<NativeBonePose>();bone.sourceName=data.name;
                bone.transform.SetParent(data.parent>=0?rig.bones[data.parent].transform:go.transform,false);bone.nativeParent=data.parent>=0?rig.bones[data.parent]:null;rig.bones[i]=bone;
            }
            rig.ResetToBindPose();sampler=go.AddComponent<NativeAnimationPlayer>();sampler.rig=rig;sampler.playing=false;
            foreach(var pair in boneNames)
            {
                var src=rig.bones.FirstOrDefault(b=>b.sourceName==pair.Key);var dst=targetAnimator.GetBoneTransform(pair.Value);if(!src||!dst)continue;
                int depth=0;for(var p=dst;p&&p!=transform;p=p.parent)depth++;
                mapping.Add(new BoneMap{source=src.transform,target=dst,sourceBind=Quaternion.Inverse(transform.rotation)*src.transform.rotation,targetBind=HostBindRotation(dst),depth=depth});
            }
            mapping.Sort((a,b)=>a.depth.CompareTo(b.depth));
            if(mapping.Count<15)throw new OriginalBindingException("Host/source humanoid bone mapping is incomplete");
            var sourceHip=rig.bones.First(b=>b.sourceName=="Skl_Root").transform;
            var sourceHead=rig.bones.First(b=>b.sourceName=="Head").transform;
            var targetHip=targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            var targetHead=targetAnimator.GetBoneTransform(HumanBodyBones.Head);
            sourceHipBind=transform.InverseTransformPoint(sourceHip.position);
            targetHipBind=transform.InverseTransformPoint(targetHip.position);
            float sourceHeight=Vector3.Distance(sourceHip.position,sourceHead.position);
            if(sourceHeight>.00001f)rootScale=Vector3.Distance(targetHip.position,targetHead.position)/sourceHeight;
        }
        Quaternion HostBindRotation(Transform bone)
        {
            foreach(var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!renderer.sharedMesh)continue;int index=Array.IndexOf(renderer.bones,bone);var bind=renderer.sharedMesh.bindposes;
                if(index>=0&&index<bind.Length)return Quaternion.Inverse(transform.rotation)*(renderer.transform.localToWorldMatrix*bind[index].inverse).rotation;
            }
            return Quaternion.Inverse(transform.rotation)*bone.rotation;
        }
        // The supplied pose2 capture showed a correct active BodyRig and
        // mesh bind, but ~54 degree source-T/host-A arm direction error. Use
        // actual source bone segments to correct only the missing swing basis;
        // no authored native rotation/curve/sample is modified.
        void PreparePhotoBindAxes()
        {
            var next=new Dictionary<string,string>(StringComparer.Ordinal)
            {
                {"Skl_Root","Spine_1"},{"Spine_1","Spine_2"},{"Spine_2","Neck"},{"Neck","Head"},
                {"Clavicle_L","Arm_1_L"},{"Arm_1_L","Arm_2_L"},{"Arm_2_L","Wrist_L"},
                {"Clavicle_R","Arm_1_R"},{"Arm_1_R","Arm_2_R"},{"Arm_2_R","Wrist_R"},
                {"Leg_1_L","Leg_2_L"},{"Leg_2_L","Ankle_L"},{"Ankle_L","Toe_L"},
                {"Leg_1_R","Leg_2_R"},{"Leg_2_R","Ankle_R"},{"Ankle_R","Toe_R"}
            };
            var renderers=GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(x=>x.enabled&&x.gameObject.activeInHierarchy&&!x.forceRenderingOff&&x.sharedMesh&&x.sharedMesh.vertexCount>0).OrderByDescending(x=>x.sharedMesh.vertexCount).ToArray();
            foreach(var bone in mapping)
            {
                bone.photoAxisReady=false;bone.photoAxisChild=null;
                if(!next.TryGetValue(bone.source.name,out string name))continue;
                var child=mapping.FirstOrDefault(x=>x.source.name==name);if(child==null)continue;
                foreach(var renderer in renderers)
                {
                    var indices=renderer.bones;var binds=renderer.sharedMesh.bindposes;
                    int parentIndex=Array.IndexOf(indices,bone.target),childIndex=Array.IndexOf(indices,child.target);
                    if(parentIndex<0||childIndex<0||parentIndex>=binds.Length||childIndex>=binds.Length)continue;
                    // Parent bindpose * inverse child bindpose gives the true
                    // child origin in the parent's bind coordinates. This also
                    // avoids subtracting world positions at preview y=-10000.
                    Vector3 axis=(binds[parentIndex]*binds[childIndex].inverse).MultiplyPoint3x4(Vector3.zero);
                    if(axis.sqrMagnitude<=.00000001f)continue;
                    bone.photoBindAxis=axis;bone.photoAxisChild=child.source;bone.photoAxisReady=true;break;
                }
            }
            foreach(string required in new[]{"Arm_1_L","Arm_2_L","Arm_1_R","Arm_2_R"})
                if(!mapping.Any(x=>x.source.name==required&&x.photoAxisReady))throw new OriginalBindingException("Actual owned preview arm bind-axis is absent: "+required);
        }
        static Vector3 SourceBoneSegment(Transform bone,Transform child)
        {
            Matrix4x4 relative=Matrix4x4.identity;var current=child;
            while(current&&current!=bone)
            {relative=Matrix4x4.TRS(current.localPosition,current.localRotation,current.localScale)*relative;current=current.parent;}
            if(current!=bone)throw new OriginalBindingException("Original source segment is not a descendant bone");
            return bone.TransformVector(relative.MultiplyPoint3x4(Vector3.zero));
        }
        void LateUpdate()
        {
            if(!IsPlaying||Time.timeScale<=0)return;
            string error=null;
            if(!manualMode&&(!clock.TryAdvanceSeconds(Time.deltaTime,out error)||!clock.TryCopyFrames(sampleFrames,out error))||!sampler.TrySamplePreparedBlend(prepared,sampleFrames,out error)){LastError=error;Stop();return;}
            ProjectPreparedBones();
        }
        void ProjectPreparedBones()
        {
            foreach(var bone in mapping)
            {
                var current=Quaternion.Inverse(transform.rotation)*bone.source.rotation;
                var rotation=transform.rotation*(current*Quaternion.Inverse(bone.sourceBind))*bone.targetBind;
                if(photoMode&&bone.photoAxisReady)
                {
                    var desired=SourceBoneSegment(bone.source,bone.photoAxisChild);var projected=rotation*bone.photoBindAxis;
                    if(desired.sqrMagnitude>.00000001f&&projected.sqrMagnitude>.00000001f)
                        rotation=Quaternion.FromToRotation(projected,desired)*rotation;
                }
                bone.target.rotation=rotation;
            }
            if(ProjectRootPose)
            {
                var root=mapping.FirstOrDefault(b=>b.source.name=="Skl_Root");
                if(root!=null)root.target.position=transform.TransformPoint(targetHipBind+(transform.InverseTransformPoint(root.source.position)-sourceHipBind)*rootScale);
            }
        }
        public void Stop()
        {
            if(targetAnimator&&IsPlaying&&ProjectRootPose)targetAnimator.GetBoneTransform(HumanBodyBones.Hips).localPosition=savedHipLocal;
            if(targetAnimator&&IsPlaying)targetAnimator.enabled=animatorWasEnabled;
            clock=null;manualMode=false;manualNodes=null;prepared=null;key=null;Command=null;
            if(photoMode){ProjectRootPose=savedPhotoRootProjection;photoMode=false;}
        }
        void OnDisable()=>Stop();
        void OnDestroy(){Stop();if(rig&&rig.source)Destroy(rig.source);}
    }
}
