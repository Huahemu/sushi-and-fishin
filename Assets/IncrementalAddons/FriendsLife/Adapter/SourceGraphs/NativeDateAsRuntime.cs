using System;
using System.Collections.Generic;
using System.Linq;
using TomodachiLife;
using TomodachiLife.Simulation;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [Serializable] public sealed class NativeDateBodyFrame
    {
        public int sourceNode;
        public string animation;
        public float duration,weight;
        public NativeAsDummyFrameState frame;
    }
    [Serializable] public sealed class NativeDatePreviousBodyFrame
    { public string animation;public float normalizedFrame; }
    [Serializable] public sealed class NativeDateAsState
    {
        public int schema=1;
        public string executionKey;
        public bool active;
        public int selectedChild=-1,bodyNode,expressionCode;
        public float rawHeight,rawBuild;
        public long ticks,faceTransitions;
        public float lastDeltaSourceFrames;
        public bool disableStateAnimation=true,disableHead;
        public int faceChild;
        public NativeAsDummyFrameState face;
        public List<NativeDateBodyFrame> body=new List<NativeDateBodyFrame>();
    }
    [Serializable] public sealed class NativeDateActorAsState
    {
        public int schema=1;
        public NativeAsRandomHistory history=new NativeAsRandomHistory();
        public List<NativeDatePreviousBodyFrame> previous=new List<NativeDatePreviousBodyFrame>();
        public NativeDateAsState date;
    }
    // A bounded implementation of the actual33-node Date command. These
    // records are one host actor's AS instance, independent of a resource
    // lease's visualElapsed. They are serialized inside the existing native
    // actor context. The host supplies its visual frame delta explicitly.
    public static class NativeDateAsRuntime
    {
        public const string Command="AI_TelopChat_RelaxDate_SitFloor";
        public const string Source="Mii.root.asb16738/33-node closure; BAEV16710/16749/16741/16764/16766; recovered selector/frame kernels";
        static readonly int[] rootChildren={16746,16747,16748,16740,16742,16744};
        static readonly int[] bodyRoots={127,13630,13609,16739,13246,13612};
        static readonly float[] durations={48,60,96,96,48,120};
        static readonly float[] weights={1,1,1,1,1,1};
        public static void Validate(NativeDateActorAsState actor)
        {
            if(actor==null||actor.schema!=1||actor.history==null||actor.history.nodes==null||actor.history.children==null||actor.history.nodes.Length!=4||actor.history.children.Length!=4||actor.previous==null)
                throw new OriginalBindingException("Saved actor Date AS instance is incomplete or belongs to another schema");
            var state=actor.date;if(state==null)return;
            if(state.schema!=1||state.selectedChild<0||state.selectedChild>=6||state.bodyNode!=bodyRoots[state.selectedChild]||string.IsNullOrEmpty(state.executionKey)||state.body==null||state.body.Count==0||state.body.Any(b=>b==null||b.frame?.words==null||b.frame.words.Length!=19||b.duration!=durations[state.selectedChild])||state.faceChild<0||state.faceChild>1||state.selectedChild>=3&&(state.face?.words==null||state.face.words.Length!=19))
                throw new OriginalBindingException("Saved Date AS node/frame ownership is incomplete");
        }
        public static NativeStateGraphParameters Parameters(float rawHeight,float rawBuild)
        {
            if(float.IsNaN(rawHeight)||float.IsInfinity(rawHeight)||float.IsNaN(rawBuild)||float.IsInfinity(rawBuild)||rawHeight<0||rawHeight>127||rawBuild<0||rawBuild>127)
                throw new OriginalBindingException("Date AS needs the actual source raw BodyHeight/BodyBuild0..127");
            var values=new NativeStateGraphParameters();values.SetFloat("BodyHeight",rawHeight);values.SetFloat("BodyBuild",rawBuild);return values;
        }
        public static NativeDateAsState Begin(NativeDateActorAsState actor,string executionKey,float rawHeight,float rawBuild,NativeAnimationStateGraph graph,ref NativeMiiEventRandomState random)
        {
            Validate(actor);if(string.IsNullOrEmpty(executionKey)||graph?.nodes==null)throw new OriginalBindingException("Date AS has no actual execution key/source graph");
            if(actor.date?.active==true)
            {
                if(actor.date.executionKey!=executionKey)throw new OriginalBindingException("A different Date still owns this actor AS instance");
                return actor.date;
            }
            if(actor.date?.executionKey==executionKey)throw new OriginalBindingException("An ended Date cannot be reentered with the same execution key");
            var root=graph.nodes.FirstOrDefault(n=>n.index==16738);
            if(root?.type!="RandomSelector"||root.children==null||!root.children.Select(c=>c.node).SequenceEqual(rootChildren)||root.children.Any(c=>c.value?.source!="constant"||c.value.number!=1)||root.Property("historyCount")?.integer!=1)
                throw new OriginalBindingException("Installed Date AS differs from its recovered authored command");
            // Validate parameters and every possible body path before the one
            // original global RNG draw. Binding failure never redraws a pose.
            var parameters=Parameters(rawHeight,rawBuild);
            var selections=new NativeAnimationSelection[6];
            for(int i=0;i<6;i++)if(!new NativeAnimationStateGraphEvaluator(graph).TrySelectNode(bodyRoots[i],parameters,out selections[i])||selections[i].clips.Count==0)
                throw new OriginalBindingException("Current source Date body path cannot be selected: "+bodyRoots[i]);
            int selected=NativeAsRandomSelector.Select(16738,weights,1,actor.history,ref random,actor.date?.selectedChild??-1,out _);
            var state=new NativeDateAsState{executionKey=executionKey,active=true,selectedChild=selected,bodyNode=bodyRoots[selected],rawHeight=rawHeight,rawBuild=rawBuild,disableHead=selected>=3,expressionCode=selected<3?23:26};
            foreach(var clip in selections[selected].clips.Where(c=>c.weight>0))
            {
                float initial=0;uint frameFlags=0,nodeFlags=0x4000;
                if(selected==1||selected==5)
                {
                    // Names identify the actual resource animation within this
                    // bounded Date subset; no transient host instance IDs.
                    var previous=actor.previous.Select(p=>new NativeAsPreviousSkeletalFrame{animationId=p.animation==clip.animation?1:0,normalizedFrame=p.normalizedFrame}).ToArray();
                    initial=NativeAsInitialFrame.SameAnimation(0,durations[selected],durations[selected],1,previous,ref frameFlags,ref nodeFlags);
                }
                var frame=NativeAsLoopFrame.Begin(durations[selected],selected==1||selected==5?13628:-1,initial);frame.words[0]|=frameFlags;frame.nodeFlags=nodeFlags;
                state.body.Add(new NativeDateBodyFrame{sourceNode=clip.node,animation=clip.animation,duration=durations[selected],weight=clip.weight,frame=frame});
            }
            // This implementation owns its actual empty Date Dummy pool. The
            // first free Dummy slot is0 (00a94b4c); GetFrameInfo00a95660 writes
            // that0 to the slot marker, so the controller takes its global
            // RNG branch. Restore retains the record and never calls Begin.
            if(selected>=3)state.face=NativeAsDummyFrame.Begin(13383,20,20,0,ref random);
            actor.date=state;return state;
        }
        public static void Tick(NativeDateAsState state,float deltaSourceFrames,ref NativeMiiEventRandomState random)
        {
            if(state==null||!state.active)return;
            if(deltaSourceFrames<0||float.IsNaN(deltaSourceFrames)||float.IsInfinity(deltaSourceFrames))throw new OriginalBindingException("Date AS visual delta is invalid");
            state.lastDeltaSourceFrames=deltaSourceFrames;state.ticks++;
            foreach(var body in state.body)NativeAsLoopFrame.Tick(body.frame,deltaSourceFrames);
            if(state.selectedChild<3)return;
            TickFace(state,deltaSourceFrames,ref random,3);
        }
        static void TickFace(NativeDateAsState state,float delta,ref NativeMiiEventRandomState random,int depth)
        {
            float raw=NativeAsDummyFrame.Tick(state.face,delta);
            if(!state.face.Complete)return;
            // Normal Seq0002ce80: selectNextFrame=false, parent notseeking,
            // completed child ->0002da80; context+34 is the child's raw frame
            // output. The source computes this exact subtraction before its
            // bounded3 recursive ticks; it is not elapsed clip duration.
            float nextDelta=raw>0?(float)(delta-raw):0;
            state.faceChild=1-state.faceChild;state.faceTransitions++;
            state.expressionCode=state.faceChild==0?26:23;
            state.face=state.faceChild==0?NativeAsDummyFrame.Begin(13383,20,20,0,ref random,raw>0?raw:delta):NativeAsDummyFrame.Begin(13385,10,10,0,ref random,raw>0?raw:delta);
            if(nextDelta>0&&depth>0)TickFace(state,nextDelta,ref random,depth-1);
        }
        public static void End(NativeDateActorAsState actor,string executionKey)
        {
            Validate(actor);var state=actor.date;if(state==null||!state.active||state.executionKey!=executionKey)return;
            actor.previous=state.body.Select(b=>new NativeDatePreviousBodyFrame{animation=b.animation,normalizedFrame=(float)(b.frame.Frame/b.duration)}).ToList();state.active=false;
        }
        // Client body prediction is purely visual. In particular it never
        // advances a Dummy controller or the shared source global random.
        public static void PredictBody(NativeDateAsState state,float deltaSourceFrames)
        {if(state?.active==true)foreach(var body in state.body)NativeAsLoopFrame.Tick(body.frame,deltaSourceFrames);}
    }
}
