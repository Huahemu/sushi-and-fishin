using System;
using System.IO;
using UnityEngine;

namespace SushiAndFish.FishingIntegration {
    [Serializable] public sealed class PlayerResumeData {
        public int version=1,slot;
        public string host,epoch,updatedUtc;
        public ulong steam;
        public bool valid,swimming,onBoat;
        public Vector3 position,boatLocalPosition;
        public Quaternion rotation=Quaternion.identity;
        public float pitch;
    }
    public static class PlayerResumeFiles {
        public static bool ValidIdentity(string host,int slot,ulong steam)=>ulong.TryParse(host,out var hostId)&&hostId!=0&&slot>=0&&slot<1000&&steam!=0;
        public static string Key(string host,int slot,ulong steam){if(!ValidIdentity(host,slot,steam))throw new ArgumentException("Invalid player resume identity");return host+"_"+slot+"_"+steam;}
        public static string PathFor(string root,string host,int slot,ulong steam)=>Path.Combine(root,Key(host,slot,steam)+".json");
        public static string EpochPath(string root,string host,int slot){if(!ValidIdentity(host,slot,1))throw new ArgumentException("Invalid world identity");return Path.Combine(root,host+"_"+slot+".epoch");}
        public static string Epoch(string root,string host,int slot){string path=EpochPath(root,host,slot);return File.Exists(path)?File.ReadAllText(path):"";}
        public static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)&&Mathf.Abs(p.x)<10000&&Mathf.Abs(p.y)<10000&&Mathf.Abs(p.z)<10000;
        public static bool Valid(PlayerResumeData data,string host,int slot,ulong steam){
            if(data==null||data.version!=1||!data.valid||data.host!=host||data.slot!=slot||data.steam!=steam||!Finite(data.position)||!float.IsFinite(data.pitch))return false;
            var q=data.rotation;float norm=Quaternion.Dot(q,q);return float.IsFinite(norm)&&norm>.99f&&norm<1.01f;
        }
        public static PlayerResumeData Read(string root,string host,int slot,ulong steam,out bool protectedFile){
            protectedFile=false;string path=PathFor(root,host,slot,steam);if(!File.Exists(path))return null;
            try{
                var data=JsonUtility.FromJson<PlayerResumeData>(File.ReadAllText(path));
                if(data!=null&&data.version!=1){protectedFile=true;return null;}
                if(!Valid(data,host,slot,steam))throw new InvalidDataException("Invalid player resume pose");
                return (data.epoch??"")==Epoch(root,host,slot)?data:null;
            }catch(Exception error){protectedFile=true;Debug.LogWarning("Player resume file retained unchanged: "+error.Message);return null;}
        }
        public static void WriteAtomic(string path,string text){
            Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temp,text);if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
