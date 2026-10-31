using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    public sealed class OriginalNodeContext
    {
        public OriginalGraphRunner Runner{get;internal set;}
        public OriginalScopeState Scope{get;internal set;}
        public OriginalFrameState Frame{get;internal set;}
        public OriginalNode Node{get;internal set;}
        public string Address=>Scope.graph+"#"+Node.index+"/"+Node.name;
        public string ExecutionKey=>Runner.State.episodeId+"/"+Scope.id+"/"+Node.index+"/"+(Frame?.enterSequence??0);
        public OriginalValue Input(string name)=>Runner.Input(Scope,Node,name,Frame);
        public IReadOnlyList<OriginalValue> InputValues(string name)=>Runner.InputValues(Scope,Node,name,Frame);
        public OriginalValue Property(string name)=>Runner.Property(Scope,Node,name,Frame);
        public bool HasInput(string name)=>(Node.inputs??Array.Empty<OriginalParameter>()).Any(p=>p.name==name)||(Node.links??Array.Empty<OriginalEdge>()).Any(e=>e.kind!="Child"&&e.kind!="Transition"&&e.name==name);
        public bool HasProperty(string name)=>(Node.properties??Array.Empty<OriginalParameter>()).Any(p=>p.name==name);
        string StorageKey(string key)=>Frame==null||Frame.node!=Node.index?"handler:"+Node.index+":"+key:key;
        public OriginalValue Read(string key)=>Runner.Get(Frame==null?Scope.values:Frame.values,StorageKey(key));
        public void Write(string key,OriginalValue value){Runner.Set(Frame==null?Scope.values:Frame.values,StorageKey(key),value);}
        public void Blackboard(string key,OriginalValue value)=>Runner.WriteBlackboard(Scope,key,value);
        public OriginalValue Blackboard(string key)=>Runner.ReadBlackboard(Scope,key);
        public OriginalBagState Bag(OriginalValue value)=>Runner.Bag(value);
        public OriginalValue NewBag(string kind)=>Runner.NewBag(kind+"/"+ExecutionKey);
        public void Signal(string name){if(Frame==null)throw new OriginalBindingException(Address+": query cannot emit a flow signal");Frame.signal=name;}
        public string[] Participants=>Runner.Participants;
        public double Elapsed=>Frame?.elapsed??Runner.State.elapsedSeconds;
        public bool IsNewEntry{get;internal set;}
    }

    // Executes source graph structure. User-defined nodes require explicit source/host ports.
    // A missing port records a binding fault; it never becomes a successful event result.
    public sealed class OriginalGraphRunner
    {
        public static bool IsBuiltInUserNode(string name)
        {
            switch(name)
            {
                case "BoolSelector":case "FloatCompareSelector":case "NullActorJudgeSelector":case "IsActorListSize1AndValidSelector":case "ActorFromActorListSelector":case "StringEmptySelector":case "ItemInfoIsNullSelector":
                case "WaitSuccessSimultaneousSelector":case "OrSimultaneousSelector":case "SuccessSelector":case "ForceSuccessSelector":case "BusySelector":
                case "RepeatUntilSuccessSelector":case "LoopSelector":case "WhileSelector":case "WhileLoopSelector":case "LoopTopNodeSimultaneousSuccessSelector":case "CaptureV3fSelector":return true;
                default:return false;
            }
        }
        public OriginalGraphLibrary Library{get;}
        public OriginalNodeRegistry Registry{get;}
        public OriginalGraphRunState State{get;private set;}
        public string[] Participants{get;}
        public Func<C004NativeSourceFrame> NativeFrameInput;
        public Func<string,bool?> ActorHandleValidity;
        public string[] ForkStrings{get;}
        readonly Dictionary<int,OriginalScopeState> scopes=new Dictionary<int,OriginalScopeState>();
        readonly Dictionary<int,OriginalFrameState> frames=new Dictionary<int,OriginalFrameState>();
        readonly Dictionary<string,OriginalValue> queryCache=new Dictionary<string,OriginalValue>(StringComparer.Ordinal);
        readonly HashSet<string> evaluating=new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string,OriginalValue> systemValues=new Dictionary<string,OriginalValue>(StringComparer.Ordinal);
        readonly Dictionary<string,NativeExpressionProgram> expressionPrograms=new Dictionary<string,NativeExpressionProgram>(StringComparer.Ordinal);
        int steps;
        public OriginalGraphRunner(OriginalGraphLibrary library,OriginalNodeRegistry registry,string episode,string graph,string command,string[] participants,string[] forkStrings,OriginalGraphRunState restored=null)
        {
            Library=library;Registry=registry;Participants=participants??Array.Empty<string>();ForkStrings=forkStrings??Array.Empty<string>();
            State=restored??new OriginalGraphRunState{episodeId=episode};
            if(State.schema!=1||State.episodeId!=episode)throw new OriginalBindingException("Graph continuation belongs to another episode/schema");
            foreach(var s in State.scopes){C004NativeGraphLibrary.RequireGraph(s.graph);scopes.Add(s.id,s);}
            foreach(var f in State.frames)frames.Add(f.id,f);
            for(int i=0;i<10;i++)
            {
                string id=i<Participants.Length?Participants[i]:null;
                systemValues["Mii"+i]=OriginalValue.Actor(id);systemValues["Mii"+i+"List"]=OriginalValue.Actors(string.IsNullOrEmpty(id)?Array.Empty<string>():new[]{id});
                if(i<4)systemValues["SlotMii"+i]=OriginalValue.Actor(id);
            }
            systemValues["MiiList"]=OriginalValue.Actors(Participants);
            systemValues["Mii0_1List"]=OriginalValue.Actors(Participants.Take(2));
            systemValues["Mii1_2List"]=OriginalValue.Actors(Participants.Skip(1).Take(2));
            systemValues["MiiListNum"]=OriginalValue.Number(Participants.Count(v=>!string.IsNullOrEmpty(v)));
            systemValues["IsSkipRestart"]=OriginalValue.Bool(false);
            systemValues["MessageParam"]=NewBag("MessageParam/root");
            if(restored==null)
            {
                var scope=NewScope(graph,null,null,null);
                State.rootFrame=NewFrame(scope.id,Library.Graph(graph).Entry(command),-1).id;
            }
        }
        public OriginalNodeStatus Tick(double seconds)
        {
            if(State.finished)return OriginalNodeStatus.Success;
            if(!string.IsNullOrEmpty(State.error))return OriginalNodeStatus.Failure;
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
            State.tick++;State.elapsedSeconds+=seconds;steps=0;queryCache.Clear();evaluating.Clear();
            try
            {
                var status=Step(frames[State.rootFrame],seconds,0);
                if(status==OriginalNodeStatus.Success)State.finished=true;
                if(status==OriginalNodeStatus.Failure&&string.IsNullOrEmpty(State.error))State.error="Original graph returned Failure without a DemoResult completion";
                return status;
            }
            catch(Exception e)
            {
                State.error=e.GetType().Name+": "+e.Message;
                Trace("BindingFault "+State.error);return OriginalNodeStatus.Failure;
            }
        }
        public void RetryAfterBindingChange(){State.error="";queryCache.Clear();evaluating.Clear();}
        public string Save()=>JsonUtility.ToJson(State);
        public void SetSystemValue(string key,OriginalValue value){systemValues[key]=value;}
        public string ForkString(int index)=>index>=0&&index<ForkStrings.Length?ForkStrings[index]:throw new OriginalBindingException("Missing original DemoForkStr slot "+index);
        OriginalScopeState NewScope(string graph,OriginalScopeState parent,OriginalNode caller,OriginalFrameState frame)
        {
            var scope=new OriginalScopeState{id=State.nextId++,graph=graph,parentScope=parent?.id??-1,callerNode=caller?.index??-1,callerFrame=frame?.id??-1};
            C004NativeGraphLibrary.RequireGraph(graph);Library.Graph(graph);State.scopes.Add(scope);scopes.Add(scope.id,scope);return scope;
        }
        OriginalFrameState NewFrame(int scope,int node,int parent)
        {
            var frame=new OriginalFrameState{id=State.nextId++,scope=scope,node=node,parent=parent,enterSequence=State.nextEnter++};
            State.frames.Add(frame);frames.Add(frame.id,frame);return frame;
        }
        OriginalNodeContext Context(OriginalScopeState s,OriginalNode n,OriginalFrameState f)=>new OriginalNodeContext{Runner=this,Scope=s,Node=n,Frame=f};
        OriginalNodeStatus Step(OriginalFrameState frame,double dt,int depth)
        {
            if(++steps>8192||depth>192)throw new OriginalBindingException("Source graph recursion/step budget exceeded at "+frame.scope+":"+frame.node);
            if(frame.complete)return frame.failed?OriginalNodeStatus.Failure:OriginalNodeStatus.Success;
            var scope=scopes[frame.scope];var graph=Library.Graph(scope.graph);var node=graph.Node(frame.node);
            bool isNewEntry=!frame.entered;
            if(!frame.entered)
            {
                // Durable proof that THIS source activation was first entered
                // under the typed lifecycle adapter. A recoverable EXB/input
                // binding fault occurs before a raw Wait body may be entered;
                // retry must not confuse it with an old elapsed-only save.
                // ACTUAL activation4ca10 clears bit1 with0x3ffffff4 at
                //4ca68 BEFORE genericEnter4d39c. Direct genericEnter alone
                //does not reset it and is not the full reentry lifecycle.
                if(node.name=="WaitSecAction"||node.name=="WaitBgmTransitioningFinishAction")
                {
                    Set(frame.values,"native-entry-proof/v1",OriginalValue.Bool(true));
                    Set(scope.values,"native-node-updated/v1/"+node.index,OriginalValue.Bool(false));
                }
                frame.entered=true;Trace(scope.graph+"#"+node.index+" "+node.name);
            }
            frame.elapsed+=dt;
            var context=Context(scope,node,frame);var entryProof=Get(frame.values,"native-entry-proof/v1");
            if(entryProof!=null&&entryProof.kind!="Bool")throw new OriginalBindingException("Invalid native activation entry proof kind");
            context.IsNewEntry=isNewEntry||entryProof!=null&&entryProof.boolean;
            OriginalNodeStatus status;
            EnsureExpressionsBound(scope,node);
            if(node.isModule)
            {
                if(frame.moduleScope<0)
                {
                    var child=NewScope(node.name,scope,node,frame);frame.moduleScope=child.id;
                    frame.children.Add(NewFrame(child.id,Library.Graph(node.name).Entry(Library.Graph(node.name).ModuleEntry()),frame.id).id);
                }
                status=Step(frames[frame.children[0]],dt,depth+1);
            }
            else if(node.kind=="Element_StateEnd")status=OriginalNodeStatus.Success;
            else if(node.kind=="Element_ModuleIF_Child")
            {
                string childName=context.Property("Name").AsText();
                if(scope.parentScope<0)throw new OriginalBindingException(context.Address+": module Child has no caller");
                var parent=scopes[scope.parentScope];var call=Library.Graph(parent.graph).Node(scope.callerNode);
                var link=call.Children.SingleOrDefault(l=>l.name==childName);
                if(link==null)
                {
                    if(context.HasProperty("IsAcceptNoConnect")&&context.Property("IsAcceptNoConnect").AsBool())status=OriginalNodeStatus.Success;
                    else throw new OriginalBindingException(context.Address+": required module Child unconnected: "+childName);
                }
                else{EnsureChildren(frame,new[]{link},parent.id);status=Step(frames[frame.children[0]],dt,depth+1);}
            }
            else if(node.kind=="Element_Sequential")status=Sequence(context,dt,depth);
            else if(node.kind=="Element_Simultaneous")status=Parallel(context,dt,depth,false);
            else if(node.kind=="Element_StringSelector"||node.kind=="Element_S32Selector"||node.kind=="Element_BoolSelector")status=Select(context,dt,depth);
            else if(node.kind=="Element_Fork")
            {
                EnsureChildren(frame,node.Transitions,scope.id);
                status=ChildrenStatus(frame,dt,depth,false);
                if(status!=OriginalNodeStatus.Running){frame.complete=true;frame.failed=status==OriginalNodeStatus.Failure;}return status;
            }
            else if(node.kind=="Element_Join")
            {
                int expected=(int)context.Property("InFlowNum").AsNumber();
                var peers=State.frames.Where(f=>f.scope==frame.scope&&f.node==frame.node&&f.entered&&!f.complete).ToArray();
                if(peers.Length<expected)return OriginalNodeStatus.Running;
                var leader=peers.OrderBy(f=>f.id).First();
                foreach(var peer in peers)if(peer!=leader){peer.complete=true;peer.failed=false;}
                status=OriginalNodeStatus.Success;
                if(frame!=leader)return OriginalNodeStatus.Success;
            }
            else if(node.kind.StartsWith("Element_ModuleIF_",StringComparison.Ordinal))
            {
                Query(scope,node,0,node.outputs?.FirstOrDefault()?.kind??"Pointer",frame);status=OriginalNodeStatus.Success;
            }
            else if(node.kind=="UserDefined")
            {
                switch(node.name)
                {
                    case "BoolSelector":case "FloatCompareSelector":case "NullActorJudgeSelector":case "IsActorListSize1AndValidSelector":case "ActorFromActorListSelector":case "StringEmptySelector":case "ItemInfoIsNullSelector":status=Select(context,dt,depth);break;
                    case "WaitSuccessSimultaneousSelector":case "OrSimultaneousSelector":status=Parallel(context,dt,depth,true);break;
                    case "LoopTopNodeSimultaneousSuccessSelector":status=LoopTopParallel(context,dt,depth);break;
                    case "CaptureV3fSelector":status=CaptureVector(context,dt,depth);break;
                    case "SuccessSelector":case "ForceSuccessSelector":case "BusySelector":case "RepeatUntilSuccessSelector":case "LoopSelector":case "WhileSelector":case "WhileLoopSelector":status=Decorator(context,dt,depth);break;
                    default:status=node.isQuery?(Query(scope,node,0,node.outputs?.FirstOrDefault()?.kind??"Pointer",frame)!=null?OriginalNodeStatus.Success:OriginalNodeStatus.Failure):Registry.Execute(context,dt);break;
                }
            }
            else throw new OriginalBindingException(context.Address+": unsupported structural node "+node.kind);
            if(status==OriginalNodeStatus.Running)return status;
            var transitions=node.Transitions;
            OriginalEdge transition=null;
            if(!string.IsNullOrEmpty(frame.signal))transition=transitions.SingleOrDefault(t=>t.transitionType==0&&t.transitionName==frame.signal);
            if(transition==null&&status==OriginalNodeStatus.Success)
            {
                var normal=transitions.Where(t=>t.transitionType==1).ToArray();
                if(normal.Length>1)throw new OriginalBindingException(context.Address+": multiple completion transitions outside Fork");
                if(normal.Length==1)transition=normal[0];
            }
            if(transition!=null)
            {
                frame.node=transition.node;frame.phase=frame.cursor=frame.loop=0;frame.elapsed=0;frame.moduleScope=-1;frame.children.Clear();frame.values.Clear();frame.signal="";frame.entered=false;frame.enterSequence=State.nextEnter++;
                return transition.postCalc?Step(frame,0,depth+1):OriginalNodeStatus.Running;
            }
            if(transitions.Any(t=>t.transitionType==0)&&string.IsNullOrEmpty(frame.signal)&&status==OriginalNodeStatus.Success)throw new OriginalBindingException(context.Address+": named output signal missing");
            frame.complete=true;frame.failed=status==OriginalNodeStatus.Failure;return status;
        }
        void EnsureChildren(OriginalFrameState f,IReadOnlyList<OriginalEdge> links,int scope)
        {
            if(f.children.Count!=0)return;
            foreach(var edge in links)f.children.Add(NewFrame(scope,edge.node,f.id).id);
        }
        OriginalNodeStatus ChildrenStatus(OriginalFrameState f,double dt,int depth,bool anySuccess)
        {
            bool running=false,failed=false;
            foreach(int id in f.children.ToArray())
            {
                var status=Step(frames[id],dt,depth+1);
                if(anySuccess&&status==OriginalNodeStatus.Success)return status;
                running|=status==OriginalNodeStatus.Running;failed|=status==OriginalNodeStatus.Failure;
            }
            if(running)return OriginalNodeStatus.Running;
            return failed?OriginalNodeStatus.Failure:OriginalNodeStatus.Success;
        }
        OriginalNodeStatus Parallel(OriginalNodeContext c,double dt,int depth,bool anySuccess)
        {
            if(c.HasProperty("EndPolicy")&&c.Property("EndPolicy").AsNumber()!=0)throw new OriginalBindingException(c.Address+": nondefault source EndPolicy requires original port");
            if(c.HasProperty("ResultPolicy")&&c.Property("ResultPolicy").AsNumber()!=0)throw new OriginalBindingException(c.Address+": nondefault source ResultPolicy requires original port");
            EnsureChildren(c.Frame,c.Node.Children,c.Scope.id);return ChildrenStatus(c.Frame,dt,depth,anySuccess);
        }
        OriginalNodeStatus Sequence(OriginalNodeContext c,double dt,int depth)
        {
            var f=c.Frame;var edges=c.Node.Children;
            foreach(var key in new[]{"BusyPolicy","PlayPolicy","ResultPolicy"})if(c.HasProperty(key)&&c.Property(key).AsNumber()!=0)throw new OriginalBindingException(c.Address+": nondefault "+key+" requires original port");
            if(edges.Length==0)return OriginalNodeStatus.Success;
            while(f.cursor<edges.Length)
            {
                if(f.children.Count<=f.cursor)f.children.Add(NewFrame(c.Scope.id,edges[f.cursor].node,f.id).id);
                var status=Step(frames[f.children[f.cursor]],dt,depth+1);
                if(status!=OriginalNodeStatus.Success)return status;
                f.cursor++;
                if(f.cursor<edges.Length&&c.HasProperty("IsUpdateNextInFrame")&&!c.Property("IsUpdateNextInFrame").AsBool())return OriginalNodeStatus.Running;
                dt=0;
            }
            bool loop=c.HasProperty("IsLoop")&&c.Property("IsLoop").AsBool();
            int loops=c.HasProperty("NumLoop")?(int)c.Property("NumLoop").AsNumber():0;
            if(loop&&(loops==0||++f.loop<loops)){f.cursor=0;f.children.Clear();return OriginalNodeStatus.Running;}
            return OriginalNodeStatus.Success;
        }
        OriginalNodeStatus Select(OriginalNodeContext c,double dt,int depth)
        {
            if(c.Frame.children.Count==0)
            {
                var n=c.Node;string selection;
                if(n.kind=="Element_BoolSelector")selection=c.Input("Input").AsBool()?"True":"False";
                else if(n.kind=="Element_StringSelector"||n.kind=="Element_S32Selector")selection=c.Input("Input").AsText();
                else if(n.name=="BoolSelector")selection=c.Input("Bool").AsBool()?"True":"False";
                else if(n.name=="FloatCompareSelector")
                {
                    double value=c.Input("FloatValue").AsNumber(),threshold=c.Input("FloatThreshold").AsNumber();selection=value>threshold?"Greater":value<threshold?"Less":"Equal";
                }
                else if(n.name=="NullActorJudgeSelector")selection=c.Input("ActorHandle").IsNull?"Null":"Valid";
                else if(n.name=="StringEmptySelector")selection=string.IsNullOrEmpty(c.Input("String").AsText())?"Empty":"NotEmpty";
                else if(n.name=="ItemInfoIsNullSelector")selection=c.Input("ItemInfo").IsNull?"Null":"NotNull";
                else if(n.name=="IsActorListSize1AndValidSelector")
                {
                    // Original01976ecc publishes its actual list pointer, then
                    // tests size==1 and the real actor registry token/index.
                    // Its original registered child names are No/Yes.
                    var list=c.Read("native-selector:ActorHandleList");
                    if(list==null){list=c.Input("ActorHandleList");if(!list.IsNull&&list.kind!="Actors")throw new OriginalBindingException(c.Address+": exact actor-list owner required");c.Write("native-selector:ActorHandleList",list);}
                    c.Write("output:ActorHandleList",list);
                    var ids=list.ActorIds();bool valid=false;
                    if(ids.Length==1)
                    {
                        var actual=ActorHandleValidity?.Invoke(ids[0]);
                        if(!actual.HasValue)throw new OriginalBindingException(c.Address+": actual actor-handle validity owner is unavailable");
                        valid=actual.Value;
                    }
                    selection=valid?"Yes":"No";
                }
                else if(n.name=="ActorFromActorListSelector")
                {
                    var all=c.Input("ActorHandleList").ActorIds();int index=(int)c.Input("Index").AsNumber();bool valid=index>=0&&index<all.Length;
                    c.Write("output:ActorHandle",valid?OriginalValue.Actor(all[index]):OriginalValue.Null());selection=valid?"In":"Out";
                }
                else throw new OriginalBindingException(c.Address+": unregistered exact selector");
                var choices=n.Children.Where(e=>!e.isDefault&&(e.condition==selection||e.name==selection)).ToArray();
                if(choices.Length>1&&choices.Select(e=>e.node).Distinct().Count()>1)throw new OriginalBindingException(c.Address+": ambiguous source selector for "+selection);
                var selected=choices.FirstOrDefault()??n.Children.SingleOrDefault(e=>e.isDefault);
                if(selected==null)throw new OriginalBindingException(c.Address+": no source selector branch for "+selection);
                c.Frame.children.Add(NewFrame(c.Scope.id,selected.node,c.Frame.id).id);
            }
            return Step(frames[c.Frame.children[0]],dt,depth+1);
        }
        OriginalNodeStatus CaptureVector(OriginalNodeContext c,double dt,int depth)
        {
            var edges=c.Node.Children;if(edges.Length!=1||edges[0].name!="Child")throw new OriginalBindingException(c.Address+": original CaptureV3f Child binding is unavailable");
            if(NativeCaptureVectorBinding.Enter(c)){string prefix=c.Scope.id+"/"+c.Node.index+"/";foreach(string key in queryCache.Keys.Where(k=>k.StartsWith(prefix,StringComparison.Ordinal)).ToArray())queryCache.Remove(key);}
            EnsureChildren(c.Frame,edges,c.Scope.id);return Step(frames[c.Frame.children[0]],dt,depth+1);
        }
        OriginalNodeStatus Decorator(OriginalNodeContext c,double dt,int depth)
        {
            var f=c.Frame;var edges=c.Node.Children;
            if(edges.Length!=1)throw new OriginalBindingException(c.Address+": expected one source decorator child");
            if(c.Node.name=="WhileSelector"||c.Node.name=="WhileLoopSelector")if(!c.Input("Bool").AsBool())return OriginalNodeStatus.Success;
            if(c.Node.name=="LoopSelector")
            {
                int count=(int)c.Input("LoopCount").AsNumber();
                if(f.loop>=count)return OriginalNodeStatus.Success;
                c.Write("output:LoopCount",OriginalValue.Number(f.loop));c.Write("output:IsLastLoop",OriginalValue.Bool(f.loop==count-1));
            }
            EnsureChildren(f,edges,c.Scope.id);var status=Step(frames[f.children[0]],dt,depth+1);
            if(c.Node.name=="ForceSuccessSelector"&&c.HasInput("Sec")&&f.elapsed>=c.Input("Sec").AsNumber())return OriginalNodeStatus.Success;
            if(status==OriginalNodeStatus.Running)return status;
            switch(c.Node.name)
            {
                case "SuccessSelector":case "ForceSuccessSelector":return OriginalNodeStatus.Success;
                case "BusySelector":return OriginalNodeStatus.Running;
                case "RepeatUntilSuccessSelector":if(status==OriginalNodeStatus.Success)return status;break;
                case "WhileSelector":return status;
                case "LoopSelector":f.loop++;break;
            }
            f.children.Clear();return OriginalNodeStatus.Running;
        }
        // Original main 00a098ec starts both children; 00a09a40 ticks both,
        // returns an unfinished Top status, succeeds when both succeed, and
        // otherwise restarts only Top (the source loop guard is exactly101).
        OriginalNodeStatus LoopTopParallel(OriginalNodeContext c,double dt,int depth)
        {
            var top=c.Node.Children.SingleOrDefault(e=>e.name=="TopChild");
            var bottom=c.Node.Children.SingleOrDefault(e=>e.name=="BottomChild");
            if(top==null||bottom==null||c.Node.Children.Length!=2)throw new OriginalBindingException(c.Address+": original TopChild/BottomChild pair is missing");
            EnsureChildren(c.Frame,new[]{top,bottom},c.Scope.id);
            var topStatus=Step(frames[c.Frame.children[0]],dt,depth+1);
            var bottomStatus=Step(frames[c.Frame.children[1]],dt,depth+1);
            for(int guard=0;guard<101;guard++)
            {
                if(topStatus!=OriginalNodeStatus.Success)return topStatus;
                if(bottomStatus==OriginalNodeStatus.Success)return OriginalNodeStatus.Success;
                c.Frame.children[0]=NewFrame(c.Scope.id,top.node,c.Frame.id).id;
                topStatus=Step(frames[c.Frame.children[0]],0,depth+1);
            }
            return topStatus;
        }
        public OriginalValue Input(OriginalScopeState scope,OriginalNode node,string name,OriginalFrameState frame=null)
        {
            var p=(node.inputs??Array.Empty<OriginalParameter>()).SingleOrDefault(v=>v.name==name);
            return ApplyExpression(scope,node,p,RawInput(scope,node,name,frame));
        }
        OriginalValue RawInput(OriginalScopeState scope,OriginalNode node,string name,OriginalFrameState frame)
        {
            var p=(node.inputs??Array.Empty<OriginalParameter>()).SingleOrDefault(v=>v.name==name);
            var edge=(node.links??Array.Empty<OriginalEdge>()).FirstOrDefault(e=>e.kind!="Child"&&e.kind!="Transition"&&e.name==name);
            if(p==null)
            {
                if(edge!=null)return Query(scope,Library.Graph(scope.graph).Node(edge.node),0,edge.kind,frame);
                throw new OriginalBindingException(scope.graph+"#"+node.index+": missing input "+name);
            }
            if(p.sources!=null&&p.sources.Length>0)
            {
                if(p.sources.Length!=1)throw new OriginalBindingException(scope.graph+"#"+node.index+": source input "+name+" requires its authored multiple-value consumer");
                var source=p.sources[0];
                if(source.node<0||(source.flags!=null&&source.flags.Length>0))throw new OriginalBindingException(scope.graph+"#"+node.index+": unresolved authored source flags for "+name);
                return Query(scope,Library.Graph(scope.graph).Node(source.node),source.output,p.kind,frame);
            }
            if(p.node>=0)return Query(scope,Library.Graph(scope.graph).Node(p.node),p.output,p.kind,frame);
            if(edge!=null&&!(p.flags??Array.Empty<string>()).Contains("uses_default"))return Query(scope,Library.Graph(scope.graph).Node(edge.node),p.output,p.kind,frame);
            if(p.blackboard>=0)return ReadBlackboardIndex(scope,p.kind,p.blackboard);
            return OriginalValue.Literal(p.kind,p.literal);
        }
        public IReadOnlyList<OriginalValue> InputValues(OriginalScopeState scope,OriginalNode node,string name,OriginalFrameState frame=null)
        {
            var parameter=(node.inputs??Array.Empty<OriginalParameter>()).SingleOrDefault(p=>p.name==name);
            if(parameter?.sources==null||parameter.sources.Length<=1)return new[]{Input(scope,node,name,frame)};
            if(parameter.expressionIndex>=0)throw new OriginalBindingException(scope.graph+"#"+node.index+": original multiple-value EXB consumer is not part of this source subset");
            var result=new OriginalValue[parameter.sources.Length];
            for(int index=0;index<result.Length;index++)
            {
                var source=parameter.sources[index];
                if(source.node<0||(source.flags!=null&&source.flags.Length>0))throw new OriginalBindingException(scope.graph+"#"+node.index+": unresolved authored multiple-input source flags for "+name);
                result[index]=Query(scope,Library.Graph(scope.graph).Node(source.node),source.output,parameter.kind,frame);
            }
            return result;
        }
        public OriginalValue Property(OriginalScopeState scope,OriginalNode node,string name,OriginalFrameState frame=null)
        {
            var p=(node.properties??Array.Empty<OriginalParameter>()).SingleOrDefault(v=>v.name==name)??throw new OriginalBindingException(scope.graph+"#"+node.index+": missing property "+name);
            return ApplyExpression(scope,node,p,p.blackboard>=0?ReadBlackboardIndex(scope,p.kind,p.blackboard):OriginalValue.Literal(p.kind,p.literal));
        }
        OriginalValue Query(OriginalScopeState scope,OriginalNode node,int output,string kind,OriginalFrameState requester)
        {
            string canonical=OriginalValue.Canonical(kind),key=scope.id+"/"+node.index+"/"+canonical+"/"+output;
            if(queryCache.TryGetValue(key,out var cached))return cached;
            if(!evaluating.Add(key))throw new OriginalBindingException(scope.graph+"#"+node.index+": cyclic original query "+key);
            try
            {
                var defs=(node.outputs??Array.Empty<OriginalParameter>()).Where(p=>OriginalValue.Canonical(p.kind)==canonical).ToArray();
                string outputName=output>=0&&output<defs.Length?defs[output].name:node.outputs?.Length==1?node.outputs[0].name:null;
                OriginalValue value;
                var active=State.frames.LastOrDefault(f=>f.scope==scope.id&&f.node==node.index&&f.entered&&!f.complete)??requester;
                if(node.isModule)
                {
                    var sub=State.scopes.LastOrDefault(s=>s.parentScope==scope.id&&s.callerNode==node.index&&s.callerFrame==(requester?.id??-1));
                    if(sub==null)sub=NewScope(node.name,scope,node,requester);
                    var g=Library.Graph(sub.graph);
                    var port=g.nodes.SingleOrDefault(n=>n.kind.StartsWith("Element_ModuleIF_Output_",StringComparison.Ordinal)&&Property(sub,n,"Name").AsText()==outputName);
                    if(port==null)throw new OriginalBindingException(scope.graph+"#"+node.index+": no module output "+outputName);
                    var input=port.inputs?.FirstOrDefault()??throw new OriginalBindingException(sub.graph+": empty module output "+outputName);
                    value=Input(sub,port,input.name,active);
                }
                else if(node.kind.StartsWith("Element_ModuleIF_Input_",StringComparison.Ordinal))
                {
                    if(scope.parentScope<0)throw new OriginalBindingException(scope.graph+": module input without caller");
                    var parent=scopes[scope.parentScope];var call=Library.Graph(parent.graph).Node(scope.callerNode);string name=Property(scope,node,"Name").AsText();
                    value=Input(parent,call,name,scope.callerFrame>=0?frames[scope.callerFrame]:requester);
                }
                else if(node.kind=="UserDefined"&&node.name=="CaptureV3fSelector")
                {
                    var captured=State.frames.LastOrDefault(f=>f.scope==scope.id&&f.node==node.index&&f.entered);
                    value=NativeCaptureVectorBinding.Output(Context(scope,node,captured),outputName);
                }
                else
                {
                    if(outputName==null)throw new OriginalBindingException(scope.graph+"#"+node.index+": missing output "+kind+"["+output+"]");
                    EnsureExpressionsBound(scope,node);
                    value=active!=null?Get(active.values,"output:"+outputName):null;
                    if(value==null)value=Registry.Evaluate(Context(scope,node,active),outputName);
                }
                if(value==null)throw new OriginalBindingException(scope.graph+"#"+node.index+": query returned no value for "+outputName);
                queryCache[key]=value;return value;
            }
            finally{evaluating.Remove(key);}
        }
        OriginalValue ReadBlackboardIndex(OriginalScopeState scope,string kind,int index)
        {
            var entry=(Library.Graph(scope.graph).blackboard??Array.Empty<OriginalBlackboardDefinition>()).SingleOrDefault(b=>b.index==index&&OriginalValue.Canonical(b.kind)==OriginalValue.Canonical(kind));
            if(entry==null)throw new OriginalBindingException(scope.graph+": missing original blackboard "+kind+"["+index+"]");
            return ReadBlackboard(scope,entry.name);
        }
        NativeExpressionProgram ExpressionProgram(OriginalScopeState scope)
        {
            if(!expressionPrograms.TryGetValue(scope.graph,out var program))
            {
                try{program=NativeExpressionRuntime.Compile(Library.Graph(scope.graph).expressions);}
                catch(Exception error){throw new OriginalBindingException(scope.graph+": "+error.Message);}
                expressionPrograms.Add(scope.graph,program);
            }
            return program;
        }
        void EnsureExpressionsBound(OriginalScopeState scope,OriginalNode node)
        {
            if(node.expressionCount<=0)return;
            var parameters=(node.inputs??Array.Empty<OriginalParameter>()).Concat(node.properties??Array.Empty<OriginalParameter>()).Where(p=>p.expressionIndex>=0).ToArray();
            if(parameters.Length==0)throw new OriginalBindingException(scope.graph+"#"+node.index+": source expression instance has no restored parameter owner");
            var program=ExpressionProgram(scope);
            foreach(var parameter in parameters)if(parameter.expressionIndex>=program.Count)throw new OriginalBindingException(scope.graph+"#"+node.index+": original expression index is outside its graph");
        }
        OriginalValue ApplyExpression(OriginalScopeState scope,OriginalNode node,OriginalParameter parameter,OriginalValue raw)
        {
            if(parameter==null||parameter.expressionIndex<0)return raw;
            NativeExpressionValue input;
            switch(OriginalValue.Canonical(parameter.kind))
            {
                case "Float":input=NativeExpressionValue.Float((float)raw.AsNumber());break;
                case "Int":input=NativeExpressionValue.Int(unchecked((int)raw.AsNumber()));break;
                case "Bool":input=NativeExpressionValue.Bool(raw.AsBool());break;
                case "Vector":var vector=raw.AsVector();input=NativeExpressionValue.Vector(vector.x,vector.y,vector.z);break;
                default:throw new OriginalBindingException(scope.graph+"#"+node.index+": unsupported original EXB input type "+parameter.kind);
            }
            NativeExpressionValue output;
            try
            {
                output=NativeExpressionRuntime.Evaluate(ExpressionProgram(scope),parameter.expressionIndex,input,
                    index=>(float)ReadBlackboardIndex(scope,"Float",index).AsNumber(),index=>ReadBlackboardIndex(scope,"Bool",index).AsBool());
            }
            catch(Exception error){throw new OriginalBindingException(scope.graph+"#"+node.index+"/"+parameter.name+": "+error.Message);}
            switch(output.type)
            {
                case NativeExpressionValue.FloatType:return OriginalValue.Number(output.x);
                case NativeExpressionValue.IntType:return OriginalValue.Number(output.integer);
                case NativeExpressionValue.BoolType:return OriginalValue.Bool(output.boolean);
                case NativeExpressionValue.VectorType:return OriginalValue.Vector(new Vector3(output.x,output.y,output.z));
                default:throw new OriginalBindingException(scope.graph+"#"+node.index+": expression returned an unsupported type");
            }
        }
        public OriginalValue ReadBlackboard(OriginalScopeState scope,string name)
        {
            var value=Get(scope.values,name);if(value!=null)return value;
            if(systemValues.TryGetValue(name,out value))return value;
            var entry=(Library.Graph(scope.graph).blackboard??Array.Empty<OriginalBlackboardDefinition>()).SingleOrDefault(b=>b.name==name);
            if(entry!=null)return OriginalValue.Literal(entry.kind,entry.literal);
            if(scope.parentScope>=0)return ReadBlackboard(scopes[scope.parentScope],name);
            throw new OriginalBindingException(scope.graph+": no source blackboard named "+name);
        }
        public void WriteBlackboard(OriginalScopeState scope,string name,OriginalValue value)
        {
            if(!(Library.Graph(scope.graph).blackboard??Array.Empty<OriginalBlackboardDefinition>()).Any(b=>b.name==name))throw new OriginalBindingException(scope.graph+": write target not declared in original blackboard: "+name);
            Set(scope.values,name,value);
        }
        public OriginalValue Get(List<OriginalSlot> values,string key)=>values.LastOrDefault(s=>s.key==key)?.value;
        public void Set(List<OriginalSlot> values,string key,OriginalValue value){var s=values.LastOrDefault(v=>v.key==key);if(s==null)values.Add(new OriginalSlot{key=key,value=value});else s.value=value;}
        public OriginalValue NewBag(string name){var id=State.episodeId+"/"+name;if(!State.bags.Any(b=>b.id==id))State.bags.Add(new OriginalBagState{id=id});return OriginalValue.Bag(id);}
        public OriginalBagState Bag(OriginalValue value)=>value!=null&&value.kind=="Bag"?State.bags.SingleOrDefault(b=>b.id==value.text)??throw new OriginalBindingException("Source bag not found: "+value.text):throw new OriginalBindingException("Expected original request/message bag");
        public void Trace(string text){State.trace.Add(text);if(State.trace.Count>256)State.trace.RemoveRange(0,State.trace.Count-256);}
    }
}
