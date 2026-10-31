using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    public static class OriginalPrimitiveBindings
    {
        const string Source="Original AI.Product.100 typed node inputs; upper graph/data binding";
        public static void Register(OriginalNodeRegistry registry)
        {
            registry.Action("NullAction",(c,dt)=>OriginalNodeStatus.Success,Source);
            registry.Action("NoResultAction",(c,dt)=>OriginalNodeStatus.Running,Source);
            registry.Action("LoopNullAction",(c,dt)=>OriginalNodeStatus.Running,Source);
            registry.Action("WaitSecAction",C004NativeWaitSecBindings.Execute,C004NativeWaitSecBody.Source+"; actual correlated frame producer and persisted generic-node updated flag");
            registry.Action("WriteDemoResultAction",(c,dt)=>{c.Runner.State.result=c.Input("Key").AsText();c.Runner.State.resultWritten=true;return OriginalNodeStatus.Success;},Source);
            registry.Action("DemoForkStrErrorAction",(c,dt)=>throw new OriginalBindingException(c.Address+": original DemoForkStr error output"),Source);
            registry.Action("AppErrorAction",(c,dt)=>throw new OriginalBindingException(c.Address+": original AppError action"),Source);
            foreach(string name in new[]{"WriteActorToBlackboardAction","WriteActorListToBlackboardAction","WriteBoolToBlackboardAction","WriteF32ToBlackboardAction","WriteS32ToBlackboardAction","WriteStringToBlackboardAction","WriteV3fToBlackboardAction"})
                registry.Action(name,(c,dt)=>{var p=c.Node.inputs??Array.Empty<OriginalParameter>();if(p.Length!=1)throw new OriginalBindingException(c.Address+": expected the authored single write value");c.Blackboard(c.Property("Prop_Key").AsText(),c.Input(p[0].name));return OriginalNodeStatus.Success;},Source);
            registry.Query("GetDemoForkStrQuery",(c,o)=>OriginalValue.String(c.Runner.ForkString((int)c.Property("Prop_DemoForkStrType").AsNumber())),Source);
            registry.Query("NotQuery",(c,o)=>OriginalValue.Bool(!c.Input("Bool").AsBool()),Source);
            registry.Query("FloatQuery",(c,o)=>c.Input("Float"),Source);
            registry.Query("IntToFloatQuery",(c,o)=>OriginalValue.Number((float)c.Input("Int").AsNumber()),Source);
            registry.Query("AddS32Query",(c,o)=>OriginalValue.Number(unchecked((int)c.Input("ValueA").AsNumber()+(int)c.Input("ValueB").AsNumber())),Source);
            registry.Query("MultiplyFloatQuery",(c,o)=>OriginalValue.Number((float)c.Input("Float").AsNumber()*(float)c.Input("Power").AsNumber()),Source);
            registry.Query("DivideFloatQuery",(c,o)=>OriginalValue.Number((float)c.Input("Dividend").AsNumber()/(float)c.Input("Divisor").AsNumber()),Source);
            registry.Query("SameElementsVector3Query",(c,o)=>OriginalValue.Vector(Vector3.one*(float)c.Input("Float").AsNumber()),Source);
            registry.Query("FloatToVectorQuery",(c,o)=>OriginalValue.Vector(new Vector3((float)c.Input("X").AsNumber(),(float)c.Input("Y").AsNumber(),(float)c.Input("Z").AsNumber())),Source);
            registry.Query("VectorToFloatQuery",(c,o)=>{var v=c.Input("Vector3").AsVector();return OriginalValue.Number(o=="x"?v.x:o=="y"?v.y:o=="z"?v.z:throw new OriginalBindingException(c.Address+": vector component "+o));},Source);
            registry.Query("VectorLengthQuery",(c,o)=>OriginalValue.Number(c.Input("Vector3").AsVector().magnitude),Source);
            registry.Query("InverceVectorQuery",(c,o)=>OriginalValue.Vector(-c.Input("Direction").AsVector()),Source);
            registry.Query("Vector3NormalizeOrEz",(c,o)=>{var v=c.Input("Vector3").AsVector();return OriginalValue.Vector(v.sqrMagnitude==0?Vector3.forward:v.normalized);},Source);
            registry.Query("MultiplyVec3Query",(c,o)=>OriginalValue.Vector(c.Input("Vector3").AsVector()*(float)c.Input("Power").AsNumber()),Source);
            registry.Query("CompareVector3Query",(c,o)=>{var a=c.Input("A").AsVector();var b=c.Input("B").AsVector();return OriginalValue.Bool(a.x==b.x&&a.y==b.y&&a.z==b.z);},Source);
            registry.Query("FloatCompareQuery",(c,o)=>{float v=(float)c.Input("Value").AsNumber(),t=(float)c.Input("Threshold").AsNumber();return OriginalValue.Bool(o=="IsEqual"?v==t:o=="IsGreater"?v>t:o=="IsLess"?v<t:throw new OriginalBindingException(c.Address+": comparison output "+o));},Source);
            registry.Query("OverwriteVectorQuery",(c,o)=>{var v=c.Input("Vector3").AsVector();if(c.Input("IsOverwriteX").AsBool())v.x=(float)c.Input("X").AsNumber();if(c.Input("IsOverwriteY").AsBool())v.y=(float)c.Input("Y").AsNumber();if(c.Input("IsOverwriteZ").AsBool())v.z=(float)c.Input("Z").AsNumber();return OriginalValue.Vector(v);},Source);
            registry.Query("RotateVecAroundCenterVecQuery",(c,o)=>OriginalValue.Vector(Quaternion.AngleAxis((float)c.Input("RotDeg").AsNumber(),c.Input("CenterDir").AsVector())*c.Input("Vec3").AsVector()),Source);
            registry.Query("LerpVector3Query",(c,o)=>OriginalValue.Vector(Vector3.LerpUnclamped(c.Input("VecA").AsVector(),c.Input("VecB").AsVector(),(float)c.Input("Ratio").AsNumber())),Source);
            registry.Query("LerpFloatQuery",(c,o)=>OriginalValue.Number(Mathf.LerpUnclamped((float)c.Input("Value0").AsNumber(),(float)c.Input("Value1").AsNumber(),(float)c.Input("Ratio").AsNumber())),Source);
            registry.Query("CalcRatioF32Query",(c,o)=>OriginalValue.Number(Mathf.LerpUnclamped((float)c.Input("Val0").AsNumber(),(float)c.Input("Val1").AsNumber(),(float)c.Input("Ratio").AsNumber())),Source);
            registry.Query("CalcRatioV3fQuery",(c,o)=>OriginalValue.Vector(Vector3.LerpUnclamped(c.Input("Val0").AsVector(),c.Input("Val1").AsVector(),(float)c.Input("Ratio").AsNumber())),Source);
            registry.Query("SlerpDirQuery",(c,o)=>OriginalValue.Vector(Vector3.SlerpUnclamped(c.Input("Dir0").AsVector(),c.Input("Dir1").AsVector(),(float)c.Input("Ratio").AsNumber())),Source);
            registry.Query("IsActorListLargerThanNumQuery",(c,o)=>OriginalValue.Bool(c.Input("ActorHandleList").ActorIds().Length>c.Input("Num").AsNumber()),Source);
            registry.Query("GetIndexActorQuery",(c,o)=>
            {
                var all=c.Input("ActorHandleList").ActorIds();int index=(int)c.Input("Index").AsNumber();
                if(index>=0&&index<all.Length)return OriginalValue.Actor(all[index]);
                if(c.Property("Prop_AllowNullOutput").AsBool())return OriginalValue.Null();
                throw new OriginalBindingException(c.Address+": original actor list index outside available participants: "+index);
            },Source);
            registry.Query("CreateSlaveActorListQuery",(c,o)=>OriginalValue.Actors(Array.Empty<string>()),Source);
            registry.Query("AddActorToActorListQuery",(c,o)=>
            {
                var list=c.Input("ActorHandleList");var actor=c.Input("ActorHandle");
                if(list.kind!="Actors")throw new OriginalBindingException(c.Address+": source actor-list owner is not bound");
                list.text=string.Join(",",list.ActorIds().Concat(actor.ActorIds()));return list;
            },Source);
            registry.Query("CreateMessageParamQuery",(c,o)=>c.NewBag("MessageParam"),Source);
            registry.Query("AnimationCommandParamQuery",(c,o)=>c.NewBag("AnimationCommandParam"),Source);
            registry.Query("CreateXLinkEmitArgQuery",(c,o)=>c.NewBag("XLinkEmitArg"),Source);
            registry.Query("CreateXLinkEmitOptionSettingEffectFadeInSecQuery",(c,o)=>NewBag(c,"XLinkEmitOption","Sec"),Source);
            registry.Query("StringToHoldStyleIdQuery",(c,o)=>OriginalValue.String(c.Input("HoldStyleKey").AsText()),Source);
            registry.Query("MakeM34fQuery",(c,o)=>NewBag(c,"M34f","Dir","Pos"),Source);
            registry.Query("SetTalkingMiiListToMessageParamQuery",(c,o)=>UpdateBag(c,"MessageParam","ActorHandleList"),Source);
            registry.Query("SetNumberToMessageParamQuery",(c,o)=>
            {
                var value=c.Input("MessageParam");var bag=c.Bag(value);c.Runner.Set(bag.values,"Number"+(int)c.Input("Slot").AsNumber(),c.Input("Num"));return value;
            },Source);
            registry.Query("SetMonthToMessageParamQuery",(c,o)=>UpdateBag(c,"MessageParam","Month"),Source);
            registry.Query("SetDayToMessageParamQuery",(c,o)=>UpdateBag(c,"MessageParam","Day"),Source);
            registry.Query("CopyMessageParamQuery",(c,o)=>
            {
                var from=c.Bag(c.Input("FromMessageParam"));var value=c.Input("ToMessageParam");var to=c.Bag(value);
                foreach(var pair in from.values)c.Runner.Set(to.values,pair.key,pair.value.Copy());return value;
            },Source);
            var setters=new Dictionary<string,string[]>
            {
                {"SetIgnoreMorphStartToAnimationCommanQuery",new[]{"AnimationCommandParam"}},
                {"SetOptionToXLinkEmitArgQuery",new[]{"XLinkEmitArg","XLinkEmitOption"}},
                {"SetItemIconToXLinkEmitArgQuery",new[]{"XLinkEmitArg","ItemId","ItemType"}},
                {"SetMiiIconToXLinkEmitArgQuery",new[]{"XLinkEmitArg","MiiIconType","ActorHandle"}},
                {"SetFaceMaskToXLinkEmitArgQuery",new[]{"XLinkEmitArg","ActorHandle"}},
                {"SetTargetPositionToWalkingRequestQuery",new[]{"WalkingRequest","MapId","Position"}},
                {"SetSpeedToWalkingRequestQuery",new[]{"WalkingRequest","Speed"}},
                {"SetTimeOfStartingAndReachingToWalkingRequestQuery",new[]{"WalkingRequest","MinimumSpeed","ReachingTime","StartingTime"}},
                {"SetAnimationCommandToWalkingRequestQuery",new[]{"WalkingRequest","AnimationCommand"}},
                {"SetReachingDistanceToWalkingRequest",new[]{"WalkingRequest","InnerDistanceMeter","OuterDistanceMeter"}},
                {"SetIsOverlapWaitingToWalkingRequestQuery",new[]{"WalkingRequest","IsEnabled"}},
                {"SetObstacleLimitSpeedToWalkingRequestQuery",new[]{"WalkingRequest","IsEnabled"}},
                {"SetIsActiveObstacleDetectorToWalkingRequestQuery",new[]{"WalkingRequest","IsActive"}},
                {"SetNavigationTargetToWalkingRequestQuery",new[]{"WalkingRequest","GoalSteps"}},
                {"SetMotionSpeedToHeadRequestQuery",new[]{"HeadRequest","LookIKMotionSpeed"}},
                {"SetPositionOffsetToHeadRequestQuery",new[]{"HeadRequest","Position"}},
                {"SetPositionToHeadRequestQuery",new[]{"HeadRequest","Position"}},
                {"SetActorToHeadRequestQuery",new[]{"HeadRequest","ActorHandle"}},
                {"SetVariationNameToHeadRequestQuery",new[]{"HeadRequest","LookIKVariationId","LookIKVariationName"}},
                {"SetBeLookedPositionKeyToHeadRequestQuery",new[]{"HeadRequest","BeLookedKey"}},
                {"SetTargetActorToTurningRequestQuery",new[]{"TurningRequest","ActorHandle"}},
                {"SetTargetDirectionToTurningRequestQuery",new[]{"TurningRequest","Direction"}}
            };
            foreach(var pair in setters)
            {
                var fields=pair.Value;
                registry.Query(pair.Key,(c,o)=>UpdateBag(c,fields[0],fields.Skip(1).ToArray()),Source);
            }
        }
        static OriginalValue NewBag(OriginalNodeContext c,string kind,params string[] fields)
        {
            var value=c.NewBag(kind);var bag=c.Bag(value);foreach(string field in fields)c.Runner.Set(bag.values,field,c.Input(field));return value;
        }
        static OriginalValue UpdateBag(OriginalNodeContext c,string name,params string[] fields)
        {
            var value=c.Input(name);var bag=c.Bag(value);foreach(string field in fields)c.Runner.Set(bag.values,field,c.Input(field));return value;
        }
    }
}
