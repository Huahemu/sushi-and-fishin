using UnityEngine;
using UnityEngine.Rendering;

namespace HowToFish {
    // Visuals follow the swept projectile endpoints; they never decide damage.
    public sealed class FishingShotEffects : MonoBehaviour {
        public Material tracerMaterial,particleMaterial;
        [Range(32,192)] public int tracerPoolSize=128;
        [Range(4,20)] public int impactPoolSize=12;
        [Range(.05f,.2f)] public float tracerLifetime=.11f;
        public enum HitKind { Surface,Fish,Killed,Water,Muzzle }
        sealed class Trace {
            public LineRenderer line;
            public Transform tip;
            public MeshRenderer tipRenderer;
            public float until;
        }
        sealed class Burst {
            public ParticleSystem particles;
            public LineRenderer ring;
            public float until,duration,radius;
            public Vector3 position,normal;
            public Color color;
            public Transform wound;
            public Vector3 woundLocalPoint;
            public float bleedUntil,nextDrip;
        }
        Trace[] traces;
        Burst[] bursts;
        Mesh bulletMesh;
        MaterialPropertyBlock properties;
        int nextTrace,nextBurst;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
        static readonly Color Gold=new Color(1,.78f,.23f);
        public static readonly Color BloodColor=new Color(.72f,.018f,.035f);

