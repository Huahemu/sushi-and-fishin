using System;
using System.Linq;
using Bozo.ModularCharacters;
using TomodachiLife;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendTabletPresenter
    {
        SourceQuestionnaireRuntime questionnaire;ResidentEditDocument questionDocument;int easyQuestion,easyStep;
        const string EasyText="ProgramMsg/Editor/MiiEditor/MiiEditorEasy.msbt.yml#";
        void StartQuestionnaire()
        {
            try
            {
                draft=photos.Create();questionnaire=new SourceQuestionnaireRuntime(FriendsLifeRuntime.Instance.Core);questionDocument=questionnaire.NewQuestionnaireDocument();
                easyQuestion=Array.FindIndex(questionnaire.Simulation.DailySource.questions,q=>q.id=="FaceAge");easyStep=1;page="Questionnaire";Render();
            }catch(Exception error){Debug.LogWarning("[FriendsLife] Questionnaire start: "+error);Status("问答制作无法开始，请重试。");}
        }
        void RenderQuestionnaire()
        {
            if(questionDocument==null){page="Add";RenderAdd();return;}var questions=questionnaire.Simulation.DailySource.questions;
            if(easyQuestion<0||easyQuestion>=questions.Length){FinishQuestionnaire();return;}
            var q=questions[easyQuestion];var p=Page("T04_SourcePicker","问答制作",true);string label;
            if(!texts.TryExact(EasyText+q.id,out label))
            {string key=q.id.StartsWith("Eyebrow")?"EyebrowAngle":q.id.StartsWith("HairFront")?"HairFront_S":q.id.StartsWith("Mouth")?"Mouth":"Beard";label=texts.ResolveExact(EasyText+key).text;}
            var question=FriendUi.Flexible(FriendUi.Text("Question",p.toolbar,0,0,1088,FriendUi.ToolbarH,label,30));question.textWrappingMode=TMPro.TextWrappingModes.NoWrap;question.enableAutoSizing=true;question.fontSizeMin=22;question.fontSizeMax=30;
            scroll=FriendUi.FitScroll("ScrollRows",p.body,1120,out var c);float y=0;int index=0;
            foreach(string answer in q.answers??Array.Empty<string>())
            {
                string id=answer;int ordinal=++index;string title=texts.TryExact(EasyText+id,out var original)?original:"样子 "+ordinal;
                SourceRow(c,ref y,title,()=>{questionnaire.ApplyQuestionnaireAnswer(questionDocument,q.id,id);AdvanceEasyQuestion(q,id);});
            }
            FriendUi.FitContent(c,y);FriendUi.FooterButton(p,"跳过",()=>AdvanceEasyQuestion(q,null),200);
            FriendUi.FooterButton(p,"生成并微调",FinishQuestionnaire,300,true);BackButton();
        }
        // Original IslandLifeHudCreator.AdvanceEasyQuestion transition semantics.
        void AdvanceEasyQuestion(TomodachiLife.Simulation.LifeEasyQuestion question,string answerId)
        {
            int index=answerId==null?-1:Array.IndexOf(question.answers,answerId);uint next=index>=0&&question.nextQuestions!=null&&index<question.nextQuestions.Length?question.nextQuestions[index]:question.nextDefault;
            int found=Array.FindIndex(questionnaire.Simulation.DailySource.questions,q=>q.type==next&&q.id!="Invalid");easyStep++;easyQuestion=found>=0?found:questionnaire.Simulation.DailySource.questions.Length;
            draft.nativeAvatarJson=JsonUtility.ToJson(questionDocument);photos.Save(draft);Render();
        }
        void FinishQuestionnaire()
        {
            try{questionDocument.Validate(questionnaire.Catalog);draft.nativeAvatarJson=JsonUtility.ToJson(questionDocument);draft.characterData=QuestionnaireBozoProjection.Project(questionDocument,questionnaire.Catalog);photos.Save(draft);page="Profile";BeginCreator(draft.characterData);}
            catch(Exception error){Debug.LogWarning("[FriendsLife] Questionnaire finish: "+error);Status("外貌生成失败，请重试。");}
        }
    }
    public static class QuestionnaireBozoProjection
    {
        // Display adapter only: native questionnaire selections remain verbatim in
        // ResidentEditDocument. Existing BoZo parts and morphs render those choices.
        // This table neither computes nor writes any social or preference result.
        public static CharacterData Project(ResidentEditDocument document,AvatarCatalog catalog)
        {
            var baseline=Resources.Load<CharacterObject>("Default");if(!baseline)throw new InvalidOperationException("缺少原角色模板");
            var data=JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(baseline.GetCharacterData()));
            var available=Resources.LoadAll<Outfit>("").Where(o=>o&&o.Type&&o.name.IndexOf("Anime",StringComparison.OrdinalIgnoreCase)<0&&(o.tags==null||!o.tags.Contains("Anime"))).OrderBy(o=>o.name,StringComparer.Ordinal).ToArray();
            foreach(var selection in document.appearance.parts.Where(p=>p.enabled))
            {
                string type=selection.category=="Faceline"?"Head":selection.category=="Hair"?"HairFront":selection.category=="Eye"?"Eyes":selection.category=="Eyebrow"?"EyeBrows":selection.category=="Glass"?"UpperFace":null;
                if(type==null)continue;var choices=available.Where(o=>o.Type.name==type).ToArray();if(choices.Length==0)continue;
                int sourceIndex=catalog.Part(selection.part).nativeIndex;var selected=choices[Math.Abs(sourceIndex)%choices.Length];string path=type+"/"+selected.name;
                int old=data.outfitDatas.FindIndex(o=>o.outfit.StartsWith(type+"/",StringComparison.Ordinal));var entry=old>=0?data.outfitDatas[old]:new OutfitData{colors=Enumerable.Repeat(Color.white,9).ToList(),pattern="",patternColors=Enumerable.Repeat(Color.white,3).ToList(),color=Color.white};entry.outfit=path;
                if(selection.category=="Hair"||selection.category=="Eyebrow")try{entry.colors[0]=catalog.Color("common",selection.color,false);}catch(ArgumentException){}
                if(old>=0)data.outfitDatas[old]=entry;else data.outfitDatas.Add(entry);
            }
            Set(data.bodyIDs,data.bodyShapes,"Weight",document.appearance.build/127f*100);
            int root=data.bodyModsKeys.IndexOf("root");if(root>=0&&root<data.bodyMods.Count){var mod=data.bodyMods[root];mod.scaleValue=Mathf.Lerp(.5f,2,document.appearance.height/127f);mod.scale=Vector3.one*mod.scaleValue;}
            Color skin=catalog.SkinColor(document.appearance,false);for(int i=0;i<data.outfitDatas.Count;i++){var outfit=data.outfitDatas[i];if(outfit.outfit.StartsWith("Head/")||outfit.outfit.StartsWith("Body/")){if(outfit.colors==null)outfit.colors=Enumerable.Repeat(Color.white,9).ToList();if(outfit.colors.Count>0)outfit.colors[0]=skin;data.outfitDatas[i]=outfit;}}
            foreach(var raw in document.neutralParameters){if(raw.kind=="Eye")Set(data.faceIDs,data.faceShapes,"IrisSize",raw.scale/7f*100);else if(raw.kind=="Mouth")Set(data.faceIDs,data.faceShapes,"MouthWide",raw.scale/8f*100);else if(raw.kind=="Eyebrow")Set(data.faceIDs,data.faceShapes,"BrowsThickness",raw.scale/8f*100);}
            return data;
        }
        static void Set(System.Collections.Generic.List<string> ids,System.Collections.Generic.List<float> values,string id,float value){int index=ids.IndexOf(id);if(index>=0&&index<values.Count)values[index]=Mathf.Clamp(value,0,100);}
    }
}
