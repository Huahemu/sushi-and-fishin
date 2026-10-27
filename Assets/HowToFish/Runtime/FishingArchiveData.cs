using System;
using System.Collections.Generic;
using UnityEngine;
namespace HowToFish {
    public enum FishArchiveAppearance { Silhouette,Grayscale,Original,Shiny }
    public enum FishingArchiveCommand { Collection,Warehouse,Close,NextPage,PreviousPage,NextEntry,PreviousEntry,ToggleAppearance }
    [Serializable] public sealed class FishCollectionRecord {
        public int id;
        public bool originalSeen,shinySeen;
        public long lifted,escaped,originalCaptured,shinyCaptured;
        public float bestWeight;
        public bool Seen=>originalSeen||shinySeen||Acquired;
        public bool Acquired=>originalCaptured>0||shinyCaptured>0;
        public FishArchiveAppearance Appearance(bool showShiny=false){
            if(originalCaptured>0)return showShiny&&shinyCaptured>0?FishArchiveAppearance.Shiny:FishArchiveAppearance.Original;
            if(shinyCaptured>0)return FishArchiveAppearance.Shiny;
            return Seen?FishArchiveAppearance.Grayscale:FishArchiveAppearance.Silhouette;
        }
    }
    [Serializable] public sealed class FishingArchiveData {
        public int version=2;
        public int[] bait={-1,8,8,8,8};
        public List<FishingInventory.Record> fish=new List<FishingInventory.Record>();
        public List<FishCollectionRecord> collection=new List<FishCollectionRecord>();
        public long captures;
        public float best;
        public static FishingArchiveData Decode(string json)=>Decode(json,out _);
        public static FishingArchiveData Decode(string json,out bool migrated){
            migrated=false;
            FishingArchiveData data=null;
            if(!string.IsNullOrEmpty(json))data=JsonUtility.FromJson<FishingArchiveData>(json);
            if(data==null)data=new FishingArchiveData();
            if(data.version>2)throw new InvalidOperationException("Fishing save was created by a newer version");
            bool legacy=data.version<2;migrated=legacy;
            if(data.fish==null)data.fish=new List<FishingInventory.Record>();if(data.collection==null)data.collection=new List<FishCollectionRecord>();
            if(data.bait==null||data.bait.Length!=5)data.bait=new[]{-1,8,8,8,8};
            foreach(var fish in data.fish){
                if(fish==null)continue;
                fish.name=FishChineseNames.Get(fish.id,fish.name);
                fish.count=Math.Max(0,fish.count);fish.originalCount=Math.Max(0,fish.originalCount);fish.shinyCount=Math.Max(0,fish.shinyCount);
                if(fish.originalCount+fish.shinyCount<fish.count)fish.originalCount+=fish.count-fish.originalCount-fish.shinyCount;
                fish.count=fish.originalCount+fish.shinyCount;
                if(legacy){
                    var record=data.collection.Find(r=>r!=null&&r.id==fish.id);if(record==null){record=new FishCollectionRecord{id=fish.id};data.collection.Add(record);}
                    record.originalSeen=true;record.originalCaptured=Math.Max(record.originalCaptured,Math.Max(1,fish.count));record.bestWeight=Math.Max(record.bestWeight,fish.bestWeight);
                }
            }
            data.fish.RemoveAll(r=>r==null);data.collection.RemoveAll(r=>r==null);data.version=2;return data;
        }
    }
}
