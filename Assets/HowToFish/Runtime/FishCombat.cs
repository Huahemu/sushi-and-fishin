using System.Collections.Generic;
using UnityEngine;

namespace HowToFish {
    [DefaultExecutionOrder(-40)]
    public sealed class FishCombat:MonoBehaviour {
        public System.Func<Vector3,Vector3> ProjectileOrigin;
        public FishingModule fishing;
        public FishingRig rig;
        public System.Func<Collider,bool> IgnoreCollider;
        public bool AffectOtherBodies=true;
        public event System.Action<FishWeapon,Vector3,Vector3> ProjectileFired;
        public event System.Action<Vector3,Vector3,int> ProjectileImpact;
        public event System.Action<Vector3,Vector3> ProjectileSegment;
        public FishWeapon[] guns;
        public FishingShotEffects effects;
        public int Kills{get;private set;}
        public float HitMarker{get;private set;}
        public bool LastHitKilled{get;private set;}
        struct Bullet {public FishWeapon gun;public Vector3 p,v;public float distance;}
        readonly List<Bullet> bullets=new List<Bullet>();
        public FishWeapon HeldGun {get{if(rig.hands==null)return null;var item=rig.ActiveHand.Held;return item?item.GetComponent<FishWeapon>():null;}}
        bool initialized,started;
        public void BindRig(FishingRig actor){rig=actor;if(started)Initialize();}
        void Start(){started=true;Initialize();}
        void Initialize(){if(initialized||!rig)return;initialized=true;foreach(var gun in guns){gun.combat=this;gun.item.SetHome(transform,gun.transform.position,gun.transform.rotation);rig.Register(gun.item);}}
        void Update(){HitMarker=Mathf.MoveTowards(HitMarker,0,Time.deltaTime*5);}
        public void Equip(int index){
            if(!rig||rig.hands==null||index< -1||index>=guns.Length)return;
            var item=index<0?fishing.rod.item:guns[index].item;
            if(rig.Take(item))rig.Notify(index<0?"海钓竿 · 右键抛竿，左键收线":guns[index].title+" · 左键射击，右键瞄准，R 换弹");
        }
        public void ReturnGun(FishWeapon gun){if(gun.item.owner)gun.item.owner.Forget(gun.item);gun.item.Recall();}
        public void AddProjectile(FishWeapon gun,Vector3 position,Vector3 velocity){bullets.Add(new Bullet{gun=gun,p=position,v=velocity});ProjectileFired?.Invoke(gun,position,velocity);}
        void FixedUpdate(){
            int impactBudget=4;
            for(int i=bullets.Count-1;i>=0;i--){
                var b=bullets[i];var step=b.v*Time.fixedDeltaTime;var hits=Physics.SphereCastAll(b.p,.05f,step.normalized,step.magnitude,~0,QueryTriggerInteraction.Ignore);System.Array.Sort(hits,(a,c)=>a.distance.CompareTo(c.distance));bool hitSomething=false;
                var end=b.p+step;float waterDistance=float.PositiveInfinity;
                if(b.p.y>fishing.SurfaceHeight&&end.y<=fishing.SurfaceHeight){float t=(b.p.y-fishing.SurfaceHeight)/(-step.y);var crossing=b.p+step*t;if(fishing.IsWaterAt(crossing,out _))waterDistance=step.magnitude*t;}
                foreach(var hit in hits){
                    var item=hit.collider.GetComponentInParent<FishingItem>();if(hit.collider is CharacterController||item&&item.owner||IgnoreCollider!=null&&IgnoreCollider(hit.collider))continue;
                    if(hit.distance>waterDistance)break;
                    var point=hit.distance>0?hit.point:hit.collider.ClosestPoint(b.p);
                    var fish=hit.collider.GetComponentInParent<FishingCatch>();bool killed=false;
                    if(fish&&!fish.Stored){bool alive=!fish.IsDead;fish.Damage(b.gun.damage,point,b.v.normalized*b.gun.force);killed=alive&&fish.IsDead;if(alive){HitMarker=1;LastHitKilled=fish.IsDead;}}else if(AffectOtherBodies&&hit.rigidbody&&!hit.rigidbody.isKinematic)hit.rigidbody.AddForceAtPosition(b.v.normalized*b.gun.force,point);
                    end=point;
                    ProjectileImpact?.Invoke(point,hit.normal,(int)(killed?FishingShotEffects.HitKind.Killed:fish?FishingShotEffects.HitKind.Fish:FishingShotEffects.HitKind.Surface));
                    if(effects&&(impactBudget>0||killed)){effects.Impact(point,hit.normal,killed?FishingShotEffects.HitKind.Killed:fish?FishingShotEffects.HitKind.Fish:FishingShotEffects.HitKind.Surface,fish?hit.collider.transform:null);impactBudget--;}
                    hitSomething=true;break;
                }
                if(!hitSomething&&!float.IsPositiveInfinity(waterDistance)){end=b.p+step.normalized*waterDistance;hitSomething=true;ProjectileImpact?.Invoke(end,Vector3.up,(int)FishingShotEffects.HitKind.Water);if(effects&&impactBudget>0){effects.Impact(end,Vector3.up,FishingShotEffects.HitKind.Water);impactBudget--;}}
                ProjectileSegment?.Invoke(b.p,end);if(effects)effects.TraceSegment(b.p,end);
                if(hitSomething||b.distance>400){bullets.RemoveAt(i);continue;}
                b.p+=step;b.distance+=step.magnitude;bullets[i]=b;
            }
        }
        public void RegisterKill(FishingCatch fish){Kills++;LastHitKilled=true;HitMarker=1;}
        public bool TriggerHand(int hand,bool trigger,bool previous,bool reload){var item=rig.hands[hand].Held;if(!item)return false;var gun=item.GetComponent<FishWeapon>();if(!gun)return false;gun.Input(trigger,previous,reload);return true;}
    }
}
