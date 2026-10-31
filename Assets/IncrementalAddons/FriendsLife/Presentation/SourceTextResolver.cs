using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // A key always includes the source file. Missing providers are explicit errors;
    // no English key, generated sentence or R summary is used as NPC dialogue.
    public sealed class SourceTextResolver
    {
        public sealed class Message {public string key,text,style;public JObject attrs;}
        public sealed class Run {public string text,control;public IReadOnlyDictionary<string,string> args;public float pause,speed=1,charSeconds=-1;public bool pageBreak;}
        static Dictionary<string,Message> sharedMessages;
        readonly Dictionary<string,Message> messages;
        readonly Dictionary<string,Func<IReadOnlyDictionary<string,string>,string>> providers=new Dictionary<string,Func<IReadOnlyDictionary<string,string>,string>>(StringComparer.Ordinal);
        IReadOnlyDictionary<string,string> context;
        public event Action<string,IReadOnlyDictionary<string,string>> ControlRequested;
        public static readonly string[] TagNames=("Nickname Color GeneralMessage PageBreak Island Scale MoneyUnitPrice RelationLong EditMii GeneralMessage10 EditUGC MapObject GeneralMessage32 MoneyTotalAmount NumberNoSeparate Month Day AMPM ClockTime BloodRelation Year RoommateGroup Travel UGCMapObject Ground UGCMapFloor RelationMeterRank Relation Character4 Character16 Cloth Coordinate UGCCloth Cap WordAttrCount WordAttrMessage IslandType Food Goods UGCGoods UGCFood RoomStyleVariationGroup UGCInterior UGCExterior NumberSeparate_5 PlayerNicknameCommon Nickname_Ex_JPKR Break Week GeneralItem Habit SoundEffect Speed WordLong HabitFace UGCText NumberSeparate_4 EmotionShout FirstPerson RelationMiiNickname Sub EmotionAnger WordShortEnd PlayerNicknameByMii Pause EmotionSadness ReplicaSwordPreset WordShortStart_Ex_ASIA BookText BalloonStyle MouthState FixPosition SpeedDirect Size CurrentClockTimeHHMMSS IsSame TelopOff RegisteredTrademark AS Expression EmotionWhisper LipSyncOff RelationPairMiiNickname CountT FocusTelopSubject IsExist FocusTelopSubjectOnePerson BloodRelationLong ClockHour Pulldown vtml_speed WordAttr").Split(' ');
        static readonly HashSet<string> Controls=new HashSet<string>(("Color Scale SoundEffect HabitFace EmotionShout EmotionAnger EmotionSadness BalloonStyle MouthState FixPosition Size TelopOff AS Expression EmotionWhisper LipSyncOff WordAttr Sub IsSame IsExist FocusTelopSubject FocusTelopSubjectOnePerson").Split(' '),StringComparer.Ordinal);
        static readonly Regex Tags=new Regex("<(?<name>[A-Za-z0-9_]+)(?<args>[^<>]*?)/>",RegexOptions.Compiled);
        static readonly Regex Args=new Regex("(?<k>[A-Za-z0-9_]+)=(?:\"(?<v>[^\"]*)\"|(?<v>[^\\s]+))",RegexOptions.Compiled);
        public int Count=>messages.Count;
        public SourceTextResolver()
        {
            if(sharedMessages!=null){messages=sharedMessages;return;}
            messages=new Dictionary<string,Message>(StringComparer.Ordinal);
            var asset=Resources.Load<TextAsset>("FriendsLife/CNzh原文目录");if(!asset)return;
            var doc=JObject.Parse(asset.text);foreach(var row in doc["messages"]??new JArray())
            {var key=(string)row["key"];if(string.IsNullOrEmpty(key))continue;messages[key]=new Message{key=key,text=(string)row["text"]??"",style=(string)row["style"],attrs=row["attrs"]as JObject};}sharedMessages=messages;
        }
        public Message ResolveExact(string key)
        {if(!messages.TryGetValue(key??"",out var m))throw new KeyNotFoundException("Missing original TextRef: "+key);return m;}
        public bool TryExact(string key,out string text){if(messages.TryGetValue(key??"",out var m)){text=m.text;return true;}text="";return false;}
        public void BindSlots(string tag,Func<IReadOnlyDictionary<string,string>,string> provider)=>providers[tag]=provider;
        public void BindContext(IReadOnlyDictionary<string,string> slots)=>context=slots;
        public static string TagSignature(string name,IReadOnlyDictionary<string,string> args)=>name+"|"+string.Join(";",System.Linq.Enumerable.Select(System.Linq.Enumerable.OrderBy(args,p=>p.Key,StringComparer.Ordinal),p=>p.Key+"="+p.Value));
        string Replacement(string name,IReadOnlyDictionary<string,string> args,string key)
        {
            if(providers.TryGetValue(name,out var provider))return provider(args)??throw new InvalidOperationException("Unresolved original parameter: "+name+" in "+key);
            if(context!=null)
            {
                if(context.TryGetValue(TagSignature(name,args),out var exact))return exact;
                if(args.TryGetValue("slot",out var slot)&&(context.TryGetValue(name+"/"+slot,out exact)||context.TryGetValue(name+":"+slot,out exact)))return exact;
                if((name=="Month"||name=="Day")&&context.TryGetValue(name,out var date)&&int.TryParse(date,NumberStyles.Integer,CultureInfo.InvariantCulture,out var number))
                {
                    string label=name+(name=="Month"&&args.TryGetValue("style",out var style)&&style=="Short"?"Short":"")+number.ToString("00",CultureInfo.InvariantCulture);
                    if(messages.TryGetValue("ReplaceMsg/"+name+".msbt.yml#"+label,out var nativeDate))return nativeDate.text;
                    throw new InvalidOperationException("Invalid original date parameter: "+name+"="+date+" in "+key);
                }
                if(context.TryGetValue(name,out exact))return exact;
            }
            throw new InvalidOperationException("Missing source tag provider: "+name+" in "+key);
        }
        public void EmitControl(Run run){if(run.control!=null)ControlRequested?.Invoke(run.control,run.args);}
        public List<Run> Play(string key)
        {
            var result=new List<Run>();var text=ResolveExact(key).text.Replace("</Sub>","");int cursor=0;float speed=1,charSeconds=-1;
            foreach(Match match in Tags.Matches(text))
            {
                if(match.Index>cursor)result.Add(new Run{text=text.Substring(cursor,match.Index-cursor),speed=speed,charSeconds=charSeconds});
                string name=match.Groups["name"].Value;var args=new Dictionary<string,string>();foreach(Match a in Args.Matches(match.Groups["args"].Value))args[a.Groups["k"].Value]=a.Groups["v"].Value;
                switch(name)
                {
                    case "PageBreak":case "Break":result.Add(new Run{pageBreak=true,text="\n"});break;
                    case "Pause":float seconds=0;if(args.TryGetValue("msec",out var time)){float.TryParse(time,NumberStyles.Float,CultureInfo.InvariantCulture,out seconds);seconds/=1000;}else if(args.TryGetValue("time",out time)||args.TryGetValue("sec",out time))float.TryParse(time,NumberStyles.Float,CultureInfo.InvariantCulture,out seconds);result.Add(new Run{pause=seconds});break;
                    case "Speed":case "SpeedDirect":case "vtml_speed":
                        if(args.TryGetValue("speed",out var rate)&&float.TryParse(rate,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)&&value>0)
                        {if(name=="SpeedDirect"){charSeconds=1/value;speed=1;}else{speed=name=="Speed"?value/100:value;charSeconds=-1;}}
                        result.Add(new Run{control=name,args=args,speed=speed,charSeconds=charSeconds});break;
                    default:if(Controls.Contains(name))result.Add(new Run{control=name,args=args,speed=speed,charSeconds=charSeconds});else result.Add(new Run{text=Replacement(name,args,key),speed=speed,charSeconds=charSeconds});break;
                }
                cursor=match.Index+match.Length;
            }
            if(cursor<text.Length)result.Add(new Run{text=text.Substring(cursor),speed=speed,charSeconds=charSeconds});return result;
        }
        public string Format(string key){var result="";foreach(var run in Play(key))result+=run.text;return result;}
        public string[] Choose(string key)
        {var result=new List<string>();var current="";foreach(var run in Play(key)){if(run.pageBreak){if(current.Length>0){result.Add(current.Trim());current="";}}else current+=run.text;}if(current.Length>0)result.Add(current.Trim());return result.ToArray();}
        public string RelationRank(string kind,int rank,bool fighting,bool oneSideLove)
        {
            // 挚友 has no own meter rank table; it uses the Friend ranks.
            string nativeKind=kind=="Known"?"Know":kind=="Spouse"?"Couple":kind=="ExSpouse"?"Divorce":kind=="BestFriend"?"Friend":kind;
            if(oneSideLove)nativeKind="OnesideLove";
            string key="ReplaceMsg/RelationMeterRank.msbt.yml#"+nativeKind;
            return TryExact(key+(fighting?"_Fight":"")+"_"+rank,out var text)||fighting&&TryExact(key+"_"+rank,out text)?text:"";
        }
        public string RelationName(string kind)
        {
            string native=kind=="Known"?"Know":kind=="Spouse"?"Couple":kind=="ExSpouse"?"Divorce":kind;
            foreach(string file in new[]{"Relation","RelationLong"})if(TryExact("ReplaceMsg/"+file+".msbt.yml#"+native,out var s))return s;
            // The source catalog has no BestFriend or Family key.
            return kind=="BestFriend"?"挚友":kind=="Family"?"家人":"";
        }
    }
}
