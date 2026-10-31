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
        public bool Pending {get;private set;}
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
        bool originalApplyRootMotion,exiting;
        int requestFrame;
        AnimatorCullingMode originalCulling;
        Camera worldCamera;
        Vector3 initialCameraPosition;
        Quaternion initialCameraRotation;
        float initialFieldOfView,exitAt=-1,exitPoseTime;
        HousingRestIK ik;
        readonly HousingInputGate gate=new HousingInputGate();
        const float HandDuration=1.4f,TuckDuration=1f,PivotDuration=1.1f,ReclineDuration=1.7f;
        // Beds no longer play the stitched sit->brace->tuck->pivot->recline
        // sequence: the final lying pose is applied at once (third person) and
        // the bed hands the night to the original Bed sleep authority.
        bool InstantLie=>rest&&rest.kind==HousingRestKind.Bed;
        bool IsToilet=>rest&&rest.kind==HousingRestKind.Toilet;
        // Toilet: shorter camera lead (lid opens while walking in) and a shorter,
        // smoother sit; the seat/chair timing is unchanged.
        float CameraLead=>IsToilet?.35f:.65f;
        float SitDuration=>IsToilet?1.25f:1.8f;
        float EnterDuration=>CameraLead+SitDuration+(rest.kind==HousingRestKind.Bed?HandDuration+TuckDuration+PivotDuration+ReclineDuration:0);
        bool sleepRequested;
        public void Begin(PPG_Player actor,HousingRestInteractable target)
        {
            if(Active||Pending||!actor||!target||!target.area||target.area.IsOccupied)return;
            var pose=actor.TPS_Character?actor.TPS_Character.GetComponent<Animator>():null;
            var catalog=HousingIsland.Instance?HousingIsland.Instance.Catalog:null;
            var camera=typeof(PPG_Player).GetField("_playerCamera",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(actor)as Camera;
            if(!pose||!pose.isHuman||!pose.avatar||!catalog||!catalog.restIdle||!catalog.restSit||!catalog.restLay||
                !catalog.restTuck||!catalog.restBrace||!catalog.restRecline||!camera||!target.exit)return;
            if(!target.area.TryRequestCustomUse(actor.Interactor))return;
            player=actor;rest=target;Pending=true;requestFrame=Time.frameCount;Phase="AwaitingGrant";
            if(!actor.GetComponent<HousingEscapeGuard>())actor.gameObject.AddComponent<HousingEscapeGuard>();
        }
        bool BeginGranted()
        {
            if(!player||!rest||!rest.area||!rest.area.HasCustomUseGrant(player))return false;
            animator=player.TPS_Character?player.TPS_Character.GetComponent<Animator>():null;
            var catalog=HousingIsland.Instance?HousingIsland.Instance.Catalog:null;
            worldCamera=(Camera)typeof(PPG_Player).GetField("_playerCamera",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(player);
            if(!animator||!animator.isHuman||!animator.avatar||!catalog||!catalog.restIdle||!catalog.restSit||!catalog.restLay||
                !catalog.restTuck||!catalog.restBrace||!catalog.restRecline||!worldCamera||!rest.exit)return false;
            hip=animator.GetBoneTransform(HumanBodyBones.Hips);head=animator.GetBoneTransform(HumanBodyBones.Head);
            var leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if(!hip||!head||!leftFoot||!rightFoot)return false;
            startHip=hip.position;StartFeet=new[]{leftFoot.position,rightFoot.position};
            originalVisualPosition=animator.transform.localPosition;originalVisualRotation=animator.transform.localRotation;
            originalWorldRotation=animator.transform.rotation;originalApplyRootMotion=animator.applyRootMotion;originalCulling=animator.cullingMode;
            initialCameraPosition=worldCamera.transform.position;initialCameraRotation=worldCamera.transform.rotation;initialFieldOfView=worldCamera.fieldOfView;
            // The native owner/epoch and live range are checked again here.
            // A rejected or cancelled claim never reaches preparation or posing.
            if(!rest.area.TryStartCustomUse(player,StaticEmotes.Sit,rest.exit,enableCameraToggle:false))return false;
            Pending=false;Active=true;Phase="Starting";
            try
            {
                if(!player.GetComponent<HousingRestCamera>())player.gameObject.AddComponent<HousingRestCamera>();
                animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                graph=PlayableGraph.Create("Residential furniture Humanoid");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                mixer=AnimationMixerPlayable.Create(graph,6);
                var assets=new[]{catalog.restIdle,catalog.restSit,catalog.restLay,catalog.restTuck,catalog.restBrace,catalog.restRecline};clips=new AnimationClipPlayable[6];
                for(int i=0;i<6;i++){clips[i]=AnimationClipPlayable.Create(graph,assets[i]);clips[i].SetApplyFootIK(false);clips[i].SetApplyPlayableIK(true);clips[i].SetSpeed(0);graph.Connect(clips[i],0,mixer,i);}
                AnimationPlayableOutput.Create(graph,"Furniture",animator).SetSourcePlayable(mixer);
                NativeSitClips=catalog.restSit.name;NativeLayClips=catalog.restLay.name;
                ik=animator.gameObject.GetComponent<HousingRestIK>();if(!ik)ik=animator.gameObject.AddComponent<HousingRestIK>();ik.session=this;
                Elapsed=0;exitAt=-1;exiting=false;Active=true;Phase=InstantLie?"Sleep":"Camera";gate.Acquire();graph.Play();
                sleepRequested=false;
                if(InstantLie)
                {
                    var bed=global::Bed.Active;
                    if(bed){bed.RequestSleep();sleepRequested=true;}
                    else Debug.LogWarning("[HousingRestSession] No spawned original Bed sleep authority in the game scene; lying only.");
                }
                return true;
            }
            catch(System.Exception exception)
            {Debug.LogWarning("[HousingRestSession] Presentation failed after grant: "+exception);Finish(true);return false;}
        }
        void CancelPending()
        {
            Pending=false;Phase="Idle";
            if(player&&rest&&rest.area)rest.area.CancelCustomUse(player);
        }
        public void RequestExit()
        {
            if(Pending){CancelPending();return;}
            if(!Active||exiting)return;
            if(sleepRequested){sleepRequested=false;global::Bed.Active?.CancelSleep();}
            exiting=true;exitAt=Elapsed;exitPoseTime=Mathf.Min(Elapsed,EnterDuration);
        }
        void Update()
        {
            if(Pending)
            {
                if(!player||!rest||!rest.area){CancelPending();return;}
                // The original request is already the initiating E release.
                if(Time.frameCount>requestFrame&&Keyboard.current?.eKey.wasReleasedThisFrame==true){CancelPending();return;}
                if(rest.area.HasCustomUseGrant(player)){if(!BeginGranted())CancelPending();}
                else if(!rest.area.HasCustomUseRequest(player)){Pending=false;Phase="Idle";}
                return;
            }
            if(!Active)return;
            if(!player||!rest||!rest.area||!rest.area.HasCustomUseStarted(player)){Finish(false);return;}
            Elapsed+=Time.unscaledDeltaTime;
            if(Elapsed>(InstantLie?.35f:EnterDuration+.25f)&&Keyboard.current?.eKey.wasReleasedThisFrame==true)RequestExit();
        }
        public void BeforePlayerUpdate()
        {
            if((Active||Pending)&&player&&Keyboard.current?.escapeKey.wasPressedThisFrame==true)RequestExit();
        }
        static float Smooth(float value)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(value));
        static float Smoother(float value){value=Mathf.Clamp01(value);return value*value*value*(value*(value*6-15)+10);}
        void LateUpdate()
        {
            if(!Active||!graph.IsValid()||!animator)return;
            if(!player||!rest||!rest.area||!rest.area.HasCustomUseStarted(player)){Finish(false);return;}
            if(exiting&&InstantLie){Finish(true);return;} // get straight up, like waking in MC
            float t=InstantLie?EnterDuration:Elapsed;
            if(exiting)
            {
                float duration=(IsToilet?1.3f:2f)*Mathf.Clamp(exitPoseTime/EnterDuration,.2f,1);
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
            if(IsToilet)sit=Smoother((t-CameraLead)/SitDuration);
            if(rest.toilet)rest.toilet.SetOpen(Smooth(t/(CameraLead+.3f)));
            var desiredHip=Vector3.Lerp(Vector3.Lerp(startHip,rest.seatHip.position,sit),rest.sleepHip.position,pivot);
            desiredHip.y+=IsToilet?.03f*Mathf.Sin(Mathf.PI*sit):0;
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
            if(sleepRequested){sleepRequested=false;global::Bed.Active?.CancelSleep();}
            bool mayRestoreExit=player&&rest&&(player.CurrentEmoteArea==rest.area||!player.CurrentEmoteArea&&!player.IsUsingEmoteArea);
            if(nativeExit&&player&&rest&&player.CurrentEmoteArea==rest.area)player.EndStaticEmote();
            if(player&&rest&&rest.area)rest.area.CancelCustomUse(player);
            if(mayRestoreExit&&rest.exit)player.TeleportPlayer(rest.exit.position,rest.exit.rotation,updateGround:true);
            gate.Release(true);Phase="Idle";
        }
        void OnDisable(){if(Pending)CancelPending();if(Active)Finish(true);}
    }
}
