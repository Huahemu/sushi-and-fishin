using System.Linq;
using TMPro;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // 关系 tab body (M3). One row per acquaintance: name, level label, a 5-step
    // bar and the latest thing that happened between the two.
    public sealed partial class FriendTabletPresenter
    {
        static readonly Color RelationStepOn=new Color32(226,120,84,255),RelationStepOff=new Color32(61,46,26,56);
        void RenderRelationsTab(Transform c,ref float y,PersonBinding person)
        {
            var runtime=FriendsLifeRuntime.Instance;int rows=0;
            foreach(var relationship in runtime.GetRelationships(person.personId).Where(x=>x.fromId==person.nativeResidentId))
            {
                var other=runtime.PersonFromNative(relationship.toId);if(other==null)continue;string id=other.personId;rows++;
                var row=FriendUi.Rect("Relation_"+id,c,0,y,750,112);
                RelationLine(FriendUi.Text("Name",row,0,0,300,40,other.displayName,24));
                RelationLine(FriendUi.Text("Label",row,310,0,280,40,RelationLabelText(person,other,relationship),24));
                int bar=FriendRelations.Bar(person.personId,id);
                for(int i=0;i<5;i++)FriendUi.Image("Step_"+i,row,i*46,48,40,14,null,false).color=i<bar?RelationStepOn:RelationStepOff;
                RelationLine(FriendUi.Text("Recent",row,0,70,570,34,FriendRelations.Recent(person.personId,id),20));
                FriendUi.Button("Pair_"+id,row,600,40,150,54,"查看",()=>{Push();pairId=id;page="Pair";Render();});y+=124;
            }
            if(rows==0)Field(c,ref y,"关系","还没有认识的人。");
        }
        static void RelationLine(TextMeshProUGUI text){text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Ellipsis;}
        // Label plus the original meter rank text, e.g. "好朋友 · 非常融洽".
        string RelationLabelText(PersonBinding from,PersonBinding to,LifeSimulationRelationship relation)
        {
            var rules=FriendsLifeRuntime.Instance?.Core?.Rules?.Data?.relation;
            int rank=rules?.rankMeterMax?.Length==6?NativeLifeRulesCalculations.RelationRank(relation.meter,rules):relation.nativeRank;
            string label=FriendRelations.Label(from.personId,to.personId),meter=texts.RelationRank(relation.kind,rank,relation.fighting,relation.oneSideLove);
            return string.IsNullOrEmpty(meter)||label=="争吵中"?label:label+" · "+meter;
        }
        // Pair page line (for the M5 pair page): "A → B" and the label line.
        string RelationPairText(PersonBinding from,PersonBinding to,LifeSimulationRelationship relation)
            =>relation==null?"":from.displayName+" → "+to.displayName+"\n"+RelationLabelText(from,to,relation);
    }
}
