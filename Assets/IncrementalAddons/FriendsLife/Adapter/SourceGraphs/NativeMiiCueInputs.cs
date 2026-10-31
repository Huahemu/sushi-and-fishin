using System;using UnityEngine;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // Source parameter contract shared by the live bridge and actual Runner
    // continuation checks. No world/clock/RNG lookup and no invented actor.
    public static class NativeMiiCueInputs
    {
        public static int Expression(OriginalNodeContext context)
        {
            var value=context.Input("Expression");if(value==null||value.kind!="Number"||double.IsNaN(value.number)||double.IsInfinity(value.number)||value.number!=Math.Truncate(value.number)||value.number< -1||value.number>=75)throw new OriginalBindingException(context.Address+": actual original int Expression unavailable");return(int)value.number;
        }
        public static bool IgnoreHabit(OriginalNodeContext context)
        {var value=context.Property("Prop_IgnoreHabit");if(value==null||value.kind!="Bool")throw new OriginalBindingException(context.Address+": original Prop_IgnoreHabit bool unavailable");return value.boolean;}
        public static string[] Actors(OriginalNodeContext context)
        {var value=context.Input("SlaveActorHandleList");if(value==null||value.kind!="Actors")throw new OriginalBindingException(context.Address+": actual original actor-list owner required");return value.ActorIds();}
        public static NativeMiiCueEndData CachedEnd(OriginalNodeContext context,string world)
        {
            var saved=context.Read("native-face-end/"+context.ExecutionKey);if(saved==null)return null;if(saved.kind!="String")throw new OriginalBindingException("Source CueEnd continuation has no raw captured actor list");NativeMiiCueSession.ValidateEndRawEnvelope(saved.text);var data=JsonUtility.FromJson<NativeMiiCueEndData>(saved.text);NativeMiiCueSession.ValidateEnd(data,world,context.ExecutionKey);return data;
        }
        public static void CacheEnd(OriginalNodeContext context,NativeMiiCueEndData data)
        {NativeMiiCueSession.ValidateEnd(data,data.worldId,context.ExecutionKey);context.Write("native-face-end/"+context.ExecutionKey,OriginalValue.String(JsonUtility.ToJson(data)));}
        public static NativeMiiCueCaptureData CachedCue(OriginalNodeContext context,string world)
        {
            var saved=context.Read("native-face-capture/"+context.ExecutionKey);if(saved==null)return null;if(saved.kind!="String")throw new OriginalBindingException("Source Cue raw capture is not serialized parameters");NativeMiiCueSession.ValidateCaptureRawEnvelope(saved.text);var data=JsonUtility.FromJson<NativeMiiCueCaptureData>(saved.text);NativeMiiCueSession.ValidateCapture(data,world,context.ExecutionKey,context.Address);return data;
        }
        public static void CacheCue(OriginalNodeContext context,NativeMiiCueCaptureData data)
        {NativeMiiCueSession.ValidateCapture(data,data.worldId,context.ExecutionKey,context.Address);context.Write("native-face-capture/"+context.ExecutionKey,OriginalValue.String(JsonUtility.ToJson(data)));}
    }
}
