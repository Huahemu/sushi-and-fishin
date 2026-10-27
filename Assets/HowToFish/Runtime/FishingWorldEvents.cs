using UnityEngine;
namespace HowToFish {
    public enum FishingEventKind { None,SeagullRun,SeahorseTide,RainSquall,SeaMist }
    public sealed class FishingWorldEvents:MonoBehaviour {
        public FishingModule module;
        public System.Func<bool> Eligibility;
        public float Duration=>activeDuration;
        public FishingSeagull seagullPrefab;
        public FishDefinition seaHorse;
        public Material waterSignalMaterial;
        public AudioClip[] seagullCalls;
        public FishingWeather weather;
        public bool automatic=true;
        public float firstEventDelayMin=20,firstEventDelayMax=40,eventDuration=40,intervalMin=45,intervalMax=75;
        public float weatherDurationMin=90,weatherDurationMax=120,weatherTransition=10;
        [Min(0)] public float seagullWeight=1,seahorseWeight=.8f,rainWeight=1,mistWeight=.8f;
        [Range(.05f,1)] public float repeatWeight=.35f;
        public float rareWeightMultiplier=1.2f,baseShinyChance=.08f,seagullShinyChance=.24f;
        [Range(0,1)] public float seaHorseChance=.5f;
        [Range(3,12)] public int flockSize=9;
        public float flockHeight=9;
        public FishingEventKind Current{get;private set;}
        public float Remaining=>Current==FishingEventKind.None?0:Mathf.Max(0,endsAt-Time.time);
        public float RareMultiplier=>Current==FishingEventKind.SeagullRun?rareWeightMultiplier:Current==FishingEventKind.SeaMist?1.1f:1;
        public float BiteRate=>Current==FishingEventKind.RainSquall?1.75f:1;
        public float ShinyChance=>Current==FishingEventKind.SeagullRun?seagullShinyChance:baseShinyChance;
        public string Title=>Current==FishingEventKind.SeagullRun?"海鸥鱼汛":Current==FishingEventKind.SeahorseTide?"海马出没":Current==FishingEventKind.RainSquall?"阵雨鱼潮":Current==FishingEventKind.SeaMist?"海雾来客":"";
        public string Description=>Current==FishingEventKind.SeagullRun?"海鸥带来鱼群 · 稀有鱼更容易上钩":Current==FishingEventKind.SeahorseTide?"水面出现奇怪波纹 · 试着钓一条海马":Current==FishingEventKind.RainSquall?"雨中鱼群活跃 · 咬钩更快":Current==FishingEventKind.SeaMist?"雾里藏着大物 · 深水远投更有机会":"";
        FishingSeagull[] flock;
        LineRenderer[] ripples;
        AudioSource calls;
        float endsAt,nextEvent,nextCall,activeDuration;
        bool firstHorsePending;
        FishingEventKind previousEvent;
        Vector3 center,right,forward;
        void Start(){Prepare();ScheduleNext(firstEventDelayMin,firstEventDelayMax);}
        void ScheduleNext(float min,float max){min=Mathf.Max(1,min);nextEvent=Time.time+Random.Range(min,Mathf.Max(min,max));}
        void Prepare(){
            if(flock!=null)return;flock=new FishingSeagull[flockSize];
            if(seagullPrefab)for(int i=0;i<flock.Length;i++){var bird=Instantiate(seagullPrefab,transform);bird.name="Event seagull "+(i+1);bird.phase=i*.8f;bird.gameObject.SetActive(false);flock[i]=bird;}
            ripples=new LineRenderer[3];
            if(waterSignalMaterial)for(int i=0;i<ripples.Length;i++){var go=new GameObject("Seahorse tide ripple "+i);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=waterSignalMaterial;line.positionCount=48;line.loop=true;line.useWorldSpace=true;line.widthMultiplier=.035f;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;ripples[i]=line;}
            var audio=new GameObject("Native gull calls");audio.transform.SetParent(transform,false);calls=audio.AddComponent<AudioSource>();calls.playOnAwake=false;calls.volume=.22f;calls.spatialBlend=1;calls.minDistance=3;calls.maxDistance=55;calls.rolloffMode=AudioRolloffMode.Linear;
        }
        void Update(){
            if(!module||!module.rig||!module.rig.view)return;
            if(Eligibility!=null&&!Eligibility()){if(Current!=FishingEventKind.None)EndEvent();nextEvent+=Time.deltaTime;return;}
            if(Current==FishingEventKind.None){if(automatic&&Time.time>=nextEvent){var kind=ChooseEvent(Random.value,previousEvent);if(kind!=FishingEventKind.None)Begin(kind);else ScheduleNext(intervalMin,intervalMax);}return;}
            if(Time.time>=endsAt){EndEvent();return;}
            float t=Mathf.Clamp01(1-Remaining/activeDuration);
            if(weather){bool active=Current==FishingEventKind.RainSquall||Current==FishingEventKind.SeaMist;float blend=active?Mathf.Clamp01(Mathf.Min(activeDuration-Remaining,Remaining)/Mathf.Max(.1f,weatherTransition)):0;weather.Apply(Current,blend);}
            if(Current==FishingEventKind.SeagullRun){
                PositionFlock(t);
                if(seagullCalls!=null&&seagullCalls.Length>0&&Time.time>=nextCall){calls.PlayOneShot(seagullCalls[Random.Range(0,seagullCalls.Length)]);nextCall=Time.time+Random.Range(3.5f,6);}
            }else if(Current==FishingEventKind.SeahorseTide){
                for(int i=0;i<ripples.Length;i++)if(ripples[i]){float wave=Mathf.Repeat(Time.time*.35f+i/3f,1);float radius=Mathf.Lerp(.3f,3.2f,wave);for(int n=0;n<48;n++){float a=n*Mathf.PI*2/48;ripples[i].SetPosition(n,center+new Vector3(Mathf.Cos(a)*radius,.04f,Mathf.Sin(a)*radius));}var color=new Color(.35f,.9f,.87f,(1-wave)*.75f);ripples[i].startColor=ripples[i].endColor=color;}
            }
        }
        public float EventWeight(FishingEventKind kind,FishingEventKind previous){
            float weight=kind==FishingEventKind.SeagullRun?seagullWeight:kind==FishingEventKind.SeahorseTide?seahorseWeight:kind==FishingEventKind.RainSquall?rainWeight:kind==FishingEventKind.SeaMist?mistWeight:0;
            return Mathf.Max(0,weight)*(kind==previous?repeatWeight:1);
        }
        public FishingEventKind ChooseEvent(float unit,FishingEventKind previous){
            float total=0;for(int i=1;i<=4;i++)total+=EventWeight((FishingEventKind)i,previous);
            if(total<=0)return FishingEventKind.None;float cursor=Mathf.Clamp01(unit)*total;var fallback=FishingEventKind.None;
            for(int i=1;i<=4;i++){var kind=(FishingEventKind)i;float weight=EventWeight(kind,previous);if(weight<=0)continue;fallback=kind;cursor-=weight;if(cursor<=0)return kind;}return fallback;
        }
        void PositionFlock(float t){
            var p=center+right*Mathf.Lerp(-30,30,t)+forward*(Mathf.Sin(t*Mathf.PI)*7);p.y=module.SurfaceHeight+flockHeight;
            var tangent=(right*60+forward*(Mathf.Cos(t*Mathf.PI)*Mathf.PI*7)).normalized;
            for(int i=0;i<flock.Length;i++)if(flock[i]){int rank=(i+1)/2;float side=i==0?0:(i%2==0?1:-1);var offset=right*(side*rank*1.3f)-forward*(rank*1.1f)+Vector3.up*Mathf.Sin(Time.time*.9f+i)*.45f;flock[i].transform.SetPositionAndRotation(p+offset,Quaternion.LookRotation(tangent));}
            calls.transform.position=p;
        }
        bool Begin(FishingEventKind kind){
            if(!module||!module.rig||!module.rig.view)return false;
            Prepare();HideVisuals();Current=kind;bool isWeather=kind==FishingEventKind.RainSquall||kind==FishingEventKind.SeaMist;activeDuration=isWeather?Random.Range(Mathf.Max(20,weatherDurationMin),Mathf.Max(Mathf.Max(20,weatherDurationMin),weatherDurationMax)):Mathf.Max(5,eventDuration);endsAt=Time.time+activeDuration;firstHorsePending=kind==FishingEventKind.SeahorseTide;
            forward=Vector3.ProjectOnPlane(module.waterTarget?module.waterTarget.position-module.rig.view.transform.position:module.rig.view.transform.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.1f)forward=Vector3.forward;right=Vector3.Cross(Vector3.up,forward);
            center=module.rig.view.transform.position+forward*16;center.y=module.SurfaceHeight;
            if(!module.IsWaterAt(center,out _)&&module.waterTarget){center=module.waterTarget.position;center.y=module.SurfaceHeight;}
            if(kind==FishingEventKind.SeagullRun){PositionFlock(0);foreach(var bird in flock)if(bird)bird.gameObject.SetActive(true);}
            else if(kind==FishingEventKind.SeahorseTide){foreach(var line in ripples)if(line)line.enabled=true;}
            module.rig.Notify(Title+"！"+Description);return true;
        }
        void EndEvent(){previousEvent=Current;HideVisuals();Current=FishingEventKind.None;firstHorsePending=false;ScheduleNext(intervalMin,intervalMax);if(module&&module.rig)module.rig.Notify("事件结束 · 海面恢复平静");}
        void HideVisuals(){if(flock!=null)foreach(var bird in flock)if(bird)bird.gameObject.SetActive(false);if(ripples!=null)foreach(var line in ripples)if(line)line.enabled=false;if(calls)calls.Stop();if(weather)weather.Clear();}
        public void SuspendEvents(){if(Current!=FishingEventKind.None)EndEvent();}
        public void ApplyReplicaState(FishingEventKind kind,float remaining,float duration){
            automatic=false;if(kind==FishingEventKind.None){if(Current!=FishingEventKind.None)EndEvent();return;}
            if(Current!=kind)Begin(kind);activeDuration=Mathf.Max(1,duration);endsAt=Time.time+remaining;
        }
        public FishDefinition PickCatch(float poolUnit,float eventUnit)=>PickCatch(module,poolUnit,eventUnit);
        public FishDefinition PickCatch(FishingModule actor,float poolUnit,float eventUnit){
            int vertices=actor.AvailableCatchVertices,triangles=actor.AvailableCatchTriangles;
            if(Current==FishingEventKind.SeahorseTide&&seaHorse&&(firstHorsePending||eventUnit<seaHorseChance)){if(FishingCatchPool.Fits(seaHorse,vertices,triangles))return seaHorse;if(firstHorsePending)return null;}
            return FishingCatchPool.PickForCast(actor.profile,actor.BaitInfo,poolUnit,actor.CastDepth,actor.CastDistance,RareMultiplier,Current==FishingEventKind.SeaMist,vertices,triangles);
        }
        public void CatchSpawned(FishDefinition fish){if(Current==FishingEventKind.SeahorseTide&&fish==seaHorse)firstHorsePending=false;}
        public void RevealHorse(FishingCatch fish){if(module.rig){module.rig.Notify("等等……这只海马，真的是一匹马！");module.rig.ActiveHand.Pulse(.6f,.12f);}}
        void OnDisable(){HideVisuals();Current=FishingEventKind.None;firstHorsePending=false;}
    }
}
