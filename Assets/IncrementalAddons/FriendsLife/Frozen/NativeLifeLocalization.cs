using System;
using System.Collections.Generic;
using UnityEngine;

namespace TomodachiLife
{
    // Display-only names. Keep the caller's native key for simulation actions.
    public static class NativeLifeLocalization
    {
        public const string ResourceKey="LifeRules/localization";
        private static Dictionary<string,string> foods,goods;
        private static bool loaded;
#pragma warning disable CS0649 // Fields are populated from the source-derived JSON resource.
        [Serializable] private sealed class Entry { public string name,displayName; }
        [Serializable] private sealed class Document { public Entry[] foods,goods; }
#pragma warning restore CS0649

        public static string Food(string name)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(name)&&foods.TryGetValue(name,out var label)?label:name;
        }
        public static string Goods(string name)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(name)&&goods.TryGetValue(name,out var label)?label:name;
        }
        public static string Text(string value)
        {
            EnsureLoaded();return System.Text.RegularExpressions.Regex.Replace(value??"","[A-Za-z][A-Za-z0-9_]*",match=>foods.TryGetValue(match.Value,out var food)?food:goods.TryGetValue(match.Value,out var gift)?gift:match.Value);
        }
        public static int FoodCount { get{EnsureLoaded();return foods.Count;} }
        public static int GoodsCount { get{EnsureLoaded();return goods.Count;} }

        private static void EnsureLoaded()
        {
            if(loaded)return;
            foods=new Dictionary<string,string>(StringComparer.Ordinal);
            goods=new Dictionary<string,string>(StringComparer.Ordinal);
            var source=Resources.Load<TextAsset>(ResourceKey);
            if(source!=null)
            {
                var document=JsonUtility.FromJson<Document>(source.text);
                if(document!=null){Fill(foods,document.foods);Fill(goods,document.goods);}
            }
            loaded=true;
        }
        private static void Fill(Dictionary<string,string> target,Entry[] entries)
        {
            if(entries==null)return;
            foreach(var entry in entries)
                if(entry!=null&&!string.IsNullOrEmpty(entry.name)&&!string.IsNullOrEmpty(entry.displayName))
                    target[entry.name]=entry.displayName;
        }
    }
}
