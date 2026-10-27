using System;
using UnityEngine;

namespace HowToFish {
    [Serializable] public sealed class FishWeaponAudioStep{public float delay,volume=1;public AudioClip[] clips;}
    public sealed class FishWeapon:MonoBehaviour {
        public FishingItem item;
        public FishCombat combat;
        public Transform muzzle;
        public Animation animationPlayer;
        public string title;
        public int damage=25,magazine=10,pellets=1;
        public float interval=.15f,projectileSpeed=350,spread,force=250;
        public bool fullAuto,blockDuringFire;
        public AudioClip[] fireSounds;
        public FishWeaponAudioStep[] reloadAudio;
        public int Ammo{get;private set;}
        public bool Reloading{get;private set;}
        public bool Aiming{get;private set;}
        float readyAt,reloadAt,reloadDuration,reloadAudioAt;
        int reloadStep;
        bool wasHeld,xrHeld,xrPressed;
        AudioSource sound;
        TextMesh readout;
        bool Has(string name)=>animationPlayer&&animationPlayer[name];
        public void ResetForStorage(){if(animationPlayer)animationPlayer.Stop();Aiming=Reloading=false;xrHeld=xrPressed=wasHeld=false;}
        void Awake(){Ammo=magazine;sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.volume=.3f;sound.spatialBlend=0;}
        void LateUpdate(){
            bool held=item.owner;
            if(held&&!wasHeld){Play("Idle",true);MakeReadout();}wasHeld=held;
            if(readout){readout.gameObject.SetActive(held&&item.owner.tracked);if(held)readout.text=title+"  "+Ammo+" / "+magazine+(Reloading?"\n换弹中":"\n扳机开火 · 主键换弹");}
            if(!held){Aiming=false;xrHeld=xrPressed=false;if(animationPlayer)animationPlayer.Stop();if(Reloading){Reloading=false;Ammo=Mathf.Clamp(Ammo,0,magazine);}return;}
            if(combat.fishing.vitals.Down)return;
            if(!item.owner.tracked&&!item.ExternalGripPose){var target=Aiming?new Vector3(-item.owner.transform.localPosition.x,.15f,-.1f):Vector3.zero;transform.localPosition=Vector3.Lerp(transform.localPosition,target,1-Mathf.Exp(-Time.deltaTime*14));}
            bool press=false,hold=false;
            if(item.owner.tracked){press=xrPressed;hold=xrHeld;xrPressed=false;}
            else {press=xrPressed;hold=xrHeld;xrPressed=false;}
            if(Reloading){
                if(reloadAudio!=null&&reloadStep<reloadAudio.Length&&Time.time>=reloadAudioAt){PlaySound(reloadAudio[reloadStep].clips,reloadAudio[reloadStep].volume);reloadStep++;if(reloadStep<reloadAudio.Length)reloadAudioAt+=reloadAudio[reloadStep].delay;}
                if(Time.time>=reloadAt-reloadDuration*.1f)Ammo=magazine;
                if(Time.time>=reloadAt){Reloading=false;Play("Idle",true);}return;
            }
            if(Ammo==0&&!IsFiring()&&Time.time>=readyAt){Reload();return;}
            if((press||fullAuto&&hold)&&Time.time>=readyAt&&(!blockDuringFire||!IsFiring()))Fire();
            if(!Reloading&&!IsFiring()&&Has("Idle")&&!animationPlayer.IsPlaying("Idle"))Play("Idle",true);
        }
        bool IsFiring()=>animationPlayer&&(animationPlayer.IsPlaying("Fire")||animationPlayer.IsPlaying("FireLast"));
        public void SetAim(bool value){Aiming=value;}
        public void Input(bool trigger,bool previous,bool reload){xrHeld=trigger;xrPressed|=trigger&&!previous;if(reload)Reload();}
        public void Reload(){if(Reloading||Ammo==magazine||!item.owner)return;Reloading=true;string clip=Ammo==0&&Has("ReloadLast")?"ReloadLast":"Reload";reloadDuration=Has(clip)?animationPlayer[clip].length:1.2f;reloadAt=Time.time+reloadDuration;reloadStep=0;reloadAudioAt=Time.time+(reloadAudio!=null&&reloadAudio.Length>0?reloadAudio[0].delay:0);Play(clip,false);combat.rig.Notify(title+"换弹中");}
        void Fire(){
            if(Ammo<=0){Reload();return;}
            Ammo--;readyAt=Time.time+interval;Play(Ammo==0&&Has("FireLast")?"FireLast":"Fire",false);PlaySound(fireSounds,1);item.owner.Pulse(.5f,.045f);
            Vector3 origin=combat.ProjectileOrigin!=null?combat.ProjectileOrigin(muzzle.position):muzzle.position;
            Vector3 direction=muzzle.forward;
            if(!item.owner.tracked){var camera=combat.rig.view;Vector3 aim=camera.transform.position+camera.transform.forward*100;foreach(var hit in Physics.RaycastAll(camera.transform.position,camera.transform.forward,100,~0,QueryTriggerInteraction.Ignore)){if(hit.collider.GetComponentInParent<FishingItem>()?.owner||hit.collider is CharacterController||combat.IgnoreCollider!=null&&combat.IgnoreCollider(hit.collider))continue;if(Vector3.Distance(camera.transform.position,hit.point)<Vector3.Distance(camera.transform.position,aim))aim=hit.point;}direction=(aim-origin).normalized;}
            if(combat.effects)combat.effects.Impact(origin,direction,FishingShotEffects.HitKind.Muzzle);
            for(int i=0;i<pellets;i++){var shot=(Quaternion.AngleAxis(UnityEngine.Random.Range(-spread,spread),Vector3.up)*Quaternion.AngleAxis(UnityEngine.Random.Range(-spread,spread),Vector3.right)*direction).normalized;combat.AddProjectile(this,origin,shot*projectileSpeed);}
            if(Ammo==0)readyAt=Mathf.Max(readyAt,Time.time+.1f);
        }
        void Play(string clip,bool loop){if(!Has(clip))return;animationPlayer[clip].wrapMode=loop?WrapMode.Loop:WrapMode.Once;animationPlayer.Play(clip);}
        void PlaySound(AudioClip[] clips,float volume){if(sound&&clips!=null&&clips.Length>0)sound.PlayOneShot(clips[UnityEngine.Random.Range(0,clips.Length)],volume);}
        void MakeReadout(){if(readout||!combat.rig.interfaceFont)return;var go=new GameObject("VR weapon ammo readout");go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(0,.13f,.10f);go.transform.localRotation=Quaternion.Euler(25,180,0);readout=go.AddComponent<TextMesh>();readout.font=combat.rig.interfaceFont;readout.fontSize=48;readout.characterSize=.012f;readout.anchor=TextAnchor.MiddleCenter;readout.alignment=TextAlignment.Center;readout.color=new Color(.39f,.87f,.73f);go.GetComponent<MeshRenderer>().sharedMaterial=combat.rig.interfaceFont.material;}
    }
}
