using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [Serializable] public sealed class C004NativeSourceFrame
    {
        public int schema=1,scaledStep,unscaledStep,previousScaledStep;
        public long sequence,cpuFrequency,gpuFrequency,cpuWorkTicks,gpuWorkTicks,adaptiveWorkTicks;
        public string worldId,frameStartHex,cpuPresentHex,gpuCompleteHex,inputOwner;
    }
    // A real platform measurement input port around the copied original
    // adaptive producer. FrameTiming timestamps stay in their OWN CPU domain;
    // Stopwatch is used only for elapsed source-present pacing, never compared
    // with those absolute timestamps. No Time.deltaTime*60 creates an int step.
    [DefaultExecutionOrder(-90)] public sealed class C004NativeFrameProducer:MonoBehaviour
    {
        public const string BagName="C004NativeFrameClock/v1";
        [Serializable] sealed class Data
        {
            public int schema=1;public string worldId;
            public long sequence;
            public C004NativeFrameIntervalState adaptive;
            public C004NativeSourceFrame frame;
        }
        sealed class Session{public Data data;public long lastPace;public ulong lastMeasurement;public bool measurementBaseline;public int publishedUnityFrame=-1;public string error;}
        readonly ConditionalWeakTable<FriendsLifeSave,Session> sessions=new ConditionalWeakTable<FriendsLifeSave,Session>();
        readonly FrameTiming[] timings=new FrameTiming[1];
        Func<FriendsLifeRuntime> runtime;
        public string LastError{get;private set;}
        public void Initialize(Func<FriendsLifeRuntime> actual)=>runtime=actual;
        FriendsLifeRuntime Actual()
        {
            var actual=runtime?.Invoke();if(!actual)actual=FriendsLifeRuntime.Instance;
            return actual&&actual.Ready&&actual.IsAuthority?actual:null;
        }
        static void RequireType(JToken parent,string name,JTokenType kind)
        {if(parent?[name]?.Type!=kind)throw new OriginalBindingException("Native frame-clock field absent/invalid: "+name);}
        static bool RawStamp(string value)=>value!=null&&value.Length==16&&ulong.TryParse(value,NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out ulong parsed)&&parsed!=0;
        public static void ValidatePublishedFrame(C004NativeSourceFrame frame)
        {
            if(frame==null||frame.schema!=1||string.IsNullOrEmpty(frame.worldId)||frame.sequence<1||frame.scaledStep<2||frame.scaledStep>3||frame.unscaledStep!=frame.scaledStep||frame.previousScaledStep!=0&&frame.previousScaledStep!=2&&frame.previousScaledStep!=3||frame.cpuFrequency<=0||frame.gpuFrequency<=0||!RawStamp(frame.frameStartHex)||!RawStamp(frame.cpuPresentHex)||!RawStamp(frame.gpuCompleteHex)||string.IsNullOrEmpty(frame.inputOwner)||frame.adaptiveWorkTicks!=Math.Max(frame.cpuWorkTicks,frame.gpuWorkTicks))
                throw new OriginalBindingException("Malformed actual native frame input; no zero/default or foreign timing fields permitted");
        }
        static Data Decode(string raw,string world)
        {
            var json=JObject.Parse(raw);RequireType(json,"schema",JTokenType.Integer);RequireType(json,"worldId",JTokenType.String);RequireType(json,"sequence",JTokenType.Integer);RequireType(json,"adaptive",JTokenType.Object);
            if((int)json["schema"]!=1||(string)json["worldId"]!=world||(long)json["sequence"]<0)throw new OriginalBindingException("Foreign/invalid native frame-clock owner");
            foreach(string field in new[]{"schema","minInterval","maxInterval","increaseDelay","decreaseDelay","rawBiasF32","minFps","maxFps","fpsBoost","currentInterval","preferredInterval","increaseCounter","decreaseCounter","currentFps","presentationFps"})
            {RequireType(json["adaptive"],field,JTokenType.Integer);long value=(long)json["adaptive"][field];if(field=="rawBiasF32"?value<0||value>uint.MaxValue:value<int.MinValue||value>int.MaxValue)throw new OriginalBindingException("Original adaptive integer width invalid: "+field);}
            RequireType(json["adaptive"],"adaptiveFrozen",JTokenType.Boolean);RequireType(json["adaptive"],"supportedPresentationFps",JTokenType.Array);
            var data=JsonUtility.FromJson<Data>(raw);C004NativeFrameIntervalBody.Validate(data.adaptive);var adaptive=data.adaptive;
            // This bridge declares only the released ordinary nnMain config,
            // with no native time-modifier or optional interval listener owner.
            if(adaptive.minInterval!=2||adaptive.maxInterval!=3||adaptive.increaseDelay!=2||adaptive.decreaseDelay!=6||adaptive.rawBiasF32!=0x3d4ccccd||adaptive.minFps!=30||adaptive.maxFps!=60||adaptive.fpsBoost!=10||adaptive.currentInterval<2||adaptive.currentInterval>3||adaptive.preferredInterval<2||adaptive.preferredInterval>3||adaptive.increaseCounter<0||adaptive.increaseCounter>2||adaptive.decreaseCounter<0||adaptive.decreaseCounter>6||adaptive.adaptiveFrozen||adaptive.supportedPresentationFps.Length!=0)
                throw new OriginalBindingException("Native frame-clock continuation is outside the sourced ordinary configuration domain");
            if(data.sequence==0)
            {
                if(data.frame!=null)throw new OriginalBindingException("Unpublished native frame-clock has a frame payload");
            }
            else
            {
                RequireType(json,"frame",JTokenType.Object);
                foreach(string field in new[]{"schema","scaledStep","unscaledStep","previousScaledStep","sequence","cpuFrequency","gpuFrequency","cpuWorkTicks","gpuWorkTicks","adaptiveWorkTicks"})RequireType(json["frame"],field,JTokenType.Integer);
                foreach(string field in new[]{"worldId","frameStartHex","cpuPresentHex","gpuCompleteHex","inputOwner"})RequireType(json["frame"],field,JTokenType.String);
                ValidatePublishedFrame(data.frame);
                if(data.frame.worldId!=world||data.frame.sequence!=data.sequence||data.frame.scaledStep!=adaptive.currentInterval)throw new OriginalBindingException("Native frame-clock published input is foreign/inconsistent");
            }
            return data;
        }
        public static void ValidateRunnerClockSnapshot(OriginalGraphRunState state,long sequence)
        {
            if(state==null||sequence<0)throw new OriginalBindingException("Invalid native clock snapshot owner");
            foreach(var frame in state.frames)
            {
                var scope=state.scopes.Single(s=>s.id==frame.scope);
                foreach(var slot in frame.values)
                {
                    long consumed=-1;
                    if(slot.key=="native-WaitSec/v1")
                    {
                        if(slot.value?.kind!="String")throw new OriginalBindingException("Saved native WaitSec capture kind invalid");
                        var raw=JObject.Parse(slot.value.text);RequireType(raw,"lastFrame",JTokenType.Integer);consumed=(long)raw["lastFrame"];
                    }
                    else if(slot.key=="native-WaitBgm-frame-sequence/v1")
                    {
                        if(slot.value?.kind!="String"||!long.TryParse(slot.value.text,NumberStyles.Integer,CultureInfo.InvariantCulture,out consumed)||consumed<1)throw new OriginalBindingException("Saved original BGM publication sequence invalid");
                    }
                    if(consumed< -1||consumed>sequence)throw new OriginalBindingException("Source consumer sequence exceeds same-snapshot native clock publication");
                    if(consumed>=1&&!frame.complete)
                    {
                        var flag=scope.values.SingleOrDefault(v=>v.key=="native-node-updated/v1/"+frame.node)?.value;
                        if(flag?.kind!="Bool"||!flag.boolean)throw new OriginalBindingException("Saved original updated-node flag absent/inconsistent after consumer update");
                    }
                }
            }
        }
        Session For(FriendsLifeRuntime actual)
        {
            if(sessions.TryGetValue(actual.State,out var existing))return existing;
            Data newest=null;
            foreach(var episode in actual.State.episodes.Where(e=>!string.IsNullOrEmpty(e.runnerJson)))
            {
                var state=JObject.Parse(episode.runnerJson);var bags=state["bags"]as JArray;if(bags==null)continue;
                foreach(var bag in bags.Where(x=>(string)x["id"]==episode.episodeId+"/"+BagName))
                {
                    var values=bag["values"]as JArray;var payload=values?.SingleOrDefault(x=>(string)x["key"]=="payload")?["value"];
                    if((string)payload?["kind"]!="String"||payload?["text"]?.Type!=JTokenType.String)throw new OriginalBindingException("Declared native clock bag has no raw string payload");
                    var candidate=Decode((string)payload["text"],actual.State.worldId);
                    if(newest==null||candidate.sequence>newest.sequence)newest=candidate;
                    else if(candidate.sequence==newest.sequence&&!JToken.DeepEquals(JObject.Parse(JsonUtility.ToJson(candidate)),JObject.Parse(JsonUtility.ToJson(newest))))
                        throw new OriginalBindingException("Conflicting same-sequence native frame-clock snapshots");
                }
            }
            if(newest==null)
            {
                // Actual nnMain00903690 calls00903a74 with min2/max3,
                // which initializes current/preferred2. It is not a guessed1.
                var adaptive=C004NativeFrameIntervalBody.Constructor();C004NativeFrameIntervalBody.InitializeOriginalDefaultConfiguration(adaptive);
                adaptive.minInterval=2;adaptive.maxInterval=3;adaptive.currentInterval=2;adaptive.preferredInterval=2;
                newest=new Data{worldId=actual.State.worldId,adaptive=adaptive};
            }
            foreach(var episode in actual.State.episodes.Where(e=>!string.IsNullOrEmpty(e.runnerJson)))ValidateRunnerClockSnapshot(JsonUtility.FromJson<OriginalGraphRunState>(episode.runnerJson),newest.sequence);
            var session=new Session{data=newest,lastPace=Stopwatch.GetTimestamp()};sessions.Add(actual.State,session);return session;
        }
        void Update()
        {
            FrameTimingManager.CaptureFrameTimings();var actual=Actual();if(!actual)return;Session session=null;
            try
            {
                session=For(actual);if(session.publishedUnityFrame==Time.frameCount)return;session.error=null;
                if(!FrameTimingManager.IsFeatureEnabled())throw new OriginalBindingException("Actual GPU/CPU frame timing feature is unavailable; no zero/default native step input is permitted");
                ulong cpu=FrameTimingManager.GetCpuTimerFrequency(),gpu=FrameTimingManager.GetGpuTimerFrequency();
                if(cpu==0||cpu>long.MaxValue||gpu==0||gpu>long.MaxValue)throw new OriginalBindingException("Actual platform CPU/GPU timing frequency is unavailable");
                if(FrameTimingManager.GetLatestTimings(1,timings)==0)throw new OriginalBindingException("Actual completed correlated CPU/GPU frame record is not ready");
                var timing=timings[0];
                if(timing.frameStartTimestamp==0||timing.cpuTimePresentCalled==0||timing.cpuTimeFrameComplete==0||timing.firstSubmitTimestamp==0||double.IsNaN(timing.gpuFrameTime)||double.IsInfinity(timing.gpuFrameTime)||timing.gpuFrameTime<=0)
                    throw new OriginalBindingException("Actual GPU completion/timestamp input is unavailable; no logical frame substitutes it");
                long now=Stopwatch.GetTimestamp();
                if(!session.measurementBaseline)
                {
                    // Drain the currently latest asynchronous completion on a
                    // fresh/restored world session. It may predate this world
                    // or be the very record captured in its save. No absolute
                    // timestamp is compared across process epochs.
                    session.lastMeasurement=timing.frameStartTimestamp;session.lastPace=now;session.measurementBaseline=true;return;
                }
                if(timing.frameStartTimestamp==session.lastMeasurement)return;
                if(now<session.lastPace)throw new OriginalBindingException("Actual source-present pacing clock regressed");
                int priorInterval=session.data.adaptive.currentInterval;
                if(priorInterval<1||priorInterval>4)throw new OriginalBindingException("Actual source-present interval outside the copied interval-action domain");
                // Presentation-side cadence only. Source logic receives solely
                // the integer result from the actual native adaptive body.
                double period=(double)Stopwatch.Frequency*priorInterval/60.0;
                if(now-session.lastPace<period)return;
                long cpuWork=unchecked((long)(timing.cpuTimePresentCalled-timing.frameStartTimestamp));
                long gpuWork=unchecked((long)(timing.cpuTimeFrameComplete-timing.frameStartTimestamp));
                // Original01d3df40..60 publishes max(CPU+120,GPU+128).
                // A signed negative GPU span is allowed, exactly as original;
                // zero/missing GPU counters were rejected above, not invented.
                long work=cpuWork>gpuWork?cpuWork:gpuWork;
                int interval=C004NativeFrameIntervalBody.Update(session.data.adaptive,work,(long)cpu);
                int previous=session.data.frame?.scaledStep??0;
                session.data.sequence=checked(session.data.sequence+1);
                session.data.frame=new C004NativeSourceFrame{worldId=actual.State.worldId,sequence=session.data.sequence,scaledStep=interval,unscaledStep=interval,previousScaledStep=previous,cpuFrequency=(long)cpu,gpuFrequency=(long)gpu,cpuWorkTicks=cpuWork,gpuWorkTicks=gpuWork,adaptiveWorkTicks=work,frameStartHex=timing.frameStartTimestamp.ToString("x16",CultureInfo.InvariantCulture),cpuPresentHex=timing.cpuTimePresentCalled.ToString("x16",CultureInfo.InvariantCulture),gpuCompleteHex=timing.cpuTimeFrameComplete.ToString("x16",CultureInfo.InvariantCulture),inputOwner="Actual correlated UnityFrameTiming CPU-domain timestamps -> original01d3df60 max -> original0028b2b8 interval -> original005e1be4 ordinary no-time-modifier transfer scaled10/unscaled18"};
                session.lastMeasurement=timing.frameStartTimestamp;session.lastPace=now;session.publishedUnityFrame=Time.frameCount;LastError=null;
            }
            catch(Exception error){LastError=error.Message;if(session!=null)session.error=error.Message;}
        }
        public bool PublishedThisUnityFrame(FriendsLifeRuntime actual,out double sourceSeconds,out string error)
        {
            sourceSeconds=0;error=null;Session session;try{session=For(actual);}catch(Exception failure){error=failure.Message;return false;}
            if(session.error!=null){error=session.error;return false;}
            if(session.publishedUnityFrame!=Time.frameCount)return false;
            if(Time.timeScale!=1f){error="Native source graph time-modifier owner is unavailable for non-unit host scale; no universal pause or active-seconds conversion";return false;}
            sourceSeconds=(float)session.data.frame.scaledStep/60f;return true;
        }
        public C004NativeSourceFrame Read(OriginalGraphRunner runner,SourceEpisode episode)
        {
            var actual=Actual();if(!actual||!actual.State.episodes.Any(e=>ReferenceEquals(e,episode))||episode.status!="SourceRunning"||episode.committed)throw new OriginalBindingException("Actual running source frame consumer owner is unavailable");
            var session=For(actual);if(session.error!=null||session.publishedUnityFrame!=Time.frameCount||session.data.frame==null)throw new OriginalBindingException("No actual native frame was published for this source tick: "+session.error);
            ValidatePublishedFrame(session.data.frame);return session.data.frame;
        }
        public void Capture(IEnumerable<OriginalGraphRunner> runners)
        {
            var actual=Actual();if(!actual)return;var session=For(actual);string payload=JsonUtility.ToJson(session.data);Decode(payload,actual.State.worldId);
            foreach(var runner in runners){ValidateRunnerClockSnapshot(runner.State,session.data.sequence);var bag=runner.Bag(runner.NewBag(BagName));runner.Set(bag.values,"payload",OriginalValue.String(payload));}
        }
    }
}
