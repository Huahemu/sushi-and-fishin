using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // An actual camera/render target owned by one source episode. The player
    // camera, its Transform, InputActions and full-screen UI are never written.
    public sealed class C004NativeEpisodeCameras:MonoBehaviour
    {
        const string BagName="C004NativeCamera/v1";
        [Serializable] sealed class ViewData
        {
            public int schema=1;
            public string worldId,episodeId;
            public float fovy;
            public bool hasPose;
            public C004NativeCameraPoseData pose;
        }
        sealed class Session
        {
            public OriginalGraphRunner runner;public SourceEpisode episode;public OriginalBagState bag;public ViewData data;
            public GameObject root;public Camera camera;public RenderTexture texture;public bool observed;
        }
        readonly Dictionary<OriginalGraphRunner,Session> sessions=new Dictionary<OriginalGraphRunner,Session>();
        Func<FriendsLifeRuntime> actualRuntime;
        public string LastProjectionError{get;private set;}
        public void Initialize(Func<FriendsLifeRuntime> runtime)=>actualRuntime=runtime;
        public static string Token(string episodeId)=>"@source-camera/"+episodeId;
        static void Release(UnityEngine.Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        FriendsLifeRuntime Actual(SourceEpisode episode)
        {
            var runtime=actualRuntime?.Invoke();
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.State.episodes.Any(e=>ReferenceEquals(e,episode))||episode.committed)
                throw new OriginalBindingException("The actual authoritative source episode camera owner is unavailable");
            return runtime;
        }
        public void Register(OriginalGraphRunner runner,SourceEpisode episode,OriginalDemoDefinition definition)
        {
            if(sessions.ContainsKey(runner))return;var runtime=Actual(episode);
            if(definition==null)throw new OriginalBindingException("Exact source DemoParam camera/lens owner is absent");
            string id=runner.State.episodeId+"/"+BagName;var records=runner.State.bags.Where(b=>b.id==id).ToArray();if(records.Length>1)throw new OriginalBindingException("Ambiguous saved source camera owner");
            var rawDefinition=JObject.Parse(definition.rawJson);var rawFovy=rawDefinition["FovyDeg"];
            if(rawFovy==null||!float.TryParse(rawFovy.ToString(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var fovy)||float.IsNaN(fovy)||float.IsInfinity(fovy)||fovy<=0||fovy>=180)
                throw new OriginalBindingException("Exact finite authored source FovyDeg is unavailable");
            OriginalBagState bag;ViewData data;
            if(records.Length==0){bag=runner.Bag(runner.NewBag(BagName));data=new ViewData{worldId=runtime.State.worldId,episodeId=episode.episodeId,fovy=fovy};}
            else
            {
                bag=records[0];var raw=runner.Get(bag.values,"payload");if(raw==null||raw.kind!="String")throw new OriginalBindingException("Saved source camera payload is absent");
                var json=JObject.Parse(raw.text);foreach(string field in new[]{"schema","worldId","episodeId","fovy","hasPose"})if(json[field]==null)throw new OriginalBindingException("Saved source camera field absent: "+field);
                if(json["schema"].Type!=JTokenType.Integer||json["worldId"].Type!=JTokenType.String||json["episodeId"].Type!=JTokenType.String||json["hasPose"].Type!=JTokenType.Boolean||(json["fovy"].Type!=JTokenType.Float&&json["fovy"].Type!=JTokenType.Integer))throw new OriginalBindingException("Saved source camera field types are invalid");
                data=JsonUtility.FromJson<ViewData>(raw.text);
                if(data==null||data.schema!=1||data.worldId!=runtime.State.worldId||data.episodeId!=episode.episodeId||data.fovy!=fovy||data.hasPose&&data.pose==null)
                    throw new OriginalBindingException("Saved source camera belongs to a different world/episode/lens");
                if(data.hasPose)ValidatePoseJson(json["pose"],data.worldId,data.episodeId,null,null);
            }
            var session=new Session{runner=runner,episode=episode,bag=bag,data=data};sessions.Add(runner,session);Persist(session);
            if(data.hasPose)Apply(session,data.pose);
        }
        Session Require(OriginalGraphRunner runner)
        {
            if(!sessions.TryGetValue(runner,out var session))throw new OriginalBindingException("Source episode camera has not bound its exact DemoParam owner");
            Actual(session.episode);return session;
        }
        void EnsureProjection(Session session)
        {
            if(session.camera&&session.texture)return;
            var root=new GameObject("Original source camera "+session.episode.episodeId){hideFlags=HideFlags.DontSave};root.transform.SetParent(transform,false);
            var camera=root.AddComponent<Camera>();var player=Camera.main;
            if(player)camera.CopyFrom(player);
            camera.enabled=false;camera.tag="Untagged";camera.fieldOfView=session.data.fovy;
            float aspect=player?player.aspect:(Screen.height>0?(float)Screen.width/Screen.height:0);
            if(float.IsNaN(aspect)||float.IsInfinity(aspect)||aspect<=0){Release(root);throw new OriginalBindingException("Actual host output aspect for source camera is unavailable");}
            camera.aspect=aspect;camera.rect=new Rect(0,0,1,1);
            int width=512,height=Math.Max(1,Mathf.RoundToInt(width/aspect));
            var texture=new RenderTexture(width,height,16,RenderTextureFormat.ARGB32){name="Owned source camera "+session.episode.episodeId,hideFlags=HideFlags.DontSave};
            if(!texture.Create()){Release(texture);Release(root);throw new OriginalBindingException("Actual owned source camera render target allocation failed");}
            camera.targetTexture=texture;session.root=root;session.camera=camera;session.texture=texture;
        }
        static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y)&&!float.IsInfinity(v.z);
        static void ValidatePoseJson(JToken json,string world,string episode,string execution,string address)
        {
            if(!(json is JObject))throw new OriginalBindingException("Raw source camera pose is absent");
            foreach(string field in new[]{"schema","worldId","episodeId","executionKey","sourceAddress","phase","onlyOnEnter","rawPosition","rawInputLookAt","rawLookAt","up"})
                if(json[field]==null)throw new OriginalBindingException("Raw source camera field absent: "+field);
            if(json["schema"].Type!=JTokenType.Integer||(int)json["schema"]!=1||json["onlyOnEnter"].Type!=JTokenType.Boolean)
                throw new OriginalBindingException("Invalid source camera schema/property type");
            foreach(string field in new[]{"worldId","episodeId","executionKey","sourceAddress","phase"})if(json[field].Type!=JTokenType.String)throw new OriginalBindingException("Invalid source camera string field: "+field);
            if((string)json["worldId"]!=world||(string)json["episodeId"]!=episode||string.IsNullOrEmpty((string)json["executionKey"])||string.IsNullOrEmpty((string)json["sourceAddress"])||
               execution!=null&&(string)json["executionKey"]!=execution||address!=null&&(string)json["sourceAddress"]!=address||((string)json["phase"]!="Captured"&&(string)json["phase"]!="Applied"))
                throw new OriginalBindingException("Foreign/unknown source camera pose owner or phase");
            foreach(string field in new[]{"rawPosition","rawInputLookAt","rawLookAt"})
                foreach(string component in new[]{"x","y","z"})
                {
                    var value=json[field]?[component];if(value?.Type!=JTokenType.Integer||(long)value<0||(long)value>uint.MaxValue)
                        throw new OriginalBindingException("Missing/invalid source camera raw vector word: "+field+"."+component);
                }
            var pose=JsonUtility.FromJson<C004NativeCameraPoseData>(json.ToString());var exact=C004NativeCameraPoseBody.AdjustRaw(pose.rawPosition,pose.rawInputLookAt);
            if(exact.x!=pose.rawLookAt.x||exact.y!=pose.rawLookAt.y||exact.z!=pose.rawLookAt.z||pose.up.x!=0||pose.up.y!=1||pose.up.z!=0)
                throw new OriginalBindingException("Saved source camera output/up does not match its actual native body");
        }
        void Apply(Session session,C004NativeCameraPoseData pose)
        {
            if(pose==null||pose.schema!=1||pose.worldId!=session.data.worldId||pose.episodeId!=session.episode.episodeId)throw new OriginalBindingException("Foreign source camera pose capture");
            if(pose.rawPosition==null||pose.rawInputLookAt==null||pose.rawLookAt==null)throw new OriginalBindingException("Raw source camera vector words are absent");
            var position=pose.rawPosition.ToVector();var lookAt=pose.rawLookAt.ToVector();
            if(!Finite(position)||!Finite(lookAt)||!Finite(pose.up))throw new OriginalBindingException("Raw source camera pose is outside the finite host renderer domain");
            Vector3 direction=lookAt-position;
            if(direction.sqrMagnitude<=0||Vector3.Cross(direction,pose.up).sqrMagnitude<=0)throw new OriginalBindingException("Source camera look/up basis has no supported actual host rotation");
            EnsureProjection(session);session.camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(direction,pose.up));
        }
        public OriginalNodeStatus SetPose(OriginalNodeContext context,SourceEpisode episode)
        {
            var runtime=Actual(episode);if(!runtime.IsWorldRunning||episode.status!="SourceRunning")throw new OriginalBindingException(context.Address+": source camera action is not currently running");
            var session=Require(context.Runner);const string key="native-camera-pose/v1";var saved=context.Read(key);C004NativeCameraPoseData pose;
            if(saved==null)
            {
                if(session.data.hasPose&&session.data.pose?.executionKey==context.ExecutionKey)throw new OriginalBindingException(context.Address+": declared applied camera activation lost its raw capture");
                var property=context.Property("Prop_OnlyOnEnter");if(property.kind!="Bool")throw new OriginalBindingException(context.Address+": original OnlyOnEnter bool is absent");
                var position=context.Input("CameraPosition");var lookAt=context.Input("LookAtPosition");if(position.kind!="Vector"||lookAt.kind!="Vector")throw new OriginalBindingException(context.Address+": exact original camera vector inputs required");
                var rawPosition=C004NativeCameraVector.FromVector(position.vector);var rawLook=C004NativeCameraVector.FromVector(lookAt.vector);var adjusted=C004NativeCameraPoseBody.AdjustRaw(rawPosition,rawLook);
                pose=new C004NativeCameraPoseData{worldId=runtime.State.worldId,episodeId=episode.episodeId,executionKey=context.ExecutionKey,sourceAddress=context.Address,position=position.vector,inputLookAt=lookAt.vector,lookAt=adjusted.ToVector(),rawPosition=rawPosition,rawInputLookAt=rawLook,rawLookAt=adjusted,up=Vector3.up,onlyOnEnter=property.boolean};
                context.Write(key,OriginalValue.String(JsonUtility.ToJson(pose)));
            }
            else
            {
                if(saved.kind!="String")throw new OriginalBindingException(context.Address+": raw source camera capture is not a string");ValidatePoseJson(JObject.Parse(saved.text),runtime.State.worldId,episode.episodeId,context.ExecutionKey,context.Address);pose=JsonUtility.FromJson<C004NativeCameraPoseData>(saved.text);
                if(pose==null||pose.executionKey!=context.ExecutionKey||pose.worldId!=runtime.State.worldId||pose.episodeId!=episode.episodeId)throw new OriginalBindingException(context.Address+": saved source camera activation is foreign");
            }
            if(!pose.onlyOnEnter)throw new OriginalBindingException(context.Address+": continuous camera update lifetime requires its exact active-node binding");
            Apply(session,pose);pose.phase="Applied";session.data.pose=pose;session.data.hasPose=true;
            context.Write(key,OriginalValue.String(JsonUtility.ToJson(pose)));Persist(session);return OriginalNodeStatus.Success;
        }
        void Persist(Session session)=>session.runner.Set(session.bag.values,"payload",OriginalValue.String(JsonUtility.ToJson(session.data)));
        public Camera CameraOf(OriginalGraphRunner runner){var session=Require(runner);EnsureProjection(session);return session.camera;}
        public Vector3 PositionOf(OriginalGraphRunner runner){var session=Require(runner);if(!session.data.hasPose)throw new OriginalBindingException("Source camera position has not been produced");return session.data.pose.rawPosition.ToVector();}
        public Vector3 LookAtOf(OriginalGraphRunner runner){var session=Require(runner);if(!session.data.hasPose)throw new OriginalBindingException("Source camera look target has not been produced");return session.data.pose.rawLookAt.ToVector();}
        public Transform Resolve(string token)
        {
            var session=sessions.Values.SingleOrDefault(s=>Token(s.episode.episodeId)==token);if(session==null)throw new OriginalBindingException("Source camera token has no current episode owner: "+token);Actual(session.episode);EnsureProjection(session);return session.camera.transform;
        }
        public bool Observe(string episodeId,bool observed,out RenderTexture texture)
        {
            texture=null;var session=sessions.Values.SingleOrDefault(s=>s.episode.episodeId==episodeId);if(session==null)return false;Actual(session.episode);EnsureProjection(session);session.observed=observed;texture=session.texture;return true;
        }
        void LateUpdate()
        {
            var runtime=actualRuntime?.Invoke();
            foreach(var entry in sessions.ToArray())
            {
                var session=entry.Value;
                if(!runtime||!runtime.Ready||!runtime.State.episodes.Any(e=>ReferenceEquals(e,session.episode))||session.episode.committed)
                {Dispose(session);sessions.Remove(entry.Key);continue;}
                if(!session.observed||session.episode.status!="SourceRunning"||!session.data.hasPose)continue;
                try{Apply(session,session.data.pose);session.camera.Render();LastProjectionError=null;}
                catch(Exception error){LastProjectionError=error.Message;session.observed=false;}
            }
        }
        static void Dispose(Session session){if(session.texture){session.texture.Release();Release(session.texture);}Release(session.root);}
        void OnDestroy(){foreach(var session in sessions.Values)Dispose(session);sessions.Clear();}
    }
}
