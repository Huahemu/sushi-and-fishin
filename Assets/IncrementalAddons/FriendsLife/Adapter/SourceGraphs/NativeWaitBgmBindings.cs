using System;using Newtonsoft.Json.Linq;using UnityEngine;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    public static class NativeWaitBgmBindings
    {
        // Optional external typed input for isolated fixtures. Production reads
        // the actual per-runner native producer and per-scope node flag.
        public static Func<OriginalNodeContext,NativeWaitBgmFrameInput> ActualNativeFrameInput;
        public static void Register(OriginalNodeRegistry registry,Func<FriendsLifeRuntime> runtime)
        {RegisterWithPorts(registry,c=>{var actual=runtime?.Invoke();if(!actual||!actual.Ready||!actual.IsAuthority)throw new OriginalBindingException("Authoritative current host BGM wait owner required");return actual.State.worldId;},ActualFrame,NativeHostBgmTransitions.Observe);}
        static NativeWaitBgmFrameInput ActualFrame(OriginalNodeContext context)
        {
            if(ActualNativeFrameInput!=null)return ActualNativeFrameInput(context);
            var source=context.Runner.NativeFrameInput?.Invoke();if(source==null)return null;
            C004NativeFrameProducer.ValidatePublishedFrame(source);
            string flagKey="native-node-updated/v1/"+context.Node.index;var flag=context.Runner.Get(context.Scope.values,flagKey);
            if(flag!=null&&flag.kind!="Bool")throw new OriginalBindingException("Original generic BGM updated flag is not Bool");
            if(flag==null&&context.Read("native-WaitBgm-frame-sequence/v1")!=null)throw new OriginalBindingException("Persisted original BGM updated flag is absent");
            return new NativeWaitBgmFrameInput{resolved=true,nodeFlagBit1=flag?.boolean??false,actualIntegerFrameStep=source.unscaledStep,source="Original005e1be4 unscaled+18; genericUpdate2ef0 flag-after-body",nativeLifecycle=true,sourceSequence=source.sequence,sourceWorldId=source.worldId};
        }
        public static void RegisterWithPorts(OriginalNodeRegistry registry,Func<OriginalNodeContext,string> actualWorld,Func<OriginalNodeContext,NativeWaitBgmFrameInput> actualFrame,Func<NativeBgmObservation> actualBgm)
        {
            registry.Action("WaitBgmTransitioningFinishAction",(context,seconds)=>
            {
                if(context.Node.inputs.Length!=0||context.Node.properties.Length!=0||context.Node.outputs.Length!=0)throw new OriginalBindingException(context.Address+": original BGM wait signature changed");
                string world=actualWorld(context),key="native-wait-bgm/"+context.ExecutionKey;var saved=context.Read(key);NativeWaitBgmState state;
                if(saved==null)state=NativeWaitBgmOwner.Enter(context.ExecutionKey,world);
                else
                {
                    if(saved.kind!="String")throw new OriginalBindingException("Original BGM continuation is not raw serialized state");var raw=JObject.Parse(saved.text);
                    foreach(string name in new[]{"schema","nativeFrames","lastRunnerTick"})if(raw[name]?.Type!=JTokenType.Integer)throw new OriginalBindingException("Original BGM integer state field absent: "+name);
                    foreach(string name in new[]{"executionKey","worldId","completionSource"})if(raw[name]?.Type!=JTokenType.String)throw new OriginalBindingException("Original BGM owner state field absent: "+name);
                    foreach(string name in new[]{"completed","nativeFramesResolved"})if(raw[name]?.Type!=JTokenType.Boolean)throw new OriginalBindingException("Original BGM phase state field absent: "+name);
                    state=JsonUtility.FromJson<NativeWaitBgmState>(saved.text);NativeWaitBgmOwner.Validate(state,context.ExecutionKey,world);
                }
                if(state.completed)return OriginalNodeStatus.Success;if(state.lastRunnerTick==context.Runner.State.tick)return OriginalNodeStatus.Running;
                var frame=actualFrame?.Invoke(context);bool complete;
                if(frame!=null&&frame.resolved)
                {
                    if(frame.nativeLifecycle)
                    {
                        if(frame.sourceWorldId!=world||frame.sourceSequence<1)throw new OriginalBindingException("Foreign original BGM source frame owner");
                        var previous=context.Read("native-WaitBgm-frame-sequence/v1");
                        if(previous==null&&state.lastRunnerTick>=0)throw new OriginalBindingException("Persisted BGM original frame sequence is absent");
                        if(previous!=null)
                        {
                            if(previous.kind!="String"||!long.TryParse(previous.text,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out long last)||last<1||last>frame.sourceSequence)throw new OriginalBindingException("Malformed/regressed BGM original frame sequence");
                            if(last==frame.sourceSequence)return OriginalNodeStatus.Running;
                        }
                    }
                    if(frame.nativeLifecycle&&saved==null&&!context.IsNewEntry)
                    {
                        complete=NativeWaitBgmOwner.CompleteFromObservedStableAudio(state,actualBgm?.Invoke());
                        if(!complete)throw new OriginalBindingException("Entered legacy BGM wait lacks raw counter; only clock-independent stable-audio terminal proof is available");
                    }
                    else complete=NativeWaitBgmOwner.Step(state,frame,actualBgm);
                    // Mark only AFTER the real body and its BGM observation
                    // succeed. A failed input/observer must not advance flag
                    // or duplicate counter consumption when binding recovers.
                    if(frame.nativeLifecycle)
                    {
                        context.Runner.Set(context.Scope.values,"native-node-updated/v1/"+context.Node.index,OriginalValue.Bool(true));
                        context.Write("native-WaitBgm-frame-sequence/v1",OriginalValue.String(frame.sourceSequence.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }
                else
                {
                    var observation=actualBgm?.Invoke();complete=NativeWaitBgmOwner.CompleteFromObservedStableAudio(state,observation);
                    if(!complete)throw new OriginalBindingException(context.Address+": real BGM is transitioning; actual native node flag/integer frame cap owner still required. No fixed-time fallback.");
                }
                state.lastRunnerTick=context.Runner.State.tick;context.Write(key,OriginalValue.String(JsonUtility.ToJson(state)));context.Write("native-entry-proof/v1",OriginalValue.Bool(false));return complete?OriginalNodeStatus.Success:OriginalNodeStatus.Running;
            },NativeWaitBgmOwner.Source+"; actual host transition observer; stable-audio terminal proof without a fabricated native frame; active-transition actual typed unscaled frame port and persisted native node lifecycle");
        }
    }
}
