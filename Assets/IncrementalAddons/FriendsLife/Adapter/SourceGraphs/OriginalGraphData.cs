using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [Serializable] public sealed class OriginalGraphLibrary
    {
        public int schema;
        public string sourceVersion;
        public OriginalEventGraphBinding[] eventBindings;
        public OriginalDemoDefinition[] demoDefinitions;
        public OriginalGraph[] graphs;
        [NonSerialized] Dictionary<string,OriginalGraph> byName;
        [NonSerialized] Dictionary<string,OriginalEventGraphBinding> byEvent;
        public void Index()
        {
            if(schema!=1)throw new InvalidOperationException("Unsupported original graph container schema");
            byName=(graphs??Array.Empty<OriginalGraph>()).ToDictionary(g=>g.name,StringComparer.Ordinal);
            byEvent=(eventBindings??Array.Empty<OriginalEventGraphBinding>()).ToDictionary(g=>g.eventName,StringComparer.Ordinal);
            foreach(var graph in graphs)graph.Index();
        }
        public OriginalGraph Graph(string name){if(byName==null)Index();return byName.TryGetValue(name,out var g)?g:throw new OriginalBindingException("Missing source graph: "+name);}
        public OriginalEventGraphBinding Event(string name){if(byEvent==null)Index();return byEvent.TryGetValue(name,out var g)?g:throw new OriginalBindingException("No original DemoParam graph binding for "+name);}
    }
    [Serializable] public sealed class OriginalEventGraphBinding {public string eventName,graph,command;}
    [Serializable] public sealed class OriginalDemoDefinition {public string graph,rowId,defaultArchive,customArchive,rawJson;}
    [Serializable] public sealed class OriginalGraph
    {
        public string name,source,blackboardId,parentBlackboardId;
        public NativeExpressionBinary expressions;
        public OriginalCommand[] commands;
        public OriginalNode[] nodes;
        public OriginalBlackboardDefinition[] blackboard;
        [NonSerialized] Dictionary<int,OriginalNode> index;
        public void Index(){index=(nodes??Array.Empty<OriginalNode>()).ToDictionary(n=>n.index);}
        public OriginalNode Node(int id){if(index==null)Index();return index.TryGetValue(id,out var n)?n:throw new OriginalBindingException(name+": missing node "+id);}
        public int Entry(string command)
        {
            var c=(commands??Array.Empty<OriginalCommand>()).SingleOrDefault(v=>v.name==command);
            if(c==null)throw new OriginalBindingException(name+": missing command "+command);
            return c.node;
        }
        public string ModuleEntry()
        {
            var all=commands??Array.Empty<OriginalCommand>();
            if(all.Any(c=>c.name=="Start"))return "Start";
            if(all.Any(c=>c.name=="Run"))return "Run";
            if(all.Length==1)return all[0].name;
            throw new OriginalBindingException(name+": ambiguous module command");
        }
    }
    [Serializable] public sealed class OriginalCommand {public string name;public int node;}
    [Serializable] public sealed class OriginalBlackboardDefinition {public string kind,name,literal;public int index;}
    [Serializable] public sealed class OriginalNode
    {
        public int index,expressionCount;
        public string kind,name;
        public bool isModule,isQuery;
        public OriginalParameter[] properties,inputs,outputs;
        public OriginalEdge[] links;
        public int[] queryIndices;
        public OriginalEdge[] Children=>(links??Array.Empty<OriginalEdge>()).Where(l=>l.kind=="Child").ToArray();
        public OriginalEdge[] Transitions=>(links??Array.Empty<OriginalEdge>()).Where(l=>l.kind=="Transition").ToArray();
    }
    [Serializable] public sealed class OriginalParameter
    {
        public string kind,name,literal;
        public int node=-1,output,blackboard=-1,expressionIndex=-1;
        public string[] flags;
        public OriginalParameterSource[] sources;
    }
    [Serializable] public sealed class OriginalParameterSource {public int node=-1,output;public string[] flags;}
    [Serializable] public sealed class OriginalEdge
    {
        public string kind,name,condition,transitionName;
        public int node,transitionType=-1;
        public bool isDefault,postCalc;
    }
    public sealed class OriginalBindingException:Exception {public OriginalBindingException(string message):base(message){}}

    [Serializable] public sealed class OriginalValue
    {
        public string kind="Null",text="";
        public double number;
        public bool boolean;
        public Vector3 vector;
        public static OriginalValue Null()=>new OriginalValue();
        public static OriginalValue Number(double value)=>new OriginalValue{kind="Number",number=value};
        public static OriginalValue Bool(bool value)=>new OriginalValue{kind="Bool",boolean=value};
        public static OriginalValue String(string value)=>new OriginalValue{kind="String",text=value??""};
        public static OriginalValue Vector(Vector3 value)=>new OriginalValue{kind="Vector",vector=value};
        public static OriginalValue Actor(string id)=>string.IsNullOrEmpty(id)?Null():new OriginalValue{kind="Actor",text=id};
        public static OriginalValue Actors(IEnumerable<string> ids)=>new OriginalValue{kind="Actors",text=string.Join(",",ids.Where(v=>!string.IsNullOrEmpty(v)))};
        public static OriginalValue Bag(string id)=>new OriginalValue{kind="Bag",text=id};
        public string[] ActorIds()=>kind=="Actor"?new[]{text}:kind=="Actors"?text.Split(new[]{','},StringSplitOptions.RemoveEmptyEntries):Array.Empty<string>();
        public bool IsNull=>kind=="Null";
        public string AsText()=>kind=="Bool"?(boolean?"true":"false"):kind=="Number"?number.ToString(CultureInfo.InvariantCulture):text;
        public double AsNumber()=>kind=="Number"?number:double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var v)?v:throw new OriginalBindingException("Expected numeric source value, got "+kind+":"+text);
        public bool AsBool()=>kind=="Bool"?boolean:kind=="Number"?number!=0:bool.TryParse(text,out var v)?v:throw new OriginalBindingException("Expected bool source value, got "+kind+":"+text);
        public Vector3 AsVector()=>kind=="Vector"?vector:throw new OriginalBindingException("Expected original Vector3, got "+kind);
        public OriginalValue Copy()=>new OriginalValue{kind=kind,text=text,number=number,boolean=boolean,vector=vector};
        public static string Canonical(string type)
        {
            switch(type){case "Int":case "S32":case "U32":return "Int";case "Float":case "F32":return "Float";case "Vector3F":case "Vec3f":return "Vector";case "Pointer":case "VoidPtr":case "Ptr":return "Pointer";default:return type;}
        }
        [Serializable] sealed class StringBox{public string value;}
        [Serializable] sealed class StringsBox{public string[] value;}
        public static OriginalValue Literal(string type,string json)
        {
            type=Canonical(type);json=json??"\"\"";
            if(type=="Vector")
            {
                var a=JsonUtility.FromJson<StringsBox>("{\"value\":"+json+"}").value;
                if(a==null||a.Length!=3)throw new OriginalBindingException("Invalid original Vector3 literal");
                return Vector(new Vector3(float.Parse(a[0],CultureInfo.InvariantCulture),float.Parse(a[1],CultureInfo.InvariantCulture),float.Parse(a[2],CultureInfo.InvariantCulture)));
            }
            string raw=json.StartsWith("\"",StringComparison.Ordinal)?JsonUtility.FromJson<StringBox>("{\"value\":"+json+"}").value:json;
            if(type=="String")return String(raw);
            if(type=="Bool")return Bool(bool.Parse(raw));
            if(type=="Int"||type=="Float")return Number(double.Parse(raw,CultureInfo.InvariantCulture));
            if(type=="Pointer"&&(raw=="null"||string.IsNullOrEmpty(raw)))return Null();
            if(type=="Pointer")throw new OriginalBindingException("Unbound original pointer literal: "+raw);
            throw new OriginalBindingException("Unsupported original literal type: "+type);
        }
    }

    [Serializable] public sealed class OriginalSlot{public string key;public OriginalValue value;}
    [Serializable] public sealed class OriginalScopeState
    {
        public int id,parentScope=-1,callerNode=-1,callerFrame=-1;
        public string graph;
        public List<OriginalSlot> values=new List<OriginalSlot>();
    }
    [Serializable] public sealed class OriginalFrameState
    {
        public int id,scope,node,parent=-1,phase,cursor,loop,enterSequence,moduleScope=-1;
        public bool complete,failed,entered;
        public double elapsed;
        public string signal="";
        public List<int> children=new List<int>();
        public List<OriginalSlot> values=new List<OriginalSlot>();
    }
    [Serializable] public sealed class OriginalBagState{public string id;public List<OriginalSlot> values=new List<OriginalSlot>();}
    [Serializable] public sealed class OriginalGraphRunState
    {
        public int schema=1,nextId=1,nextEnter=1,rootFrame;
        public long tick;
        public double elapsedSeconds;
        public string episodeId,result="",error="";
        public bool resultWritten,finished;
        public List<OriginalScopeState> scopes=new List<OriginalScopeState>();
        public List<OriginalFrameState> frames=new List<OriginalFrameState>();
        public List<OriginalBagState> bags=new List<OriginalBagState>();
        public List<string> trace=new List<string>();
    }
    public enum OriginalNodeStatus { Running,Success,Failure }
    public delegate OriginalNodeStatus OriginalActionHandler(OriginalNodeContext context,double seconds);
    public delegate OriginalValue OriginalQueryHandler(OriginalNodeContext context,string outputName);
    public sealed class OriginalNodeRegistry
    {
        readonly Dictionary<string,OriginalActionHandler> actions=new Dictionary<string,OriginalActionHandler>(StringComparer.Ordinal);
        readonly Dictionary<string,OriginalQueryHandler> queries=new Dictionary<string,OriginalQueryHandler>(StringComparer.Ordinal);
        readonly Dictionary<string,string> sources=new Dictionary<string,string>(StringComparer.Ordinal);
        public void Action(string name,OriginalActionHandler action,string source){if(actions.ContainsKey(name))throw new InvalidOperationException("Duplicate original action "+name);actions.Add(name,action);sources[name]=source;}
        public void Query(string name,OriginalQueryHandler query,string source){if(queries.ContainsKey(name))throw new InvalidOperationException("Duplicate original query "+name);queries.Add(name,query);sources[name]=source;}
        public bool Has(OriginalNode n)=>n.isModule||n.kind!="UserDefined"||OriginalGraphRunner.IsBuiltInUserNode(n.name)||actions.ContainsKey(n.name)||queries.ContainsKey(n.name);
        public OriginalNodeStatus Execute(OriginalNodeContext c,double dt)=>actions.TryGetValue(c.Node.name,out var f)?f(c,dt):throw new OriginalBindingException(c.Address+": missing original action binding "+c.Node.name);
        public OriginalValue Evaluate(OriginalNodeContext c,string output)=>queries.TryGetValue(c.Node.name,out var f)?f(c,output):throw new OriginalBindingException(c.Address+": missing original query binding "+c.Node.name);
        public IReadOnlyDictionary<string,string> Sources=>sources;
    }
}
