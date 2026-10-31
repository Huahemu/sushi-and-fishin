using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    public static class C004NativeWaitSecBindings
    {
        [Serializable] sealed class Record
        {
            public int schema=1;public string worldId,executionKey;
            public long lastFrame=-1;public C004NativeWaitSecState state;
        }
        [StructLayout(LayoutKind.Explicit)]struct Word{[FieldOffset(0)]public uint bits;[FieldOffset(0)]public float value;}
        public static OriginalNodeStatus Execute(OriginalNodeContext context,double ignoredHostSeconds)
        {
            const string key="native-WaitSec/v1";string flagsKey="native-node-updated/v1/"+context.Node.index;
            var source=context.Runner.NativeFrameInput?.Invoke();C004NativeFrameProducer.ValidatePublishedFrame(source);
            var stored=context.Read(key);Record record;
            if(stored==null)
            {
                if(!context.IsNewEntry)throw new OriginalBindingException(context.Address+": entered legacy wait has no original raw duration/counter; host elapsed seconds cannot reconstruct it");
                var input=context.Input("Sec");if(input.kind!="Number")throw new OriginalBindingException(context.Address+": exact original F32 Sec input required");
                uint bits=new Word{value=(float)input.number}.bits;
                record=new Record{worldId=source.worldId,executionKey=context.ExecutionKey,state=C004NativeWaitSecBody.Enter(bits,context.ExecutionKey)};
                context.Write(key,OriginalValue.String(JsonUtility.ToJson(record)));
                context.Write("native-entry-proof/v1",OriginalValue.Bool(false));
            }
            else
            {
                if(stored.kind!="String")throw new OriginalBindingException(context.Address+": invalid raw WaitSec record kind");var raw=JObject.Parse(stored.text);
                foreach(string field in new[]{"schema","worldId","executionKey","lastFrame","state"})if(raw[field]==null)throw new OriginalBindingException("Raw WaitSec field absent: "+field);
                foreach(string field in new[]{"schema","remainingSteps","rawSecBits","completed","executionKey"})if(raw["state"]?[field]==null)throw new OriginalBindingException("Raw native WaitSec state field absent: "+field);
                record=JsonUtility.FromJson<Record>(stored.text);if(record==null||record.schema!=1||record.worldId!=source.worldId||record.executionKey!=context.ExecutionKey||record.lastFrame< -1)throw new OriginalBindingException("Foreign raw WaitSec continuation");C004NativeWaitSecBody.Validate(record.state,context.ExecutionKey);
            }
            if(record.lastFrame>source.sequence)throw new OriginalBindingException("Original WaitSec source sequence regressed");
            var flag=context.Runner.Get(context.Scope.values,flagsKey);
            if(flag==null){if(record.lastFrame>=1)throw new OriginalBindingException("Persisted native node updated flag absent after original WaitSec update");flag=OriginalValue.Bool(false);context.Runner.Set(context.Scope.values,flagsKey,flag);}
            if(flag.kind!="Bool")throw new OriginalBindingException("Original generic node updated flag is not raw Bool");
            if(record.state.completed)return OriginalNodeStatus.Success;
            if(record.lastFrame==source.sequence)return OriginalNodeStatus.Running;
            // Actual genericUpdate00002ef0 calls the native body BEFORE setting
            // bit1 at00003318. First update therefore consumes no integer step;
            // actual new activation4ca10 resets bit1 before Enter; the runner
            // persists the current activation flag across ordinary saves.
            bool completed=C004NativeWaitSecBody.Step(record.state,context.ExecutionKey,flag.boolean,source.scaledStep);
            context.Runner.Set(context.Scope.values,flagsKey,OriginalValue.Bool(true));record.lastFrame=source.sequence;
            context.Write(key,OriginalValue.String(JsonUtility.ToJson(record)));return completed?OriginalNodeStatus.Success:OriginalNodeStatus.Running;
        }
    }
}
