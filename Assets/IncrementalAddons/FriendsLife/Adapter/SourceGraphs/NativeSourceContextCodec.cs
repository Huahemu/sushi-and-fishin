using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // JsonUtility may materialize an inline complex class in a list element
    // even when that field is ABSENT from the original JSON. The raw property
    // presence, not the fabricated CLR object, owns optional-module presence.
    public static class NativeSourceContextCodec
    {
        static bool Absent(JToken value)=>value==null||value.Type==JTokenType.Null;
        static JArray ActorRows(JObject json,NativeSourceContextData data)
        {
            var rows=json["actors"] as JArray;
            if(data?.actors==null||rows==null||rows.Count!=data.actors.Count)throw new OriginalBindingException("Saved native actor JSON/DTO rows disagree");
            return rows;
        }
        static JObject ActorRow(JArray rows,int index,NativeSourceActorRecord actor)
        {
            var row=rows[index] as JObject;
            if(row==null||actor==null||row["nativeResidentId"]?.Type!=JTokenType.String||(string)row["nativeResidentId"]!=actor.nativeResidentId)
                throw new OriginalBindingException("Saved native actor JSON/DTO identity disagrees");
            return row;
        }
        public static NativeSourceContextData Decode(string sourceJson)
        {
            var json=JObject.Parse(sourceJson);var data=JsonUtility.FromJson<NativeSourceContextData>(sourceJson);
            if(data==null)throw new OriginalBindingException("Saved native source context is not a valid object");
            var rows=ActorRows(json,data);
            for(int index=0;index<data.actors.Count;index++)
            {
                var actor=data.actors[index];var row=ActorRow(rows,index,actor);var version=row["asModuleVersion"];var animation=row["animation"];
                if(version!=null&&version.Type!=JTokenType.Integer)throw new OriginalBindingException("Saved actor AS module version is not an integer");
                int declared=version==null?0:(int)version;
                if(declared==0)
                {
                    // Exact legacy/empty representation: no AS module is
                    // declared and no AS payload exists in the RAW JSON.
                    // Never erase an object (even an empty object) that the
                    // payload actually stored under an undeclared version.
                    if(!Absent(animation))throw new OriginalBindingException("Actor AS payload exists without a declared module version");
                    actor.asModuleVersion=0;actor.animation=null;continue;
                }
                if(declared!=1)throw new OriginalBindingException("Unknown saved actor AS module version");
                if(!(animation is JObject rawAs)||actor.asModuleVersion!=1||actor.animation==null)
                    throw new OriginalBindingException("Declared actor AS module has no saved payload");
                // date/face are genuinely optional inside a declared AS
                // instance. Required history/body records still go through
                // the strict existing NativeDateAsRuntime.Validate checks.
                var rawDate=rawAs["date"];
                if(Absent(rawDate))actor.animation.date=null;
                else if(!(rawDate is JObject))throw new OriginalBindingException("Saved Date AS state is not an object");
                else if(actor.animation.date!=null&&Absent(rawDate["face"]))actor.animation.date.face=null;
                NativeDateAsRuntime.Validate(actor.animation);
            }
            return data;
        }
        public static string Encode(NativeSourceContextData data)
        {
            if(data?.actors==null)throw new OriginalBindingException("Native source actor owner is unavailable for serialization");
            foreach(var actor in data.actors)
            {
                if(actor==null)throw new OriginalBindingException("Native source actor owner is null");
                if(actor.asModuleVersion==0)
                {if(actor.animation!=null)throw new OriginalBindingException("Actor AS data has no declared module version");}
                else if(actor.asModuleVersion==1)NativeDateAsRuntime.Validate(actor.animation);
                else throw new OriginalBindingException("Unknown saved actor AS module version");
            }
            var json=JObject.Parse(JsonUtility.ToJson(data));var rows=ActorRows(json,data);
            for(int index=0;index<data.actors.Count;index++)
            {
                var actor=data.actors[index];var row=ActorRow(rows,index,actor);
                // Canonicalize only fields whose ACTUAL CLR owner is null.
                // This prevents the serializer from persisting a fabricated
                // empty inline representation during a later save/snapshot.
                if(actor.asModuleVersion==0){row.Remove("animation");continue;}
                var rawAs=row["animation"] as JObject;
                if(rawAs==null)throw new OriginalBindingException("Declared AS module failed serialization");
                if(actor.animation.date==null)rawAs.Remove("date");
                else if(actor.animation.date.face==null&&(rawAs["date"] as JObject)!=null)((JObject)rawAs["date"]).Remove("face");
            }
            return json.ToString(Formatting.None);
        }
    }
}
