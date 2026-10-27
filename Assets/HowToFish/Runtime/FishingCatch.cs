using UnityEngine;
namespace HowToFish {
    public sealed class FishingCatch : MonoBehaviour {
        public FishDefinition definition;
        public FishingItem item;
        public Rigidbody body,tailBody;
        public Transform hookAnchor;
        public HingeJoint tailJoint;
        public FishingModule fishing;
        public float weight;
        [HideInInspector] public float shootingScale=1;
        public bool Landed,Stored;
        public GameObject regularVisual,dripVisual;
        [HideInInspector] public int landingSlot=-1;
        public bool Attached {get;private set;}
        public bool IsDead=>Health<=0;
        public int Health {get;private set;}
        public bool IsDrip {get;private set;}
        public bool HasReachedShore {get;private set;}
        public bool HorseRevealed{get;private set;}
        public bool Escaped{get;private set;}
        public bool IsPursuingPlayer=>definition&&definition.ShoreAttackEligible&&Shore.pursuePlayer;
        public bool IsAttackingPlayer=>!Attached&&!IsDead&&Time.fixedTime<attackUntil;
        public bool ArchiveLifted=>archiveLifted;
        bool archiveLifted,archiveAcquired;
        void MarkArchiveLifted(){if(archiveLifted||!fishing||!fishing.inventory)return;archiveLifted=true;fishing.inventory.RecordLifted(definition,IsDrip);}
        public void MarkArchiveAcquired(){if(archiveAcquired||!IsDead||!fishing||!fishing.inventory)return;archiveAcquired=true;MarkArchiveLifted();fishing.inventory.RecordAcquired(definition,IsDrip,weight);}
        public string DisplayName=>definition.seaHorse?(HorseRevealed?"海马（真马）":"海马"):definition.displayName;
        public bool IsAvailable=>isActiveAndEnabled&&item&&!Stored&&!Escaped&&!item.Retired;
        public bool CanGrab=>IsAvailable&&IsDead;
        public bool IsEscaping=>IsAvailable&&!IsDead&&!Attached&&!item.owner;
        public bool IsInWater=>IsEscaping&&fishing&&body.worldCenterOfMass.y<fishing.SurfaceHeight&&fishing.IsWaterAt(body.worldCenterOfMass,out _);
        public float RescueSeconds=>0;
        public int MaximumHealth=>definition?definition.MaximumHealth:60;
        Rigidbody[] bodies;
        Collider[] supportColliders;
        HingeJoint[] hinges;
        Vector3[] restPositions;
        Quaternion[] restRotations;
        Animation[] animations;
        float rest,motor,lastContact=-100,lastPlayerHit=-100;
        float shotStep=-100,shotVelocityUsed;
        float lastGroundContact=-100;
        float nextAttackAt=-100,attackUntil=-100,escapeRefreshAt=-100;
        float nextLandHopAt=-1;
        Vector3 escapeTarget,lastEscapeSearch;
        readonly EscapeWaterSearch escapeSearch=new EscapeWaterSearch();
        bool escapeTargetFound;
        bool motorStarted,wasContact;
        int sign=1;
        OriginalFishMotion Motion=>definition.original??new OriginalFishMotion();
        static readonly ShoreFishMotion defaultShoreMotion=new ShoreFishMotion();
        ShoreFishMotion Shore=>definition.shore??defaultShoreMotion;
        void Awake(){
            Health=MaximumHealth;bodies=GetComponentsInChildren<Rigidbody>(true);supportColliders=GetComponentsInChildren<Collider>(true);hinges=GetComponentsInChildren<HingeJoint>(true);animations=GetComponentsInChildren<Animation>(true);
            if(hinges.Length>0)foreach(var part in bodies){part.solverIterations=12;part.solverVelocityIterations=4;}
            restPositions=new Vector3[bodies.Length];restRotations=new Quaternion[bodies.Length];
            for(int i=0;i<bodies.Length;i++){restPositions[i]=bodies[i].transform.localPosition;restRotations[i]=bodies[i].transform.localRotation;}
            rest=Random.Range(Motion.restMin,Motion.restMax);
            // Physics-driven bones can leave the bounds imported from their bind pose.
            // Keep the small, capped set of loose catches updating their skin bounds.
            foreach(var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
        }
        public void RestoreRuntimeState(int health,bool reachedShore,bool seen=false){archiveLifted=seen;Health=Mathf.Clamp(health,0,MaximumHealth);Attached=false;Landed=true;HasReachedShore=reachedShore;foreach(var part in bodies){part.isKinematic=false;part.useGravity=true;}if(fishing&&fishing.BoundedLineMotion)ApplyMotionLimits();if(IsDead)StopMotor();}
        float suspendedBottomOffset;
        Quaternion suspendedRotation;
        public void SuspendOnLine(){suspendedBottomOffset=BottomY-transform.position.y;suspendedRotation=transform.rotation;StopMotor();foreach(var part in bodies){if(!part.isKinematic){part.linearVelocity=Vector3.zero;part.angularVelocity=Vector3.zero;}part.isKinematic=true;part.useGravity=false;part.interpolation=RigidbodyInterpolation.None;}foreach(var c in supportColliders)c.enabled=false;}
        public void MoveOnLine(Vector3 point){
            if(fishing&&fishing.BoundedLineMotion&&fishing.Hooked==this&&!IsDead)transform.rotation=FishingMotionMath.LineWriggle(suspendedRotation,Time.time);
            var delta=point-(hookAnchor?hookAnchor.position:body.position);transform.position+=delta;
            foreach(var part in bodies){part.position=part.transform.position;part.rotation=part.transform.rotation;}
            if(fishing&&body.worldCenterOfMass.y>fishing.SurfaceHeight+.15f)MarkArchiveLifted();
        }
        public float BottomY {get{if(Attached&&body.isKinematic)return transform.position.y+suspendedBottomOffset;float bottom=float.PositiveInfinity;foreach(var c in supportColliders)if(!c.isTrigger&&c.gameObject.activeInHierarchy)bottom=Mathf.Min(bottom,c.bounds.min.y);return float.IsPositiveInfinity(bottom)?body.position.y-.25f:bottom;}}
        void ApplyMotionLimits(){foreach(var part in bodies){part.maxDepenetrationVelocity=2;part.maxAngularVelocity=6;part.maxLinearVelocity=16;part.linearDamping=0;}}
        public void EnableLandingHitboxes(){foreach(var c in supportColliders)c.enabled=true;}
        public void ConfirmShoreLanding(){HasReachedShore=true;Landed=true;MarkArchiveLifted();}
        public void ResumeFromLine(){if(!Attached)return;foreach(var part in bodies){part.isKinematic=false;part.useGravity=true;part.interpolation=RigidbodyInterpolation.Interpolate;part.linearVelocity=Vector3.zero;part.angularVelocity=Vector3.zero;part.linearDamping=0;if(fishing&&fishing.BoundedLineMotion){part.maxDepenetrationVelocity=2;part.maxAngularVelocity=6;part.maxLinearVelocity=16;}}foreach(var c in supportColliders)c.enabled=true;}
        public void SetDrip(bool value){IsDrip=value&&regularVisual&&dripVisual;if(regularVisual&&dripVisual){regularVisual.SetActive(!IsDrip);dripVisual.SetActive(IsDrip);}}
        public void Attach(){Attached=true;Landed=false;HasReachedShore=false;nextLandHopAt=-1;attackUntil=-100;escapeTargetFound=false;escapeRefreshAt=-100;}
        public void MarkLanded(){Attached=false;Landed=true;foreach(var part in bodies)part.linearDamping=0;}
        public void SettleOnShore(){
            // A fast haul must not leave the fish sliding across the whole
            // platform. Stop the reel momentum equally on connected sections.
            var velocity=Vector3.ClampMagnitude(Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up),Mathf.Min(1.4f,Shore.escapeSpeed));
            velocity+=Vector3.up*Mathf.Clamp(body.linearVelocity.y,-1,.3f);
            foreach(var part in bodies)if(!part.isKinematic){part.linearVelocity=velocity;part.angularVelocity=Vector3.ClampMagnitude(part.angularVelocity,4);}
            StopMotor();lastGroundContact=Time.fixedTime;nextLandHopAt=-1;
        }
        public void SetHeld(bool held){
            foreach(var part in bodies){part.isKinematic=held;part.useGravity=!held;}
            if(held){
                // A dead fish can still be bent by its ragdoll. Freeze first,
                // then restore the connected sections before parenting to a hand.
                for(int i=0;i<bodies.Length;i++)if(bodies[i]!=body){bodies[i].transform.localPosition=restPositions[i];bodies[i].transform.localRotation=restRotations[i];}
                foreach(var part in bodies){part.position=part.transform.position;part.rotation=part.transform.rotation;}
            }
            StopMotor();
        }
        // Match the original boat's delta motion for every connected fish body.
        // Applying it only to the head would stretch its joints apart.
        public void CarryOnBoat(Vector3 previous,Vector3 current,Quaternion delta){
            if(!IsAvailable||Attached||item.owner)return;
            foreach(var part in bodies){part.position=current+delta*(part.position-previous);part.rotation=delta*part.rotation;if(!part.isKinematic){part.linearVelocity=delta*part.linearVelocity;part.angularVelocity=delta*part.angularVelocity;}}
        }
        void FixedUpdate(){
            if(!fishing||!IsAvailable||item.owner||body.isKinematic)return;
            if(body.worldCenterOfMass.y>fishing.SurfaceHeight+.15f)MarkArchiveLifted();
            if(definition.seaHorse&&!HorseRevealed&&body.worldCenterOfMass.y>fishing.SurfaceHeight+.2f){HorseRevealed=true;item.name=(IsDrip?"稀有 ":"")+DisplayName+" "+weight.ToString("0.00")+" kg";if(fishing.worldEvents)fishing.worldEvents.RevealHorse(this);}
            if(IsDead){
                if(motorStarted)StopMotor();
                bool openWater=body.worldCenterOfMass.y<fishing.SurfaceHeight+.15f&&fishing.IsWaterAt(body.worldCenterOfMass,out _);
                if(FishingShoreRules.CanFloatCorpse(openWater,body.worldCenterOfMass.y,fishing.SurfaceHeight)){
                    foreach(var part in bodies)if(part.position.y<fishing.SurfaceHeight-.1f)part.AddForce(Vector3.up*((fishing.SurfaceHeight-.1f-part.position.y)*18-part.linearVelocity.y*6),ForceMode.Acceleration);
                }else ApplyShoreWeight();
                return;
            }
            if(!Attached&&Landed){
                // Only actual open water counts. No countdown or safe period.
                if(FishingShoreRules.CanEscape(Landed,Attached,body.worldCenterOfMass.y<fishing.SurfaceHeight-fishing.profile.escapeWaterDepth&&fishing.IsWaterAt(body.worldCenterOfMass,out _),body.worldCenterOfMass.y,fishing.SurfaceHeight,fishing.profile.escapeWaterDepth)){
                    EscapeIntoWater();return;
                }
                ApplyShoreWeight();UpdateLandHops();return;
            }
            var config=Motion;bool contact=Time.fixedTime-lastContact<Time.fixedDeltaTime*2.5f;
            if(contact&&!wasContact){StopMotor();rest=Random.Range(config.restMin,config.restMax);}wasContact=contact;
            if(rest>0){rest-=Time.fixedDeltaTime;if(rest<=0)BeginMotor(contact,false);}
            else if(motor>0){motor-=Time.fixedDeltaTime;if(config.torque)body.AddTorque(transform.up*(sign*config.rotForce));if(motor<=0){StopMotor();rest=Random.Range(config.restMin,config.restMax);}}
            else rest=Mathf.Max(.01f,config.restMin);
        }
        void EscapeIntoWater(){
            if(!IsEscaping)return;
            Escaped=true;Landed=false;StopMotor();
            // Remove the target before archive callbacks can refresh the HUD.
            fishing.ForgetCatch(this);item.Retire();
            if(archiveLifted&&fishing.inventory)fishing.inventory.RecordEscaped(definition);
            fishing.rig.Notify(DisplayName+" 已逃走（落回水里）");
        }
        void OnDisable(){if(fishing)fishing.ForgetCatch(this);}
        bool HasRestingShoreSupport(){
            // Sleeping rigidbodies stop receiving OnCollisionStay. A resting
            // tail can also support a previously landed fish after it rolls.
            // Query only its active physical colliders, close to their bottoms.
            foreach(var collider in supportColliders){
                if(!collider.enabled||collider.isTrigger||!collider.gameObject.activeInHierarchy||!collider.attachedRigidbody)continue;
                var bounds=collider.bounds;
                if(fishing.HasShoreSupport(bounds.center,bounds.extents.y+.04f))return true;
            }
            return false;
        }
        float ShoreGravity=>FishingMotionMath.ShoreGravity(fishing.profile,fishing.BoundedLineMotion,Physics.gravity.y);
        void ApplyShoreWeight(){
            if(!fishing.BoundedLineMotion)return;
            // Apply the same acceleration to every connected body. The rest of
            // the game, held items and underwater buoyancy retain world gravity.
            var correction=Vector3.down*ShoreGravity-Physics.gravity;
            foreach(var part in bodies)if(!part.isKinematic&&part.gameObject.activeInHierarchy)part.AddForce(correction,ForceMode.Acceleration);
        }
        void UpdateLandHops(){
            if(motor>0){motor-=Time.fixedDeltaTime;if(motor<=0)StopMotor();}
            bool grounded=Time.fixedTime-lastGroundContact<Time.fixedDeltaTime*1.6f&&body.linearVelocity.y<=1.8f;
            if(!grounded&&HasReachedShore&&FishingLandingRules.ShouldProbeRestingSupport(body.IsSleeping(),body.linearVelocity)&&HasRestingShoreSupport()){
                grounded=true;lastGroundContact=Time.fixedTime;
            }
            if(!grounded)return;
            // A tail rolling between support contacts must not reset the next
            // hop on every contact. Rearm only after an actual launched hop.
            if(nextLandHopAt<0){StopMotor();nextLandHopAt=Time.fixedTime+Random.Range(fishing.profile.landHopRestMin,Mathf.Max(fishing.profile.landHopRestMin,fishing.profile.landHopRestMax))*Shore.restMultiplier;}
            if(Time.fixedTime<nextLandHopAt)return;
            float gravity=ShoreGravity;float height=fishing.profile.landHopHeight*Shore.hopHeightMultiplier;
            float vertical=FishingMotionMath.HopSpeed(gravity,height);float flightTime=FishingMotionMath.HopSeconds(gravity,height);
            var toward=Vector3.ProjectOnPlane(fishing.rig.view.transform.position-body.worldCenterOfMass,Vector3.up);
            bool attack=definition.ShoreAttackEligible&&Time.fixedTime>=nextAttackAt&&toward.sqrMagnitude<100&&Random.value<Shore.attackChance;
            Vector3 direction;float speed,distance;
            if(attack){
                direction=toward.normalized;speed=Shore.attackSpeed;distance=toward.magnitude;
                nextAttackAt=Time.fixedTime+Shore.attackCooldown;attackUntil=Time.fixedTime+flightTime+.2f;
            }else if(IsPursuingPlayer){
                // Recover or close in between attacks; never seek the water.
                // Only the explicit attack flight can damage the player.
                var facing=toward.sqrMagnitude>.01f?toward.normalized:Vector3.forward;
                bool close=toward.sqrMagnitude<4;
                direction=close?Vector3.Cross(Vector3.up,facing)*sign:facing;
                speed=close?Mathf.Lerp(.55f,.9f,Shore.aggression):Mathf.Lerp(.85f,1.45f,Shore.aggression);
                distance=close?1:Mathf.Max(0,toward.magnitude-1.5f);attackUntil=-100;
            }else{
                var center=body.worldCenterOfMass;
                if(Time.fixedTime>=escapeRefreshAt&&(!escapeTargetFound||(center-lastEscapeSearch).sqrMagnitude>3)){
                    escapeTargetFound=fishing.TryFindEscapeWater(center,escapeSearch,out escapeTarget);lastEscapeSearch=center;escapeRefreshAt=Time.fixedTime+1.5f;
                }
                var fallback=fishing.waterTarget?Vector3.ProjectOnPlane(fishing.waterTarget.position-center,Vector3.up):-toward;
                var escape=escapeTargetFound?Vector3.ProjectOnPlane(escapeTarget-center,Vector3.up):fallback;
                direction=escape.sqrMagnitude>.01f?escape.normalized:Vector3.zero;
                speed=Shore.escapeSpeed;distance=escapeTargetFound?escape.magnitude:100;attackUntil=-100;
            }
            var planar=direction*Mathf.Min(speed*fishing.profile.shoreMoveSpeedMultiplier,distance/flightTime);
            // Equal launch velocity on connected sections prevents stretched skins.
            foreach(var part in bodies)if(!part.isKinematic&&part.gameObject.activeInHierarchy){part.WakeUp();part.linearVelocity=planar+Vector3.up*vertical;}
            StartLandMotor();lastGroundContact=-100;nextLandHopAt=-1;
        }
        void StartLandMotor(){
            var c=Motion;motor=Mathf.Max(.16f,c.motorTime);float speed=Random.Range(-c.rotForce,c.rotForce)*sign;
            foreach(var hinge in hinges){var settings=hinge.motor;settings.targetVelocity=fishing&&fishing.BoundedLineMotion?Mathf.Clamp(speed,-220,220):speed;if(fishing&&fishing.BoundedLineMotion)settings.force=Mathf.Min(settings.force,18);hinge.motor=settings;hinge.useMotor=true;}motorStarted=true;sign=-sign;
            if(hinges.Length==0)body.angularVelocity=Random.onUnitSphere*2.8f;
        }
        void BeginMotor(bool contact,bool underwater){
            var c=Motion;motor=c.motorTime;float speed=Random.Range(-c.rotForce,c.rotForce)*sign;
            foreach(var hinge in hinges){var settings=hinge.motor;settings.targetVelocity=fishing&&fishing.BoundedLineMotion?Mathf.Clamp(speed,-220,220):speed;if(fishing&&fishing.BoundedLineMotion)settings.force=Mathf.Min(settings.force,18);hinge.motor=settings;hinge.useMotor=true;}motorStarted=true;sign=-sign;
            if(contact&&!Attached){body.linearVelocity+=Vector3.up*c.jump;if(c.boss==0){var away=body.position-fishing.islandCenter;away.y=0;body.linearVelocity+=away.normalized*c.waterForce;}}
            if(!Attached&&definition.ShoreAttackEligible&&c.component=="AttackingFish"){
                var target=fishing.rig.view.transform.position-Vector3.up*.7f;
                var toward=(target-body.worldCenterOfMass).normalized;var random=Random.insideUnitSphere*c.randomDirection;random.y=0;var direction=(toward+random).normalized;if(Vector3.Dot(direction,toward)<=0)direction=toward;
                bool grounded=contact||underwater;float multi=underwater?c.waterAttackMulti:1;
                if(grounded||c.attackInAir){body.linearVelocity*=c.keepVelocity;body.linearVelocity+=direction*(grounded?c.attackForce:c.airForce)*multi;if(grounded)body.linearVelocity+=Vector3.up*c.jump*multi;}
            }
        }
        void StopMotor(){foreach(var hinge in hinges)if(hinge)hinge.useMotor=false;motorStarted=false;motor=0;}
        public void Contact(Collision collision,Vector3 before,Rigidbody reportingBody=null){
            if(!fishing||!IsAvailable||IsDead)return;
            if(fishing.TryPlayerContact!=null&&fishing.TryPlayerContact(this,collision.collider))return;
            if(collision.collider.GetComponent<CharacterController>()){TouchPlayer();return;}
            if(collision.collider.GetComponentInParent<FishingItem>())return;
            lastContact=Time.fixedTime;
            bool shore=false;for(int i=0;i<collision.contactCount;i++){
                var contact=collision.GetContact(i);var center=body.worldCenterOfMass;
                if(FishingLandingRules.IsSupportedTopContact(!reportingBody||reportingBody==body,contact.normal,contact.point,center,body.linearVelocity.y,fishing.SurfaceHeight)
                    &&fishing.HasShoreSupport(center,center.y-contact.point.y)){shore=true;break;}
            }
            if(shore){bool firstLanding=!HasReachedShore;lastGroundContact=Time.fixedTime;HasReachedShore=true;Landed=true;MarkArchiveLifted();if(firstLanding&&!Attached)SettleOnShore();}
            if(!Attached){Landed=true;return;}
            // Side impacts leave the line attached, regardless of reel speed.
            if(shore)fishing.ReleaseCatch(this,true);
        }
        public void TouchPlayer(){
            if(!IsAvailable||IsDead||!definition.ShoreAttackEligible||!IsAttackingPlayer||!fishing||Time.time-lastPlayerHit<Shore.contactCooldown)return;
            lastPlayerHit=Time.time;fishing.vitals.Hit(Shore.attackDamage,definition.displayName);
        }
        public void Damage(int amount,Vector3 point,Vector3 force){
            if(!IsAvailable||amount<=0)return;
            if(IsDead)return;
            fishing.FocusCatch(this);
            Health=Mathf.Max(0,Health-amount);
            if(IsDead){StopMotor();foreach(var a in animations)a.Stop();fishing.OnCatchKilled(this);foreach(var part in bodies)if(!part.isKinematic){part.linearVelocity=FishingShoreRules.CorpseVelocity(part.linearVelocity);part.angularVelocity=Vector3.ClampMagnitude(part.angularVelocity,2);}}
            else{ApplyShotKick(force);fishing.rig.Notify(definition.displayName+" · "+Health+" / "+MaximumHealth);}
        }
        void ApplyShotKick(Vector3 force){
            if(!fishing||force.sqrMagnitude==0)return;
            if(shotStep!=Time.fixedTime){shotStep=Time.fixedTime;shotVelocityUsed=0;}
            float mass=0;foreach(var part in bodies)if(!part.isKinematic&&part.gameObject.activeInHierarchy)mass+=part.mass;
            if(mass<=0)return;
            float change=Mathf.Min(force.magnitude*Time.fixedDeltaTime*fishing.profile.shotForceScale/mass,Mathf.Max(0,fishing.profile.maxShotVelocityChange-shotVelocityUsed));
            shotVelocityUsed+=change;
            // Move every connected section equally. Off-centre forces on a single
            // tiny body caused extreme torque, and 25 shotgun pellets stacked it.
            foreach(var part in bodies)if(!part.isKinematic&&part.gameObject.activeInHierarchy)part.AddForce(force.normalized*change,ForceMode.VelocityChange);
        }
        public void Place(Vector3 position,Quaternion rotation){
            for(int i=0;i<bodies.Length;i++){var p=bodies[i];if(!p.isKinematic){p.linearVelocity=Vector3.zero;p.angularVelocity=Vector3.zero;}p.isKinematic=true;p.interpolation=RigidbodyInterpolation.None;}
            transform.SetPositionAndRotation(position,rotation);
            for(int i=0;i<bodies.Length;i++){var p=bodies[i];if(p!=body){p.transform.localPosition=restPositions[i];p.transform.localRotation=restRotations[i];}p.position=p.transform.position;p.rotation=p.transform.rotation;p.isKinematic=false;p.useGravity=true;p.interpolation=RigidbodyInterpolation.Interpolate;}
            StopMotor();rest=Random.Range(Motion.restMin,Motion.restMax);
        }
        public void Recover(){if(!CanGrab||item.owner)return;fishing.GetLandingPose(this,out var position,out var rotation);transform.SetParent(fishing.transform,true);Place(position,rotation);}
    }
}
