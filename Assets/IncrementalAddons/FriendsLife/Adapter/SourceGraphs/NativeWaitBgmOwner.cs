using System;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [Serializable]public sealed class NativeWaitBgmState
    {
        public int schema=1,nativeFrames;
        public string executionKey,worldId,completionSource="";
        public bool completed,nativeFramesResolved=true;
        public long lastRunnerTick=-1;
    }
    public sealed class NativeWaitBgmFrameInput
    {
        public bool resolved,nodeFlagBit1;
        public int actualIntegerFrameStep;
        public string source;
        public bool nativeLifecycle;public long sourceSequence=-1;public string sourceWorldId;
    }
    public sealed class NativeBgmObservation
    {public bool resolved,transitioning;public string owner,detail;}
    public static class NativeWaitBgmOwner
    {
        public const string Source="WaitBgm getter01899174/vtable031439d8; enter01899060; update01899068; exit018990e8; actual BGM list0205588c/0259e790";
        public static NativeWaitBgmState Enter(string executionKey,string worldId)
        {
            if(string.IsNullOrEmpty(executionKey)||string.IsNullOrEmpty(worldId))throw new InvalidOperationException("Actual BGM wait activation/world required");return new NativeWaitBgmState{executionKey=executionKey,worldId=worldId};
        }
        public static void Validate(NativeWaitBgmState state,string executionKey,string worldId)
        {
            if(state==null||state.schema!=1||state.executionKey!=executionKey||state.worldId!=worldId||string.IsNullOrEmpty(executionKey)||string.IsNullOrEmpty(worldId)||state.lastRunnerTick< -1||state.completionSource==null||!state.nativeFramesResolved&&!state.completed||!state.completed&&state.completionSource!=""||state.completed&&(state.nativeFramesResolved?state.completionSource!="Original240IntegerFrameBoundary"&&!state.completionSource.StartsWith("ObservedStableBgm/",StringComparison.Ordinal):!state.completionSource.StartsWith("ClockIndependentStableBgm/",StringComparison.Ordinal)))throw new InvalidOperationException("Malformed/foreign BGM wait continuation");
        }
        // The raw integer step is supplied by its own source owner. It is not
        // dt*60, an AudioSource time, wall seconds or an invented fixed delay.
        public static bool Step(NativeWaitBgmState state,NativeWaitBgmFrameInput frame,Func<NativeBgmObservation> observe)
        {
            Validate(state,state.executionKey,state.worldId);if(state.completed)return true;
            if(frame==null||!frame.resolved)throw new InvalidOperationException("Actual source node flag/integer frame owner unavailable");
            int next=frame.nodeFlagBit1?unchecked(state.nativeFrames+frame.actualIntegerFrameStep):state.nativeFrames;
            if(frame.nodeFlagBit1&&next>=240){state.nativeFrames=next;state.completed=true;state.completionSource="Original240IntegerFrameBoundary";return true;}
            var actual=observe?.Invoke();if(actual==null||!actual.resolved||string.IsNullOrEmpty(actual.owner))throw new InvalidOperationException("Actual host BGM transition owner unavailable");
            state.nativeFrames=next;if(actual.transitioning)return false;state.completed=true;state.completionSource="ObservedStableBgm/"+actual.owner;return true;
        }
        public static bool CompleteFromObservedStableAudio(NativeWaitBgmState state,NativeBgmObservation actual)
        {
            Validate(state,state.executionKey,state.worldId);if(state.completed)return true;if(actual==null||!actual.resolved||string.IsNullOrEmpty(actual.owner))throw new InvalidOperationException("Actual host BGM transition owner unavailable");if(actual.transitioning)return false;
            // For EVERY raw flag/counter/int-step input, native update either
            // exits at240 or observes stable BGM and exits. No future tick can
            // inspect its dead counter before OnEnter resets it. Record the
            // terminal proof and explicitly leave that dead counter unresolved;
            // do not fabricate its raw value or a source clock.
            state.completed=true;state.nativeFramesResolved=false;state.completionSource="ClockIndependentStableBgm/"+actual.owner;return true;
        }
    }
}
