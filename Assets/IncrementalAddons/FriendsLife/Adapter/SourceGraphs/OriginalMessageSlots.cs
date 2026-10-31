using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // The source MessageParam setters own the values. Reading the message never
    // samples a random item or substitutes a resident selected by the host UI.
    public static class OriginalMessageSlots
    {
        public static Dictionary<string,string> Build(OriginalNodeContext context,string speaker)
        {
            var result=new Dictionary<string,string>(StringComparer.Ordinal){["Talker"]=speaker};
            var participants=context.Participants;
            // Original ParticipantMiiN is the ordered demo participant role,
            // independent of a later TalkingMii/SlaveActorHandleList reorder.
            // Missing participants remain missing; never fill them from UI or
            // the current speaker, and never manufacture a larger party.
            for(int i=0;i<participants.Length;i++)if(!string.IsNullOrEmpty(participants[i]))result["ParticipantMii"+i]=participants[i];
            OriginalBagState bag=null;
            if(context.HasInput("MessageParam"))
            {
                var pointer=context.Input("MessageParam");
                if(!pointer.IsNull)bag=context.Bag(pointer);
            }
            var talking=bag==null?null:context.Runner.Get(bag.values,"ActorHandleList");
            var actors=talking==null?participants:talking.ActorIds();
            for(int i=0;i<actors.Length;i++){result["Mii"+i]=actors[i];result["SlotMii"+i]=actors[i];}
            if(bag==null)return result;
            foreach(var slot in bag.values)
            {
                if(slot.value==null||slot.value.IsNull||slot.value.kind=="Bag"||slot.value.kind=="Actors")continue;
                result[slot.key]=slot.value.AsText();
                if(slot.key.StartsWith("Number",StringComparison.Ordinal)&&int.TryParse(slot.key.Substring(6),out int numberSlot))
                {
                    string value=slot.value.AsText();result["NumberNoSeparate/"+numberSlot]=value;
                    if(slot.value.kind=="Number")
                        foreach(string tag in new[]{"NumberSeparate_4","NumberSeparate_5","NumberSeparate"})
                            result[tag+"/"+numberSlot]=slot.value.number.ToString("#,0.################",CultureInfo.InvariantCulture);
                }
                foreach(string tag in new[]{"GeneralMessage","GeneralMessage10","GeneralMessage32","UGCText","BookText"})
                    if(slot.key.StartsWith(tag,StringComparison.Ordinal)&&int.TryParse(slot.key.Substring(tag.Length),out int index))result[tag+"/"+index]=slot.value.AsText();
            }
            return result;
        }
    }
}
