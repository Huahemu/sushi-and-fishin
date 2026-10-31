using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using TomodachiLife;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendTabletPresenter
    {
        ResidentEditDocument profileDocument;JObject profileEnvelope;TMP_InputField ageField,readingField,birthdayField;
        string profileGender="Other",profilePreference="Anyone";
        readonly Dictionary<string,TMP_InputField> nativeProfileFields=new Dictionary<string,TMP_InputField>();
        void RenderProfileFields()
        {
            var runtime=FriendsLifeRuntime.Instance;var person=draft==null?runtime?.GetPerson(personId):null;var resident=person==null?null:runtime.Core.FindResident(person.nativeResidentId);
            editNativeJson=resident?.avatarJson??draft?.nativeAvatarJson??"";
            try{profileEnvelope=string.IsNullOrEmpty(editNativeJson)?new JObject():JObject.Parse(editNativeJson);profileDocument=string.IsNullOrEmpty(editNativeJson)?null:JsonUtility.FromJson<ResidentEditDocument>(editNativeJson);}catch(Exception){profileDocument=null;profileEnvelope=new JObject();}
            if(profileDocument?.appearance==null){profileDocument=new SourceQuestionnaireRuntime(runtime.Core).NewQuestionnaireDocument();profileEnvelope=JObject.Parse(JsonUtility.ToJson(profileDocument));}
            var family=person==null?null:runtime.Core.State.family.profiles.FirstOrDefault(f=>f.residentId==person.nativeResidentId);profileGender=family?.gender??(string)profileEnvelope["profileMetadata"]?["gender"]??"Other";profilePreference=family?.romancePreference??(string)profileEnvelope["profileMetadata"]?["preference"]??"Anyone";
            // Preview on the left (380 x 456), fields scroll on the right, 保存 and 返回 in the footer.
            var form=Page("T08_ProfileForm",person==null?"朋友资料":"修改资料");var raw=FriendUi.Rect("Preview",form.body,0,0,380,456).gameObject.AddComponent<UnityEngine.UI.RawImage>();FriendUi.UseTabletGraphic(raw);if(person!=null)FriendPortraitCache.Bind(raw,personId);else if(draft!=null)FriendPortraitCache.Bind(raw,draft);raw.raycastTarget=false;
            var fields=FriendUi.Band("FieldsColumn",form.body,Vector2.zero,Vector2.one,new Vector2(404,0),Vector2.zero);
            scroll=FriendUi.FitScroll("Fields",fields,680,out var c);float y=0;nativeProfileFields.Clear();
            nameField=ProfileInput(c,ref y,"姓名",person?.displayName??draft?.displayName??"");readingField=ProfileInput(c,ref y,"读法",(string)profileEnvelope["profileMetadata"]?["reading"]??"");
            ageField=ProfileInput(c,ref y,"年龄",(family?.ageYears??(int?)profileEnvelope["profileMetadata"]?["ageYears"]??20).ToString());birthdayField=ProfileInput(c,ref y,"生日",(string)profileEnvelope["profileMetadata"]?["birthday"]??"");
            var gender=FriendUi.Button("Gender",c,0,y,330,56,GenderLabel(profileGender),null);gender.Clicked+=()=>{profileGender=profileGender=="Other"?"Man":profileGender=="Man"?"Woman":"Other";gender.SetText(GenderLabel(profileGender));};
            var preference=FriendUi.Button("Preference",c,350,y,330,56,PreferenceLabel(profilePreference),null);preference.Clicked+=()=>{profilePreference=profilePreference=="Anyone"?"Man":profilePreference=="Man"?"Woman":"Anyone";preference.SetText(PreferenceLabel(profilePreference));};y+=76;
            phraseField=ProfileInput(c,ref y,"口头禅",resident?.catchphrase??draft?.catchphrase??"");hobbyField=ProfileInput(c,ref y,"爱好",resident?.hobby??draft?.hobby??"");
            var appearance=profileDocument.appearance;
            foreach(var row in new[]{new[]{"walkStyle","步态",appearance.walkStyle.ToString()},new[]{"personalityEnergy","活跃",appearance.personalityEnergy.ToString()},new[]{"personalitySpeech","说话",appearance.personalitySpeech.ToString()},new[]{"personalityExpression","表情",appearance.personalityExpression.ToString()},new[]{"personalityMood","心情",appearance.personalityMood.ToString()},new[]{"personalityManners","处事",appearance.personalityManners.ToString()},new[]{"voicePitch","音高",appearance.voicePitch.ToString()},new[]{"voiceSpeed","语速",appearance.voiceSpeed.ToString()},new[]{"voiceTone","音色",appearance.voiceTone.ToString()}})
                nativeProfileFields[row[0]]=ProfileInput(c,ref y,row[1],row[2]);
            FriendUi.Button("VoicePreview",c,0,y,680,56,"试听声音",PreviewVoice);y+=76;FriendUi.Button("Appearance",c,0,y,680,56,"修改外貌",()=>{CaptureNativeProfile();SaveDraftFields();if(draft!=null)BeginCreator(draft.characterData);else EditAppearance(person);});y+=76;FriendUi.FitContent(c,y);
            BackButton();FriendUi.FooterButton(form,person==null?"安排住所":"保存",SaveProfile,300,true);
        }
        TMP_InputField ProfileInput(Transform parent,ref float y,string label,string value){FriendUi.Text("Label_"+label,parent,0,y,150,56,label,24);var field=FriendUi.Input("Field_"+label,parent,160,y,520,56,value);y+=76;return field;}
        string GenderLabel(string id)=>id=="Man"?"性别：男":id=="Woman"?"性别：女":"性别：其他";
        string PreferenceLabel(string id)=>id=="Man"?"偏好：男":id=="Woman"?"偏好：女":"偏好：不限";
        void CaptureNativeProfile()
        {
            profileDocument.name=nameField.text;profileDocument.catchphrase=phraseField.text;profileDocument.hobby=hobbyField.text;
            foreach(var row in nativeProfileFields){if(!int.TryParse(row.Value.text,out var number))continue;var field=typeof(ResidentAppearance).GetField(row.Key);field.SetValue(profileDocument.appearance,Mathf.Clamp(number,0,row.Key.StartsWith("voice")?100:4));}
            int.TryParse(ageField.text,out var age);var native=JObject.Parse(JsonUtility.ToJson(profileDocument));native["profileMetadata"]=new JObject{["reading"]=readingField.text,["birthday"]=birthdayField.text,["ageYears"]=age,["gender"]=profileGender,["preference"]=profilePreference};profileEnvelope=native;editNativeJson=native.ToString(Newtonsoft.Json.Formatting.None);
            if(draft!=null){draft.nativeAvatarJson=editNativeJson;photos.Save(draft);}
        }
        void ApplyFamilyFields(string id)
        {int.TryParse(ageField.text,out var age);FriendsLifeRuntime.Instance.SubmitSourceAction(id,"profile-fields",new JObject{["ageYears"]=age,["gender"]=profileGender,["preference"]=profilePreference}.ToString(Newtonsoft.Json.Formatting.None));}
        void PreviewVoice()
        {
            CaptureNativeProfile();string words=string.IsNullOrWhiteSpace(phraseField.text)?nameField.text:phraseField.text;if(string.IsNullOrWhiteSpace(words)){Status("请输入姓名或口头禅");return;}
            var audio=UnityEngine.Object.FindFirstObjectByType<LifeAudioRuntime>();if(!audio){var go=new GameObject("Friend Voice Preview");audio=go.AddComponent<LifeAudioRuntime>();audio.musicEnabled=false;}audio.Speak(words,profileDocument.appearance);
        }
    }
}
