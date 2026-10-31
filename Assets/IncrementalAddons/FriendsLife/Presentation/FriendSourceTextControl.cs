using System;
using System.Globalization;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.FriendsLife.SourceGraphs;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Concrete consumers for source text commands. A command with no recovered
    // host owner fails the source node; emitting an unobserved event is not success.
    public sealed class FriendSourceTextControl : MonoBehaviour
    {
        OriginalMotionRetargeter textMotion;
        bool ownsMotion;
        public string LastControl{get;private set;}
        public bool Apply(SourceTextResolver.Run run,string execution,ref string text,string archive,out string error)
        {
            error=null;LastControl=run.control;
            string Argument(string key)=>run.args!=null&&run.args.TryGetValue(key,out var value)?value:null;
            switch(run.control)
            {
                case "Speed":case "SpeedDirect":case "vtml_speed":return true; // Consumed by the host typewriter clock.
                case "Scale":
                    if(float.TryParse(Argument("percent"),NumberStyles.Float,CultureInfo.InvariantCulture,out var percent)&&percent>0)
                    {text+="<size="+percent.ToString(CultureInfo.InvariantCulture)+"%>";return true;}break;
                case "Size":
                    if(float.TryParse(Argument("size"),NumberStyles.Float,CultureInfo.InvariantCulture,out var size)&&size>0)
                    {text+="<size="+size.ToString(CultureInfo.InvariantCulture)+">";return true;}break;
                case "Color":
                    string color=Argument("name");
                    if(string.IsNullOrEmpty(color)||color=="~"){text+="</color>";return true;}
                    if(ColorUtility.TryParseHtmlString(color,out _)){text+="<color="+color+">";return true;}break;
                case "TelopOff":
                    var billboard=GetComponentInChildren<ResidentBillboardText>(true);if(billboard)billboard.Hide();text="";return true;
                case "EmotionShout":text+="<b>";return true;
                case "EmotionWhisper":text+="<size=75%>";return true;
                case "Expression":
                    var actor=GetComponent<ResidentController>();
                    if(actor&&FriendResidentExpressionProjection.TryPlay(actor,Argument("e"),2f))return true;break;
                case "AS":
                    string command=Argument("c");
                    if(string.IsNullOrEmpty(command))break;
                    textMotion=GetComponent<OriginalMotionRetargeter>();if(!textMotion)textMotion=gameObject.AddComponent<OriginalMotionRetargeter>();
                    if(!textMotion.PlaySource(command,execution+"/text/"+command,0,archive,out error))return false;
                    ownsMotion=true;return true;
            }
            error=error??"Original text control owner is unavailable: "+run.control;
            return false;
        }
        void Update()
        {
            if(ownsMotion&&textMotion&&(!textMotion.IsPlaying||textMotion.Finished)){textMotion.Stop();ownsMotion=false;}
        }
        public void EndLine(){if(ownsMotion&&textMotion&&textMotion.Finished){textMotion.Stop();ownsMotion=false;}}
        void OnDisable(){if(ownsMotion&&textMotion)textMotion.Stop();ownsMotion=false;}
    }
}