        void Awake(){Initialize();}
        void Initialize(){
            if(traces!=null||!tracerMaterial||!particleMaterial)return;
            properties=new MaterialPropertyBlock();bulletMesh=MakeBulletMesh();
            traces=new Trace[tracerPoolSize];bursts=new Burst[impactPoolSize];
            for(int i=0;i<traces.Length;i++){
                var line=MakeLine("Pooled bullet trail "+i,2);line.widthMultiplier=.018f;
                line.widthCurve=new AnimationCurve(new Keyframe(0,.2f),new Keyframe(.85f,1),new Keyframe(1,.7f));
                var tip=new GameObject("Visible bullet");tip.transform.SetParent(line.transform,false);
                tip.AddComponent<MeshFilter>().sharedMesh=bulletMesh;
                var renderer=tip.AddComponent<MeshRenderer>();renderer.sharedMaterial=tracerMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.enabled=false;
                traces[i]=new Trace{line=line,tip=tip.transform,tipRenderer=renderer};
            }
            for(int i=0;i<bursts.Length;i++){
                var ring=MakeLine("Pooled impact ring "+i,17);ring.loop=true;ring.widthMultiplier=.012f;
                var go=new GameObject("Pooled hit particles "+i);go.transform.SetParent(transform,false);
                var particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=particles.main;main.playOnAwake=false;main.loop=false;main.duration=.3f;main.startLifetime=new ParticleSystem.MinMaxCurve(.10f,.26f);main.startSpeed=new ParticleSystem.MinMaxCurve(.6f,1.8f);main.startSize=new ParticleSystem.MinMaxCurve(.025f,.055f);main.maxParticles=16;main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=.35f;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                var emission=particles.emission;emission.enabled=false;
                var shape=particles.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=55;shape.radius=.025f;
                var life=particles.colorOverLifetime;life.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});life.color=gradient;
                var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=particleMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                bursts[i]=new Burst{particles=particles,ring=ring};
            }
        }
        LineRenderer MakeLine(string title,int count){
            var go=new GameObject(title);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=count;line.sharedMaterial=tracerMaterial;line.numCapVertices=2;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;return line;
        }
        public void TraceSegment(Vector3 from,Vector3 to){
            Initialize();if(traces==null)return;var delta=to-from;if(delta.sqrMagnitude<.000001f)return;
            var trace=traces[nextTrace++%traces.Length];var direction=delta.normalized;
            trace.line.SetPosition(0,to-direction*Mathf.Min(delta.magnitude,4.5f));trace.line.SetPosition(1,to);trace.line.enabled=true;trace.line.startColor=trace.line.endColor=Gold;trace.until=Time.time+tracerLifetime;
            trace.tip.SetPositionAndRotation(to,Quaternion.LookRotation(direction));trace.tip.localScale=new Vector3(.024f,.024f,.10f);trace.tipRenderer.enabled=true;
            properties.SetColor(BaseColor,new Color(1,.96f,.70f));trace.tipRenderer.SetPropertyBlock(properties);
        }
        public void Muzzle(Transform muzzle){if(muzzle)Impact(muzzle.position,muzzle.forward,HitKind.Muzzle);}
        public void Impact(Vector3 position,Vector3 normal,HitKind kind,Transform wound=null){
            Initialize();if(bursts==null)return;
            var burst=bursts[nextBurst++%bursts.Length];normal=normal.sqrMagnitude>.01f?normal.normalized:Vector3.up;
            bool muzzle=kind==HitKind.Muzzle,water=kind==HitKind.Water,killed=kind==HitKind.Killed,blood=kind==HitKind.Fish||killed;
            burst.position=position+normal*.012f;burst.normal=normal;burst.duration=muzzle?.045f:water?.3f:.22f;burst.until=Time.time+burst.duration;burst.radius=muzzle?.055f:killed?.25f:water?.22f:.13f;
            burst.color=blood?BloodColor:water?new Color(.45f,.90f,1):new Color(1,.89f,.63f);
            burst.wound=blood?wound:null;burst.woundLocalPoint=wound?wound.InverseTransformPoint(position):Vector3.zero;burst.bleedUntil=blood?Time.time+.55f:0;burst.nextDrip=Time.time+.08f;
            burst.particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);burst.particles.transform.SetPositionAndRotation(burst.position,Quaternion.LookRotation(normal));
            var main=burst.particles.main;main.duration=blood?1.2f:muzzle?.12f:.4f;main.maxParticles=blood?32:16;
            main.startColor=blood?new ParticleSystem.MinMaxGradient(new Color(.35f,.004f,.012f),BloodColor):new ParticleSystem.MinMaxGradient(burst.color);
            main.startLifetime=new ParticleSystem.MinMaxCurve(blood?.4f:muzzle?.025f:.10f,blood?.85f:muzzle?.05f:.26f);main.startSpeed=new ParticleSystem.MinMaxCurve(blood?1.1f:muzzle?.3f:.6f,blood?2.8f:muzzle?1.2f:1.8f);main.startSize=new ParticleSystem.MinMaxCurve(.025f,blood?.065f:muzzle?.06f:.055f);main.gravityModifier=blood?1.2f:water?.6f:muzzle?0:.35f;
            var shape=burst.particles.shape;shape.angle=blood?35:55;
            burst.particles.Play();burst.particles.Emit(muzzle?5:killed?22:blood?16:10);
            burst.ring.enabled=!muzzle&&!blood;if(burst.ring.enabled)DrawRing(burst,0);
        }
        void Update(){
            if(traces==null)return;
            foreach(var trace in traces){
                if(!trace.line.enabled)continue;float remaining=trace.until-Time.time;
                if(remaining<=0){trace.line.enabled=false;trace.tipRenderer.enabled=false;continue;}
                var color=Gold;color.a=Mathf.Clamp01(remaining/tracerLifetime);trace.line.startColor=trace.line.endColor=color;
                trace.tipRenderer.enabled=remaining>tracerLifetime*.55f;
            }
            foreach(var burst in bursts){
                if(burst.ring.enabled){float t=1-(burst.until-Time.time)/burst.duration;if(t>=1)burst.ring.enabled=false;else DrawRing(burst,t);}
                if(burst.wound&&burst.wound.gameObject.activeInHierarchy&&Time.time<burst.bleedUntil&&Time.time>=burst.nextDrip){
                    burst.nextDrip=Time.time+.08f;
                    var drip=new ParticleSystem.EmitParams{position=burst.wound.TransformPoint(burst.woundLocalPoint),velocity=Vector3.down*.5f+Random.insideUnitSphere*.15f,startColor=BloodColor,startSize=Random.Range(.025f,.045f),startLifetime=.6f};
                    burst.particles.Emit(drip,2);
                }
            }
        }
        void DrawRing(Burst burst,float t){
            var tangent=Vector3.Cross(burst.normal,Vector3.up);if(tangent.sqrMagnitude<.01f)tangent=Vector3.Cross(burst.normal,Vector3.right);tangent.Normalize();var bitangent=Vector3.Cross(burst.normal,tangent);
            float radius=Mathf.Lerp(burst.radius*.3f,burst.radius,t);for(int i=0;i<17;i++){float angle=i*Mathf.PI*2/17;burst.ring.SetPosition(i,burst.position+(tangent*Mathf.Cos(angle)+bitangent*Mathf.Sin(angle))*radius);}
            var color=burst.color;color.a=(1-t)*.85f;burst.ring.startColor=burst.ring.endColor=color;burst.ring.widthMultiplier=Mathf.Lerp(.016f,.005f,t);
        }
        static Mesh MakeBulletMesh(){
            var mesh=new Mesh{name="Visible bullet - shared six vertex mesh"};mesh.vertices=new[]{new Vector3(0,0,.5f),new Vector3(0,0,-.5f),new Vector3(.5f,0,0),new Vector3(0,.5f,0),new Vector3(-.5f,0,0),new Vector3(0,-.5f,0)};mesh.triangles=new[]{0,2,3,0,3,4,0,4,5,0,5,2,1,3,2,1,4,3,1,5,4,1,2,5};mesh.colors=new[]{Color.white,Color.white,Color.white,Color.white,Color.white,Color.white};mesh.RecalculateBounds();return mesh;
        }
        void OnDisable(){if(traces==null)return;foreach(var trace in traces){trace.line.enabled=false;trace.tipRenderer.enabled=false;}foreach(var burst in bursts){burst.ring.enabled=false;burst.wound=null;burst.bleedUntil=0;burst.particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}}
        void OnDestroy(){if(bulletMesh)Destroy(bulletMesh);}
    }
}
