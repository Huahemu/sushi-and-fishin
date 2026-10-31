using System;
using System.Threading.Tasks;
using Bozo.ModularCharacters;
using Newtonsoft.Json.Linq;
using PaperPixelGames.Framework.Enums;
using SushiAndFish.ResidentialIsland;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendTabletPresenter
    {
        async Task<FriendsWorldResponse> SendProfileCommand(FriendsWorldCommand command)
        {
            var runtime=FriendsLifeRuntime.Instance;var transport=FriendsLifeNetwork.Instance;if(!transport)throw new InvalidOperationException("正在连接，请稍后再试。");
            command.commandId=command.commandId??Guid.NewGuid().ToString("N");command.actorPersonId=runtime.LocalHuman()?.personId;
            var pending=new TaskCompletionSource<FriendsWorldResponse>();Action<FriendsWorldResponse> listener=response=>{if(response.commandId==command.commandId)pending.TrySetResult(response);};transport.ResponseReceived+=listener;
            try
            {
                transport.Submit(command);float until=Time.realtimeSinceStartup+120;while(!pending.Task.IsCompleted&&Time.realtimeSinceStartup<until)await Task.Yield();
                if(!pending.Task.IsCompleted)throw new InvalidOperationException("同步超时，草稿已保存。");return pending.Task.Result;
            }finally{transport.ResponseReceived-=listener;}
        }
        async Task SaveRemoteProfile()
        {
            var workingDraft=draft;string targetPersonId=workingDraft?.personId??personId;var runtime=FriendsLifeRuntime.Instance;
            string profileName=nameField.text,profileJson=editNativeJson,profilePhrase=phraseField.text,profileHobby=hobbyField.text;
            int.TryParse(ageField.text,out int age);string familyJson=new JObject{["ageYears"]=age,["gender"]=profileGender,["preference"]=profilePreference}.ToString(Newtonsoft.Json.Formatting.None);
            if(workingDraft==null)
            {
                var response=await SendProfileCommand(new FriendsWorldCommand{kind="profile",targetPersonId=targetPersonId,displayName=profileName,nativeProfileJson=profileJson,catchphrase=profilePhrase,hobby=profileHobby});
                if(!response.accepted)throw new InvalidOperationException(response.message);runtime.SubmitSourceAction(targetPersonId,"profile-fields",familyJson);if(ReferenceEquals(draft,workingDraft)&&personId==targetPersonId){page="Detail";Render();}return;
            }
            SaveDraftFields();if(workingDraft.characterData==null)throw new InvalidOperationException("请先完成外貌");var appearanceData=NativePhotoParameterFit.Copy(workingDraft.characterData);string appearanceJson=JsonUtility.ToJson(appearanceData);
            string displayName=workingDraft.displayName,nativeJson=workingDraft.nativeAvatarJson,phrase=workingDraft.catchphrase,hobby=workingDraft.hobby;bool editingExisting=workingDraft.editingExisting,create=string.IsNullOrEmpty(targetPersonId);
            if(string.IsNullOrEmpty(workingDraft.createCommandId))workingDraft.createCommandId=Guid.NewGuid().ToString("N");photos.Save(workingDraft);string createCommandId=workingDraft.createCommandId;
            var submittedSource=JsonUtility.FromJson<PhotoFriendDraft>(JsonUtility.ToJson(workingDraft));
            if(!await FriendPhotoPackageNetwork.ShareIfGenerated(appearanceData))throw new InvalidOperationException("同步失败，草稿已保存。");
            var response2=await SendProfileCommand(new FriendsWorldCommand{commandId=create?createCommandId:null,kind=create?"create":"profile",targetPersonId=targetPersonId,displayName=displayName,nativeProfileJson=nativeJson,appearanceJson=appearanceJson,catchphrase=phrase,hobby=hobby});
            if(!string.IsNullOrEmpty(response2.personId)){targetPersonId=response2.personId;workingDraft.personId=targetPersonId;photos.Save(workingDraft);}
            if(!response2.accepted)throw new InvalidOperationException(response2.message);
            float until=Time.realtimeSinceStartup+15;while(runtime.GetPerson(targetPersonId)==null&&Time.realtimeSinceStartup<until)await Task.Yield();if(runtime.GetPerson(targetPersonId)==null)throw new InvalidOperationException("同步失败，草稿已保存。");
            if(!create){var appearance=await SendProfileCommand(new FriendsWorldCommand{kind="appearance",targetPersonId=targetPersonId,appearanceJson=appearanceJson});if(!appearance.accepted)throw new InvalidOperationException(appearance.message);}
            NativePhotoSourceStore.Commit(submittedSource,targetPersonId,appearanceData,null,runtime.GetPerson(targetPersonId)?.appearanceRevision??0);
            if(editingExisting){photos.RemoveDraft(workingDraft.draftId);if(ReferenceEquals(draft,workingDraft)){personId=targetPersonId;page="Detail";draft=null;Render();}return;}
            runtime.SubmitSourceAction(targetPersonId,"profile-fields",familyJson);workingDraft.stage=PhotoFriendStage.AwaitingHome;photos.Save(workingDraft);if(!ReferenceEquals(draft,workingDraft))return;personId=targetPersonId;Navigation.PushVisit(new UiReturnFrame{page="Detail",personId=targetPersonId,tab="资料",purpose="Arrival"});FriendTabletAccess.OpenMap(tablet);FriendHousingUI.BeginArrival(targetPersonId);
        }
        async Task<bool> CommitAppearance(string id,CharacterData data,PhotoFriendDraft workingDraft)
        {
            string targetPersonId=id;var appearanceData=NativePhotoParameterFit.Copy(data);var runtime=FriendsLifeRuntime.Instance;string previous=runtime.GetPerson(targetPersonId)?.appearanceJson;bool accepted;
            string json=JsonUtility.ToJson(appearanceData);if(workingDraft!=null&&PhotoFriendPipeline.IsNative(workingDraft)&&workingDraft.appearanceCommandJson!=json){workingDraft.appearanceCommandJson=json;workingDraft.appearanceCommandId=Guid.NewGuid().ToString("N");photos.Save(workingDraft);}
            string commandId=workingDraft?.appearanceCommandId;var submittedSource=workingDraft==null?null:JsonUtility.FromJson<PhotoFriendDraft>(JsonUtility.ToJson(workingDraft));
            if(!await FriendPhotoPackageNetwork.ShareIfGenerated(appearanceData))throw new InvalidOperationException("同步失败，草稿已保存。");
            if(runtime.IsAuthority)accepted=previous==json||runtime.SetAppearance(targetPersonId,json);else{var response=await SendProfileCommand(new FriendsWorldCommand{commandId=commandId,kind="appearance",targetPersonId=targetPersonId,appearanceJson=json});if(!response.accepted)throw new InvalidOperationException(response.message);accepted=true;}
            if(accepted)NativePhotoSourceStore.Commit(submittedSource,targetPersonId,appearanceData,previous,runtime.GetPerson(targetPersonId)?.appearanceRevision??0);return accepted;
        }
    }
}
