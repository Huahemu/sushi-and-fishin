using System;
using System.Linq;
using TomodachiLife;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Receiver dependencies only. The two method bodies below are the original
    // IslandLifeGameFeatures.cs:33-62, with their original names and arithmetic.
    // No reference world, Mii model, camera, scene or separate save is installed.
    public sealed class SourceQuestionnaireRuntime
    {
        public LifeSimulationRuntime Simulation;
        readonly AvatarCatalog catalog;
        public AvatarCatalog Catalog=>catalog;
        public SourceQuestionnaireRuntime(LifeSimulationRuntime simulation){Simulation=simulation;catalog=AvatarCatalog.Load();}
        public ResidentEditDocument NewQuestionnaireDocument()=>NativeSdkPresets.CreateDocument("SdkEditorMale",catalog);
        public void ApplyQuestionnaireAnswer(ResidentEditDocument document,string questionId,string answerId)
        {
            var answer=Simulation.DailySource.answers.FirstOrDefault(a=>a.id==answerId);if(answer==null)return;
            foreach(var candidate in answer.candidates??Array.Empty<TomodachiLife.Simulation.LifeEasyCandidate>())
            {
                if(candidate.type==3428438567)
                {
                    if(candidate.partType==780281636){document.appearance.skinColorDomain="game";document.appearance.skinColor=candidate.param;continue;}
                    var colorPart=catalog.parts.FirstOrDefault(p=>p.typeHash==candidate.partType);var colorSelection=colorPart==null?null:document.appearance.Selection(colorPart.category);
                    if(colorSelection!=null){colorSelection.color=candidate.param;if(colorPart.category=="Eye")colorSelection.subColor=candidate.param;}continue;
                }
                var slot=catalog.parts.FirstOrDefault(p=>p.typeHash==candidate.partType)?.category;
                var raw=document.neutralParameters.FirstOrDefault(p=>p.kind==slot);
                if(raw!=null)
                {
                    if(candidate.type==2725165)raw.x=Mathf.Clamp(raw.x+candidate.param,0,slot=="Eye"?12:slot=="Eyebrow"?14:0);
                    if(candidate.type==923381552)raw.y=Mathf.Clamp(raw.y+candidate.param,slot=="Eyebrow"?3:0,18);
                    if(candidate.type==1797173052)raw.scale=Mathf.Clamp(raw.scale+candidate.param,0,slot=="Eye"?7:8);
                    if(candidate.type==1773371631)raw.rotation=Mathf.Clamp(raw.rotation+candidate.param,0,slot=="Eye"?7:slot=="Eyebrow"?11:31);
                    if(candidate.type==2494138173)raw.aspect=Mathf.Clamp(raw.aspect+candidate.param,0,6);
                }
                var part=catalog.parts.FirstOrDefault(p=>p.id==candidate.part);if(part==null)continue;
                var selection=document.appearance.Selection(part.category);if(selection==null)document.appearance.parts=document.appearance.parts.Concat(new[]{AvatarSelection.From(part)}).ToArray();
                else{selection.part=part.id;selection.enabled=true;selection.positionX=part.defaultX;selection.positionY=part.defaultY;selection.scale=part.defaultScale;selection.aspect=part.defaultAspect;}
            }
            if(questionId.Contains("Height"))document.appearance.height=Mathf.Clamp((Array.IndexOf(Simulation.DailySource.questions.First(q=>q.id==questionId).answers,answerId)+1)*22,0,127);
            if(questionId.Contains("Body"))document.appearance.build=Mathf.Clamp((Array.IndexOf(Simulation.DailySource.questions.First(q=>q.id==questionId).answers,answerId)+1)*22,0,127);
            document.parameterSource+="; questionnaire "+questionId+"="+answerId;
        }
    }
}
