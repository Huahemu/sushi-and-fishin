using UnityEngine;
using UnityEngine.Rendering;
namespace HowToFish {
    // Local visual pools; one event director owns the temporary environment blend.
    public sealed class FishingWeather:MonoBehaviour {
        public FishingModule module;
        public System.Action<FishingEventKind,float> EnvironmentBlend;
        public System.Func<Vector3,bool> Covered;
        public Material rainMaterial;
        public AudioClip rainLoop,mistLoop;
        public Transform[] rainFishSigns;
        [Range(32,240)] public int rainStreaks=180;
        [Range(4,16)] public int surfaceRings=12;
        public float mistNear=12,mistFar=55;
        Mesh rainMesh;
        MeshRenderer rainRenderer;
        Vector3[] vertices;
        Color[] colors;
        AudioSource rainSound,mistSound;
        bool captured;
        bool savedFog;
        FogMode savedFogMode;
        Color savedFogColor,savedAmbient,savedBackground,savedSunColor;
        float savedFogStart,savedFogEnd,savedFogDensity,savedSunIntensity;
        Light savedSun;
        Camera savedCamera;
        CameraClearFlags savedClearFlags;
        void Prepare(){
            if(rainMesh)return;
            int quads=rainStreaks+surfaceRings*12;
            vertices=new Vector3[quads*4];colors=new Color[vertices.Length];var uv=new Vector2[vertices.Length];var triangles=new int[quads*6];
            for(int i=0;i<quads;i++){
                int v=i*4,t=i*6;uv[v]=new Vector2(0,0);uv[v+1]=new Vector2(1,0);uv[v+2]=new Vector2(1,1);uv[v+3]=new Vector2(0,1);
                triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;triangles[t+3]=v;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            rainMesh=new Mesh{name="Pooled rain streaks and sea rings"};rainMesh.MarkDynamic();rainMesh.vertices=vertices;rainMesh.uv=uv;rainMesh.triangles=triangles;
            var root=new GameObject("Local rain visual pool");root.transform.SetParent(transform,false);root.AddComponent<MeshFilter>().sharedMesh=rainMesh;
            rainRenderer=root.AddComponent<MeshRenderer>();rainRenderer.sharedMaterial=rainMaterial;rainRenderer.shadowCastingMode=ShadowCastingMode.Off;rainRenderer.receiveShadows=false;rainRenderer.enabled=false;
            rainSound=LoopSource("Rain ambience",rainLoop);mistSound=LoopSource("Sea mist wind",mistLoop);
        }
        AudioSource LoopSource(string title,AudioClip clip){var root=new GameObject(title);root.transform.SetParent(transform,false);var source=root.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=true;source.clip=clip;source.spatialBlend=0;source.volume=0;return source;}
        void Capture(){
            if(captured)return;captured=true;savedFog=RenderSettings.fog;savedFogMode=RenderSettings.fogMode;savedFogColor=RenderSettings.fogColor;savedFogStart=RenderSettings.fogStartDistance;savedFogEnd=RenderSettings.fogEndDistance;savedFogDensity=RenderSettings.fogDensity;savedAmbient=RenderSettings.ambientLight;
            savedSun=RenderSettings.sun;if(!savedSun)foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.type==LightType.Directional){savedSun=light;break;}
            if(savedSun){savedSunIntensity=savedSun.intensity;savedSunColor=savedSun.color;}
            savedCamera=module.rig.view;savedBackground=savedCamera.backgroundColor;savedClearFlags=savedCamera.clearFlags;
        }
        public void Apply(FishingEventKind kind,float intensity){
            bool rain=kind==FishingEventKind.RainSquall,mist=kind==FishingEventKind.SeaMist;
            if((!rain&&!mist)||intensity<=0||!module||!module.rig||!module.rig.view){Clear();return;}
            Prepare();intensity=Mathf.Clamp01(intensity);
            if(EnvironmentBlend!=null){EnvironmentBlend(kind,intensity);rainRenderer.enabled=rain&&rainMaterial;if(rain){UpdateRain(intensity);UpdateFishSigns(intensity);}else HideFishSigns();SetLoop(rainSound,rain?intensity*.16f:0);SetLoop(mistSound,mist?intensity*.11f:0);return;}
            Capture();
            var tint=mist?new Color(.52f,.66f,.70f):new Color(.39f,.51f,.60f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=Color.Lerp(savedFog?savedFogColor:savedBackground,tint,intensity);
            RenderSettings.fogStartDistance=Mathf.Lerp(savedFog?savedFogStart:120,mist?mistNear:38,intensity);
            RenderSettings.fogEndDistance=Mathf.Lerp(savedFog?savedFogEnd:260,mist?mistFar:125,intensity);
            RenderSettings.ambientLight=Color.Lerp(savedAmbient,savedAmbient*(mist?.82f:.72f),intensity);
            if(savedSun){savedSun.intensity=Mathf.Lerp(savedSunIntensity,savedSunIntensity*(mist?.65f:.55f),intensity);savedSun.color=Color.Lerp(savedSunColor,new Color(.76f,.86f,.93f),intensity*.5f);}
            savedCamera.clearFlags=CameraClearFlags.SolidColor;savedCamera.backgroundColor=Color.Lerp(savedBackground,tint,intensity);
            rainRenderer.enabled=rain&&rainMaterial;
            if(rain){UpdateRain(intensity);UpdateFishSigns(intensity);}else HideFishSigns();
            SetLoop(rainSound,rain?intensity*.16f:0);SetLoop(mistSound,mist?intensity*.11f:0);
        }
        static float Noise(int seed){uint n=(uint)(seed+1)*747796405u+2891336453u;n=((n>>((int)(n>>28)+4))^n)*277803737u;return ((n>>22)^n)/4294967295f;}
        void UpdateRain(float intensity){
            var view=module.rig.view.transform;var inverse=rainRenderer.transform.worldToLocalMatrix;
            var basePoint=view.position;basePoint.y=module.SurfaceHeight+.08f;
            var right=Vector3.ProjectOnPlane(view.right,Vector3.up).normalized;if(right.sqrMagnitude<.1f)right=Vector3.right;
            int quad=0;
            for(int i=0;i<rainStreaks;i++){
                float a=Noise(i*7)*Mathf.PI*2,radius=2+Noise(i*7+1)*13;
                var p=basePoint+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);p.y+=Mathf.Repeat(Noise(i*7+2)*14-Time.time*12,14);
                float visibility=Covered!=null&&Covered(new Vector3(p.x,view.position.y,p.z))?0:1;
                var tip=p+new Vector3(.06f,.45f+Noise(i*7+3)*.3f,.02f);var width=right*.012f;
                SetQuad(quad++,p-width,p+width,tip+width,tip-width,new Color(.65f,.82f,.95f,intensity*.48f*visibility),inverse);
            }
            var water=module.waterTarget?module.waterTarget.position:basePoint+Vector3.ProjectOnPlane(view.forward,Vector3.up).normalized*10;water.y=module.SurfaceHeight+.025f;
            for(int i=0;i<surfaceRings;i++){
                float phase=Mathf.Repeat(Time.time*(.7f+Noise(i+300)) + Noise(i+600),1),radius=.08f+phase*.45f;
                var p=water+new Vector3((Noise(i+700)-.5f)*20,0,(Noise(i+800)-.5f)*20);var color=new Color(.64f,.86f,.92f,module.IsWaterAt(p,out _)?(1-phase)*intensity*.32f:0);
                for(int n=0;n<12;n++){
                    float a=n*Mathf.PI/6,b=(n+1)*Mathf.PI/6;var d0=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var d1=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                    SetQuad(quad++,p+d0*radius,p+d1*radius,p+d1*(radius+.025f),p+d0*(radius+.025f),color,inverse);
                }
            }
            rainMesh.vertices=vertices;rainMesh.colors=colors;rainMesh.RecalculateBounds();
        }
        void SetQuad(int index,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,Matrix4x4 inverse){int v=index*4;vertices[v]=inverse.MultiplyPoint3x4(a);vertices[v+1]=inverse.MultiplyPoint3x4(b);vertices[v+2]=inverse.MultiplyPoint3x4(c);vertices[v+3]=inverse.MultiplyPoint3x4(d);for(int n=0;n<4;n++)colors[v+n]=color;}
        void UpdateFishSigns(float intensity){
            if(rainFishSigns==null)return;var water=module.waterTarget?module.waterTarget.position:module.rig.view.transform.position+Vector3.forward*12;water.y=module.SurfaceHeight;
            for(int i=0;i<rainFishSigns.Length;i++)if(rainFishSigns[i]){
                float t=Mathf.Repeat(Time.time*.27f+i*.47f,1),jump=Mathf.Clamp01(t/.28f);bool visible=t<.28f&&intensity>.3f;
                rainFishSigns[i].gameObject.SetActive(visible);if(!visible)continue;
                var p=water+new Vector3((i==0?-3:4)+jump*2,Mathf.Sin(jump*Mathf.PI)*1.15f-.15f,i*3);
                rainFishSigns[i].SetPositionAndRotation(p,Quaternion.Euler(-90,90,Mathf.Lerp(-30,30,jump)));
            }
        }
        static void SetLoop(AudioSource source,float volume){if(!source||!source.clip)return;source.volume=volume;if(volume>.001f){if(!source.isPlaying)source.Play();}else if(source.isPlaying)source.Stop();}
        void HideFishSigns(){if(rainFishSigns!=null)foreach(var fish in rainFishSigns)if(fish&&fish.gameObject.activeSelf)fish.gameObject.SetActive(false);}
        public void Clear(){
            EnvironmentBlend?.Invoke(FishingEventKind.None,0);
            if(rainRenderer)rainRenderer.enabled=false;HideFishSigns();if(rainSound)rainSound.Stop();if(mistSound)mistSound.Stop();
            if(!captured)return;captured=false;RenderSettings.fog=savedFog;RenderSettings.fogMode=savedFogMode;RenderSettings.fogColor=savedFogColor;RenderSettings.fogStartDistance=savedFogStart;RenderSettings.fogEndDistance=savedFogEnd;RenderSettings.fogDensity=savedFogDensity;RenderSettings.ambientLight=savedAmbient;
            if(savedSun){savedSun.intensity=savedSunIntensity;savedSun.color=savedSunColor;}if(savedCamera){savedCamera.backgroundColor=savedBackground;savedCamera.clearFlags=savedClearFlags;}
        }
        void OnDisable(){Clear();}
        void OnDestroy(){Clear();if(rainMesh)Destroy(rainMesh);}
    }
}
