using System;
using System.Collections.Generic;
using UnityEngine;
namespace HowToFish {
    public sealed class FishingInventory:MonoBehaviour {
        [Serializable] public sealed class Record{public int id;public long count,originalCount,shinyCount;public string name;public float bestWeight;}
        public bool persistence=true;
        public string saveKey="HowToFishPortable.Demo.Inventory.v1";
        FishingArchiveData data;
        public void UseSharedData(FishingArchiveData shared){data=shared??new FishingArchiveData();persistence=false;Changed?.Invoke();}
        public event Action Changed;
        public event Action<FishingArchiveCommand> ArchiveRequested;
        public bool ArchiveOpen{get;set;}
        public bool ArchiveIsWarehouse{get;set;}
        public void RequestArchive(FishingArchiveCommand command){ArchiveRequested?.Invoke(command);}
        FishingArchiveData Data{get{if(data==null)Load();return data;}}
        public long Total{get{long n=0;foreach(var r in Data.fish)n+=r.count;return n;}}
        public long Landed=>Data.captures;
        public float RecordWeight=>Data.best;
        public IReadOnlyList<Record> Records=>Data.fish;
        public IReadOnlyList<FishCollectionRecord> Collection=>Data.collection;
        public FishCollectionRecord Discovery(int id)=>Data.collection.Find(r=>r.id==id);
        public Record Stored(int id)=>Data.fish.Find(r=>r.id==id);
        void Load(){
            string json=persistence?PlayerPrefs.GetString(saveKey,""):"";
            bool migrated=false;
            try{data=FishingArchiveData.Decode(json,out migrated);}catch(Exception error){
                if(persistence&&!string.IsNullOrEmpty(json))PlayerPrefs.SetString(saveKey+".recovery",json);
                data=new FishingArchiveData();Debug.LogWarning("Fishing archive save could not be read; original JSON retained: "+error.Message);
            }
            if(persistence&&migrated&&!string.IsNullOrEmpty(json)&&!PlayerPrefs.HasKey(saveKey+".backup.v1"))PlayerPrefs.SetString(saveKey+".backup.v1",json);
        }
        FishCollectionRecord Entry(FishDefinition definition){var record=Discovery(definition.originalId);if(record==null){record=new FishCollectionRecord{id=definition.originalId};Data.collection.Add(record);}return record;}
        public void RecordLifted(FishDefinition definition,bool shiny){if(!definition)return;var r=Entry(definition);if(shiny)r.shinySeen=true;else r.originalSeen=true;r.lifted++;Write();}
        public void RecordEscaped(FishDefinition definition){if(!definition)return;var r=Entry(definition);r.escaped++;Write();}
        public void RecordAcquired(FishDefinition definition,bool shiny,float weight){if(!definition)return;var r=Entry(definition);if(shiny){r.shinySeen=true;r.shinyCaptured++;}else{r.originalSeen=true;r.originalCaptured++;}r.bestWeight=Mathf.Max(r.bestWeight,weight);Write();}
        public int BaitCount(int index)=>index==0?-1:Data.bait[Mathf.Clamp(index,1,4)];
        public bool UseBait(int index){if(index==0)return true;if(BaitCount(index)<=0)return false;Data.bait[index]--;Write();return true;}
        public void RestockBait(){for(int i=1;i<5;i++)Data.bait[i]=Mathf.Max(Data.bait[i],8);Write();}
        public bool Deposit(FishingCatch fish){
            if(!fish||!fish.IsDead||fish.Stored)return false;
            fish.MarkArchiveAcquired();var r=Stored(fish.definition.originalId);
            if(r==null){r=new Record{id=fish.definition.originalId,name=fish.definition.displayName};Data.fish.Add(r);}
            r.count++;if(fish.IsDrip)r.shinyCount++;else r.originalCount++;r.bestWeight=Mathf.Max(r.bestWeight,fish.weight);Data.captures++;Data.best=Mathf.Max(Data.best,fish.weight);
            fish.Stored=true;fish.item.Retire();Write();return true;
        }
        public bool Withdraw(int id){var record=Stored(id);if(record==null||record.count<=0)return false;return Withdraw(id,record.originalCount<=0);}
        public bool Withdraw(int id,bool shiny){var r=Stored(id);if(r==null||(shiny?r.shinyCount:r.originalCount)<=0)return false;if(shiny)r.shinyCount--;else r.originalCount--;r.count--;Write();return true;}
        public void Clear(){Data.fish.Clear();Write();}
        void Write(){if(persistence){PlayerPrefs.SetString(saveKey,JsonUtility.ToJson(Data));PlayerPrefs.Save();}Changed?.Invoke();}
    }
}
