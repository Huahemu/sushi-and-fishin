using System.Linq;
using System.Reflection;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Enums;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.InputSystem;

namespace SushiAndFish.ResidentialIsland
{
    // A single manually evaluated Humanoid graph owns the pose. Furniture alignment
    // translates the entire avatar; it never moves, scales or stretches individual bones.
    [DefaultExecutionOrder(31000)]
    public sealed class HousingRestSession : MonoBehaviour
    {
        public bool Active {get;private set;}
        public string Phase {get;private set;}
        public string NativeSitClips {get;private set;}
        public string NativeLayClips {get;private set;}
        public float Elapsed {get;private set;}
        public float BedSupportLift=>0;
        public float PoseTime {get;private set;}
        public Animator PoseAnimator=>animator;
        public HousingRestInteractable Target=>rest;
        public Vector3[] StartFeet {get;private set;}
        PPG_Player player;
        HousingRestInteractable rest;
        Animator animator;
        Transform hip,head;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] clips;
        Vector3 startHip,originalVisualPosition;
        Quaternion originalVisualRotation,originalWorldRotation;
        bool originalApplyRootMotion,restoreArea,exiting;
        AnimatorCullingMode originalCulling;
        Camera worldCamera;
        Vector3 initialCameraPosition;
        Quaternion initialCameraRotation;
        float initialFieldOfView,exitAt=-1,exitPoseTime;
        HousingRestIK ik;
        readonly HousingInputGate gate=new HousingInputGate();
        const float CameraLead=.65f,SitDuration=1.8f,HandDuration=1.4f,TuckDuration=1f,PivotDuration=1.1f,ReclineDuration=1.7f;
        float EnterDuration=>CameraLead+SitDuration+(rest.kind==HousingRestKind.Bed?HandDuration+TuckDuration+PivotDuration+ReclineDuration:0);
        public void Begin(PPG_Player actor,HousingRestInteractable target)
        {
            if(Active||!actor||!target.area||target.area.IsOccupied||!actor.TryPrepareForEmoteArea())return;
            player=actor;rest=target;animator=actor.TPS_Character.GetComponent<Animator>();
            var catalog=HousingIsland.Instance.Catalog;
            if(!animator||!animator.isHuman||!animator.avatar||!catalog.restSit||!catalog.restLay)return;
            if(!actor.GetComponent<HousingEscapeGuard>())actor.gameObject.AddComponent<HousingEscapeGuard>();
            if(!actor.GetComponent<HousingRestCamera>())actor.gameObject.AddComponent<HousingRestCamera>();
            hip=animator.GetBoneTransform(HumanBodyBones.Hips);head=animator.GetBoneTransform(HumanBodyBones.Head);startHip=hip.position;StartFeet=new[]{animator.GetBoneTransform(HumanBodyBones.LeftFoot).position,animator.GetBoneTransform(HumanBodyBones.RightFoot).position};
            originalVisualPosition=animator.transform.localPosition;originalVisualRotation=animator.transform.localRotation;
            originalWorldRotation=animator.transform.rotation;originalApplyRootMotion=animator.applyRootMotion;originalCulling=animator.cullingMode;
            animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            worldCamera=(Camera)typeof(PPG_Player).GetField("_playerCamera",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(actor);
            initialCameraPosition=worldCamera.transform.position;initialCameraRotation=worldCamera.transform.rotation;initialFieldOfView=worldCamera.fieldOfView;
            player.CurrentEmoteArea=target.area;target.area.ServerChangeSomeoneUsingRpc(true);
            player.StartStaticEmote(StaticEmotes.Sit,target.exit,enableCameraToggle:false);
            graph=PlayableGraph.Create("Residential furniture Humanoid");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer=AnimationMixerPlayable.Create(graph,6);
            var assets=new[]{catalog.restIdle,catalog.restSit,catalog.restLay,catalog.restTuck,catalog.restBrace,catalog.restRecline};clips=new AnimationClipPlayable[6];
            for(int i=0;i<6;i++){clips[i]=AnimationClipPlayable.Create(graph,assets[i]);clips[i].SetApplyFootIK(false);clips[i].SetApplyPlayableIK(true);clips[i].SetSpeed(0);graph.Connect(clips[i],0,mixer,i);}
            AnimationPlayableOutput.Create(graph,"Furniture",animator).SetSourcePlayable(mixer);
            NativeSitClips=catalog.restSit.name;NativeLayClips=catalog.restLay.name;
            ik=animator.gameObject.GetComponent<HousingRestIK>();if(!ik)ik=animator.gameObject.AddComponent<HousingRestIK>();ik.session=this;
            Elapsed=0;exitAt=-1;exiting=false;Active=true;Phase="Camera";gate.Acquire();graph.Play();
        }
        public void RequestExit(){if(Active&&!exiting){exiting=true;exitAt=Elapsed;exitPoseTime=Mathf.Min(Elapsed,EnterDuration);}}
        void Update()
        {
            if(!Active)return;
            if(!player||!rest||(!restoreArea&&!player.IsUsingEmoteArea)){Finish(false);return;}
            Elapsed+=Time.unscaledDeltaTime;
            if(Elapsed>EnterDuration+.25f&&Keyboard.current?.eKey.wasReleasedThisFrame==true)RequestExit();
        }
        public void BeforePlayerUpdate()
        {
            if(Active&&player&&Keyboard.current?.escapeKey.wasPressedThisFrame==true)
            {player.CurrentEmoteArea=null;restoreArea=true;RequestExit();}
        }
        static float Smooth(float value)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(value));
        void LateUpdate()
        {
            if(!Active||!graph.IsValid()||!animator)return;
            if(restoreArea){player.CurrentEmoteArea=rest.area;restoreArea=false;}
            float t=Elapsed;
            if(exiting)
            {
                float duration=(rest.kind==HousingRestKind.Bed?6.5f:2f)*Mathf.Clamp(exitPoseTime/EnterDuration,.2f,1);
                float u=Mathf.Clamp01((Elapsed-exitAt)/duration);if(u>=1){Finish(true);return;}t=exitPoseTime*(1-u);
            }
            PoseTime=t;
            float sit=Smooth((t-CameraLead)/SitDuration);
            float brace=Smooth((t-CameraLead-SitDuration)/1f);
            float lift=rest.kind==HousingRestKind.Bed?Smooth((t-CameraLead-SitDuration-HandDuration)/TuckDuration):0;
            float pivot=rest.kind==HousingRestKind.Bed?Smooth((t-CameraLead-SitDuration-HandDuration-TuckDuration)/PivotDuration):0;
            float lie=rest.kind==HousingRestKind.Bed?Smooth((t-CameraLead-SitDuration-HandDuration-TuckDuration-PivotDuration)/ReclineDuration):0;
            Phase=t<CameraLead?"Camera":sit<1?"Sit":rest.kind==HousingRestKind.Toilet?"Seated":lift<=0?"HandSupport":lift<1?"LegLift":pivot<1?"Pivot":lie<1?"Recline":"Sleep";
            if(exiting)Phase="Exit/"+Phase;
            if(rest.toilet)rest.toilet.SetOpen(Smooth(t/CameraLead));
            var desiredHip=Vector3.Lerp(Vector3.Lerp(startHip,rest.seatHip.position,sit),rest.sleepHip.position,pivot);
            desiredHip.y+=rest.kind==HousingRestKind.Toilet?.05f*Mathf.Sin(Mathf.PI*sit):0;
            player.FirstPersonCharacter.TeleportPosition(new Vector3(desiredHip.x,rest.exit.position.y,desiredHip.z));
            animator.transform.localPosition=originalVisualPosition;
            animator.transform.rotation=Quaternion.Slerp(Quaternion.Slerp(originalWorldRotation,rest.seatHip.rotation,sit),Quaternion.LookRotation(-rest.sleepHip.forward),pivot);
            float recline=Smooth(lie/.65f),extension=Smooth((lie-.65f)/.35f);
            float support=rest.kind==HousingRestKind.Bed?brace:0;
            mixer.SetInputWeight(0,1-sit);mixer.SetInputWeight(1,sit*(1-lift)*(1-support));
            mixer.SetInputWeight(4,sit*(1-lift)*support);mixer.SetInputWeight(3,sit*lift*(1-recline));
            mixer.SetInputWeight(5,sit*lift*recline*(1-extension));mixer.SetInputWeight(2,sit*lift*recline*extension);
            for(int i=0;i<6;i++)clips[i].SetTime(.02);
            // Evaluate once without IK to measure the normal retargeted hip offset.
            ik.apply=false;graph.Evaluate(0);
            animator.transform.position+=desiredHip-hip.position;
            ik.sit=sit;ik.lie=lift;ik.brace=brace*(1-pivot);ik.apply=true;
            graph.Evaluate(0);
        }
        public void ApplyThirdPersonCamera()
        {
            if(!worldCamera||!rest||!rest.cameraPoint||!head||!hip)return;
            var target=Vector3.Lerp(hip.position,head.position,.4f);var candidate=rest.cameraPoint.position;
            var delta=candidate-target;float distance=delta.magnitude;
            foreach(var hit in Physics.SphereCastAll(target,.065f,delta.normalized,distance,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {
                if(hit.collider.transform.IsChildOf(player.transform))continue;
                candidate=target+delta.normalized*Mathf.Max(.5f,hit.distance-.1f);break;
            }
            float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(Elapsed/.35f));
            var position=Vector3.Lerp(initialCameraPosition,candidate,blend);
            var rotation=Quaternion.LookRotation(target-position,Vector3.up);
            worldCamera.transform.SetPositionAndRotation(position,Quaternion.Slerp(initialCameraRotation,rotation,blend));worldCamera.fieldOfView=65;
        }
        void Finish(bool nativeExit)
        {
            if(!Active)return;Active=false;
            if(ik){ik.apply=false;ik.session=null;}
            if(graph.IsValid())graph.Destroy();
            if(rest&&rest.toilet)rest.toilet.SetOpen(0);
            if(animator){animator.transform.localPosition=originalVisualPosition;animator.transform.localRotation=originalVisualRotation;animator.applyRootMotion=originalApplyRootMotion;animator.cullingMode=originalCulling;}
            if(worldCamera)worldCamera.fieldOfView=initialFieldOfView;
            if(nativeExit&&player)player.EndStaticEmote();
            if(player&&rest&&rest.exit)player.TeleportPlayer(rest.exit.position,rest.exit.rotation,updateGround:true);
            gate.Release(true);Phase="Idle";
        }
        void OnDisable(){if(Active)Finish(true);}
    }
}
