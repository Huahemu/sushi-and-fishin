using System;
using System.IO;
using System.Linq;
using Bozo.ModularCharacters;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class NativePhotoSourceRecord
    {
        public string version,personId,appearanceJson,previousAppearanceJson,sourcePath,maskPath,cutoutPath,fitJson,legacyPackageId,legacyDraftId,commitUtc;
        public int observedAppearanceRevision,inputRevision;
    }
    // Photo provenance is separate from resident identity/economy and legacy packages.
    public static class NativePhotoSourceStore
    {
        static string Root=>PhotoFriendPaths.Folder("NativePhotoSources");
        public static bool LegacyArchived(string id)=>File.Exists(Path.Combine(Root,"ArchivedLegacyDrafts",id+".txt"));
        public static void ArchiveLegacy(string id)
        {if(!Guid.TryParseExact(id,"N",out _))throw new InvalidDataException("草稿编号无效");string dir=Path.Combine(Root,"ArchivedLegacyDrafts");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,id+".txt"),"Legacy payload retained; hidden from active drafts. No external request retried.");}
        public static void Commit(PhotoFriendDraft draft,string personId,CharacterData appearance,string previousJson,int observedRevision)
        {
            if(draft==null||!PhotoFriendPipeline.IsNative(draft)||string.IsNullOrEmpty(draft.sourcePath)||!File.Exists(draft.sourcePath))return;
            if(!Guid.TryParseExact(personId,"N",out _))throw new InvalidDataException("朋友编号无效");NativePhotoParameterFit.Validate(appearance);
            string dir=Path.Combine(Root,personId,"revision-"+observedRevision+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            Func<string,string,string> copy=(source,name)=>{if(string.IsNullOrEmpty(source)||!File.Exists(source))return null;string target=Path.Combine(dir,name);File.Copy(source,target,false);return target;};
            var record=new NativePhotoSourceRecord{version=NativePhotoParameterFit.Version,personId=personId,appearanceJson=JsonUtility.ToJson(appearance),previousAppearanceJson=previousJson,observedAppearanceRevision=observedRevision,inputRevision=draft.inputRevision,legacyPackageId=draft.legacyPackageId,legacyDraftId=draft.legacyDraftId,sourcePath=copy(draft.sourcePath,"source.png"),maskPath=copy(draft.maskPath,"mask.png"),cutoutPath=copy(draft.cutoutPath,"cutout.png"),fitJson=draft.nativeFitJson,commitUtc=DateTime.UtcNow.ToString("O")};
            string json=JsonUtility.ToJson(record,true);File.WriteAllText(Path.Combine(dir,"record.json"),json);string latest=Path.Combine(Root,personId,"latest.json"),writing=latest+".writing";File.WriteAllText(writing,json);if(File.Exists(latest))File.Replace(writing,latest,null);else File.Move(writing,latest);
        }
        public static bool TryRead(string personId,CharacterData appearance,out byte[] image,out byte[] mask,out string legacyPackage)
        {
            image=mask=null;legacyPackage=null;
            if(!string.IsNullOrEmpty(personId)&&Guid.TryParseExact(personId,"N",out _))
            {
                string file=Path.Combine(Root,personId,"latest.json");if(File.Exists(file)){var record=JsonUtility.FromJson<NativePhotoSourceRecord>(File.ReadAllText(file));if(record!=null&&File.Exists(record.sourcePath)){image=File.ReadAllBytes(record.sourcePath);if(File.Exists(record.maskPath))mask=File.ReadAllBytes(record.maskPath);legacyPackage=record.legacyPackageId;return true;}}
            }
            var generated=appearance?.outfitDatas?.FirstOrDefault(o=>(o.outfit??"").Contains("/FriendsGenerated/"));string path=generated?.outfit;if(string.IsNullOrEmpty(path))return false;var parts=path.Split('/');if(parts.Length!=4||!Guid.TryParseExact(parts[2],"N",out _))return false;
            var package=JObject.Parse(FriendGeneratedModels.ExportPackage(parts[2]));string source=(string)package["sourcePhotoPng"],alpha=(string)package["sourceMaskPng"];if(string.IsNullOrEmpty(source))return false;
            image=Convert.FromBase64String(source);if(image.Length>32*1024*1024)throw new InvalidDataException("旧照片过大");if(!string.IsNullOrEmpty(alpha))mask=Convert.FromBase64String(alpha);legacyPackage=parts[2];return true;
        }
    }
}
