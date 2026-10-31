using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using SushiAndFish.Casino.Original;
namespace SushiAndFish.FriendsLife
{
    // Data codec for NEW-owned clone objects and original iterator instances only.
    internal static class NativeCasinoCloneCodec
    {
        internal static FieldInfo Field(Type type,string name){for(;type!=null;type=type.BaseType){var f=type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);if(f!=null)return f;}return null;}
        internal static object Get(object target,string name)=>Field(target.GetType(),name)?.GetValue(target);
        internal static void Set(object target,string name,object value){var f=Field(target.GetType(),name);if(f!=null)f.SetValue(target,value);}
        internal static object Invoke(object target,Type declaring,string method,params object[] args)=>declaring.GetMethod(method,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(target,args);
        static FieldInfo[] Fields(Type t){var list=new System.Collections.Generic.List<FieldInfo>();for(;t!=null&&t!=typeof(object);t=t.BaseType)list.AddRange(t.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Where(f=>!f.IsStatic));return list.ToArray();}
        internal static JToken Encode(object value,FriendsCasinoCloneDriver owner)
        {
            if(value==null)return JValue.CreateNull();var t=value.GetType();
            if(t.IsPrimitive||t==typeof(string)||t==typeof(decimal))return new JValue(value);
            if(t.IsEnum)return new JObject{["enum"]=t.AssemblyQualifiedName,["value"]=Convert.ToInt64(value)};
            if(value is UnityEngine.Object obj)
            {
                if(obj is Component c&&c.transform.IsChildOf(owner.transform))return new JObject{["component"]=c.GetType().AssemblyQualifiedName,["path"]=owner.Relative(c.transform),["index"]=Array.IndexOf(c.GetComponents(c.GetType()),c)};
                if(obj is GameObject go&&go.transform.IsChildOf(owner.transform))return new JObject{["gameObject"]=owner.Relative(go.transform)};
                int pattern=owner.Patterns.IndexOf(obj as SlotPattern);if(pattern>=0)return new JObject{["pattern"]=pattern};
                for(int i=0;i<owner.Reels.Count;i++){var atlas=Get(owner.Reels[i],"atlas") as Sprite[];int index=atlas==null?-1:Array.IndexOf(atlas,obj as Sprite);if(index>=0)return new JObject{["spriteReel"]=i,["sprite"]=index};}
                throw new InvalidOperationException("Unsupported external native clone reference: "+t.FullName);
            }
            if(value is IList list){var a=new JArray();foreach(var item in list)a.Add(Encode(item,owner));return new JObject{["list"]=t.AssemblyQualifiedName,["items"]=a};}
            if(!(t.Namespace??"").StartsWith("SushiAndFish.Casino.Original",StringComparison.Ordinal)&&!(t.Namespace??"").StartsWith("System.Collections.Generic",StringComparison.Ordinal)&&!(t.Namespace??"").StartsWith("IncrementalAddons.Casino.SourceReuse",StringComparison.Ordinal)&&!t.IsValueType)throw new InvalidOperationException("Unsupported native iterator data "+t.FullName);
            var fields=new JObject();foreach(var f in Fields(t)){if(f.FieldType==typeof(object)&&f.Name.Contains("current"))continue;fields[f.Name]=Encode(f.GetValue(value),owner);}
            return new JObject{["type"]=t.AssemblyQualifiedName,["fields"]=fields};
        }
        internal static object Decode(JToken token,Type expected,FriendsCasinoCloneDriver owner)
        {
            if(token==null||token.Type==JTokenType.Null)return null;
            if(token is JValue)return Convert.ChangeType(((JValue)token).Value,expected,System.Globalization.CultureInfo.InvariantCulture);
            var o=(JObject)token;
            if(o["enum"]!=null)return Enum.ToObject(expected,(long)o["value"]);
            if(o["component"]!=null){var tr=owner.Find((string)o["path"]);var type=Type.GetType((string)o["component"],true);return tr.GetComponents(type)[(int)o["index"]];}
            if(o["gameObject"]!=null)return owner.Find((string)o["gameObject"]).gameObject;
            if(o["pattern"]!=null)return owner.Patterns[(int)o["pattern"]];
            if(o["sprite"]!=null)return ((Sprite[])Get(owner.Reels[(int)o["spriteReel"]],"atlas"))[(int)o["sprite"]];
            if(o["list"]!=null)
            {
                var type=Type.GetType((string)o["list"],true);var a=(JArray)o["items"];var itemType=type.IsArray?type.GetElementType():type.GetGenericArguments()[0];
                if(type.IsArray){var arr=Array.CreateInstance(itemType,a.Count);for(int i=0;i<a.Count;i++)arr.SetValue(Decode(a[i],itemType,owner),i);return arr;}
                var list=(IList)Activator.CreateInstance(type);foreach(var x in a)list.Add(Decode(x,itemType,owner));return list;
            }
            var actual=Type.GetType((string)o["type"],true);
            if(!expected.IsAssignableFrom(actual)&&expected!=typeof(object))throw new InvalidOperationException("Native snapshot field type mismatch");
            if(!(actual.Namespace??"").StartsWith("SushiAndFish.Casino.Original",StringComparison.Ordinal)&&!(actual.Namespace??"").StartsWith("System.Collections.Generic",StringComparison.Ordinal)&&!(actual.Namespace??"").StartsWith("IncrementalAddons.Casino.SourceReuse",StringComparison.Ordinal)&&!actual.IsValueType)throw new InvalidOperationException("Unexpected native snapshot type");
            var result=actual.IsValueType?Activator.CreateInstance(actual):FormatterServices.GetUninitializedObject(actual);
            foreach(var pair in (JObject)o["fields"]){var f=Field(actual,pair.Key);if(f==null)throw new InvalidOperationException("Native field no longer exists: "+pair.Key);f.SetValue(result,Decode(pair.Value,f.FieldType,owner));}return result;
        }
        internal static JObject Machine(FriendsCasinoCloneDriver d)
        {
            var o=new JObject();foreach(string key in new[]{"currentBet","isPlaying","canBet","gameTurn","isGoldenBet","isGoldenChipApplied","_finalMultiplier","estimatedValue"}){var f=Field(d.Game.GetType(),key);if(f!=null)o[key]=Encode(f.GetValue(d.Game),d);}return o;
        }
        internal static void RestoreMachine(FriendsCasinoCloneDriver d,JObject o){foreach(var pair in o){var f=Field(d.Game.GetType(),pair.Key);if(f==null)throw new InvalidOperationException("Native field missing "+pair.Key);f.SetValue(d.Game,Decode(pair.Value,f.FieldType,d));}}
    }
}
