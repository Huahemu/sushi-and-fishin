using System;
using UnityEngine;
using PaperPixelGames.Core.Surf;
namespace SushiAndFish.FishingIntegration {
    // How to Fish 1.1.1: six-point buoyancy, steering by rotating the motor,
    // thrust at the propeller, authored inertia/drag and delayed engine start.
    public sealed class OriginalBoatRig:MonoBehaviour {
        public Rigidbody body;public Transform[] forcePoints;public Transform motor,propeller,steering,throttle,driverSeat;public Collider[] hull;
        public float motorForce=800,waterForce=800,motorAngle=20,steerAngle=120,throttleAngle=70,steerSmoothing=.25f,startDelay=1.6f;
        public float bounciness=1,antiRoll=2500,maxAntiRollHeight=2,maxVelocity=50,waterLinearDrag=.75f,waterAngularDrag=2;
        public AnimationCurve returnForce=new AnimationCurve(new Keyframe(0,10,0,0),new Keyframe(10000,500,.15066573f,.15066573f));
        public ArchipelagoWorld world;public ulong driver=ulong.MaxValue;public Vector2 Input{get;private set;}public bool MotorReady=>Time.time>=readyAt;
        Vector2 smoothed,smoothingVelocity;Quaternion wheelRest,throttleRest,remoteRotation;Vector3 remotePosition,remoteVelocity;bool remoteReceived;float readyAt,lastInputAt;AudioSource engine,idle,oneShot,water;PhysicsMaterial friction;
        public float Speed=>world&&world.Server?body.linearVelocity.magnitude:remoteVelocity.magnitude;
        public int RelocationRevision{get;private set;}
        public Vector3 DriverFeet{get{var seat=driverSeat.position;float floor=float.NegativeInfinity;var ray=new Ray(seat+Vector3.up*.1f,Vector3.down);foreach(var collider in hull)if(collider.Raycast(ray,out var hit,2)&&hit.normal.y>.5f)floor=Mathf.Max(floor,hit.point.y);return new Vector3(seat.x,float.IsFinite(floor)?floor+.05f:seat.y-.6f,seat.z);}}
        public void Bind(ArchipelagoWorld owner,bool server){world=owner;body.isKinematic=!server;body.maxLinearVelocity=maxVelocity;body.interpolation=RigidbodyInterpolation.Interpolate;
            body.ResetCenterOfMass();var center=body.centerOfMass;center.x=0;body.centerOfMass=center;
            wheelRest=steering.localRotation;throttleRest=throttle.localRotation;friction=new PhysicsMaterial("How to Fish boat friction");foreach(var c in hull)c.sharedMaterial=friction;
            engine=Audio(world.Settings.motorRun,true);idle=Audio(world.Settings.motorIdle,true);oneShot=Audio(null,false);water=Audio(world.Settings.waterMoving,true);if(water.clip)water.Play();
        }
        AudioSource Audio(AudioClip clip,bool loop){var source=gameObject.AddComponent<AudioSource>();source.clip=clip;source.loop=loop;source.playOnAwake=false;source.spatialBlend=1;source.minDistance=3;source.maxDistance=55;source.volume=0;return source;}
        public void SetDriver(ulong id){if(driver==id)return;driver=id;Input=Vector2.zero;readyAt=Time.time+startDelay;lastInputAt=Time.unscaledTime;var clip=id==ulong.MaxValue?world.Settings.motorStop:world.Settings.motorStart;if(oneShot&&clip){oneShot.volume=.5f;oneShot.PlayOneShot(clip);}if(id==ulong.MaxValue){if(engine)engine.Stop();if(idle)idle.Stop();}}
        public void SetInput(Vector2 value){Input=Vector2.ClampMagnitude(value,1.414214f);Input=new Vector2(Mathf.Clamp(Input.x,-1,1),Mathf.Clamp(Input.y,-1,1));lastInputAt=Time.unscaledTime;}
        float Water(Vector3 p)=>SurfboardWorld.Instance?SurfboardWorld.Instance.SampleHeight(p):world.SeaHeight;
        void Update(){if(!world)return;
            // Interpolate every rendered frame, rather than only on receipt of
            // a network packet. The authoritative body remains on the host.
            if(!world.Server&&remoteReceived){float t=1-Mathf.Exp(-20*Time.deltaTime);body.position=Vector3.Lerp(body.position,remotePosition,t);body.rotation=Quaternion.Slerp(body.rotation,remoteRotation,t);}
            smoothed.x=Mathf.SmoothDamp(smoothed.x,Input.x,ref smoothingVelocity.x,steerSmoothing);smoothed.y=Mathf.SmoothDamp(smoothed.y,MotorReady?Input.y:0,ref smoothingVelocity.y,steerSmoothing);
            motor.localRotation=Quaternion.Euler(-90,0,-smoothed.x*motorAngle);propeller.Rotate(smoothed.y*5000*Time.deltaTime,0,0,Space.Self);
            steering.localRotation=wheelRest*Quaternion.AngleAxis(smoothed.x*steerAngle,Vector3.forward);throttle.localRotation=throttleRest*Quaternion.AngleAxis(-smoothed.y*throttleAngle,Vector3.up);
            bool running=driver!=ulong.MaxValue&&MotorReady;
            if(running){if(engine&&!engine.isPlaying)engine.Play();if(idle&&!idle.isPlaying)idle.Play();if(engine){engine.volume=Mathf.Abs(smoothed.y);engine.pitch=Mathf.Lerp(.75f,1,Mathf.Abs(smoothed.y));}if(idle){idle.volume=.5f*(1-Mathf.Abs(smoothed.y));idle.pitch=Mathf.SmoothStep(1,2,Mathf.Abs(smoothed.y));}}
            if(water)water.volume=Mathf.Lerp(water.volume,transform.position.y<Water(transform.position)+.5f?Mathf.Lerp(.08f,.4f,Mathf.Clamp01(Speed/8)):0,Time.deltaTime*5);
        }
        void FixedUpdate(){if(!world||!world.Server)return;if(driver!=ulong.MaxValue&&Time.unscaledTime-lastInputAt>.5f)Input=Vector2.zero;
            bool wet=Water(propeller.position)>=propeller.position.y;
            foreach(var point in forcePoints){float depth=Water(point.position)-point.position.y;if(depth<0)continue;float lift=waterForce*Mathf.Abs(depth);float damp=Mathf.Clamp(-body.GetPointVelocity(point.position).y*waterForce*(1-bounciness),-lift,waterForce);body.AddForceAtPosition(Vector3.up*(lift+damp),point.position,ForceMode.Force);}
            if(body.position.y<=world.SeaHeight+maxAntiRollHeight&&Vector3.Dot(transform.up,Vector3.up)<.9f){var q=Quaternion.Euler(0,transform.eulerAngles.y,0)*Quaternion.Inverse(body.rotation);if(q.w<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);q.ToAngleAxis(out float angle,out Vector3 axis);if(angle>180)angle-=360;body.AddTorque(axis*(angle*Mathf.Deg2Rad*antiRoll));}
            body.linearDamping=wet?waterLinearDrag:.05f;body.angularDamping=wet?waterAngularDrag:.25f;
            if(driver!=ulong.MaxValue&&MotorReady&&Input.y!=0&&wet)body.AddForceAtPosition(-propeller.right*(motorForce*Input.y),propeller.position);
            if(driver==ulong.MaxValue&&wet&&!world.AnyPlayerNear(transform.position,10)){var offset=world.NearestDock(transform.position)-transform.position;body.AddForce(offset.normalized*returnForce.Evaluate(offset.sqrMagnitude));}
            if(friction){float f=Mathf.Max(0,.6f-.06f*body.linearVelocity.magnitude);friction.dynamicFriction=friction.staticFriction=f;}
        }
        public void Relocate(Vector3 position,Quaternion rotation){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;SetInput(Vector2.zero);body.position=position;body.rotation=rotation;transform.SetPositionAndRotation(position,rotation);RelocationRevision++;}
        public void ApplyRemote(Vector3 position,Quaternion rotation,ulong captain,Vector2 input,Vector3 velocity,bool snap=false){SetDriver(captain);SetInput(input);remotePosition=position;remoteRotation=rotation;remoteVelocity=velocity;if(snap||!remoteReceived||Vector3.Distance(body.position,position)>25){body.position=position;body.rotation=rotation;transform.SetPositionAndRotation(position,rotation);}remoteReceived=true;}
        void OnDestroy(){if(friction)Destroy(friction);}
    }
}
