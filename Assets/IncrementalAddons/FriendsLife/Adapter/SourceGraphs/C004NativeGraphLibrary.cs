using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // Typed data supplement. Existing 340 graphs, eventBindings and DemoParam
    // definitions are retained. A source file is not a recovered UserDefined body.
    public static class C004NativeGraphLibrary
    {
        [Serializable] sealed class MetadataFile{public int schema;public Metadata[] sources;}
        [Serializable] sealed class Metadata{public string name,source,binary;public bool supplemental;public string[] unsupported;}
        static Dictionary<string,Metadata> sources;
        public static void Extend(OriginalGraphLibrary library)
        {
            if(library==null||library.schema!=1)throw new OriginalBindingException("C004 supplement requires original graph schema 1");
            var metadata=new Dictionary<string,Metadata>(StringComparer.Ordinal);
            foreach(var asset in Resources.LoadAll<TextAsset>("FriendsLife/C004Native/Metadata").OrderBy(a=>a.name,StringComparer.Ordinal))
            {
                var file=JsonUtility.FromJson<MetadataFile>(asset.text);
                if(file==null||file.schema!=1||file.sources==null)throw new OriginalBindingException("Invalid C004 source metadata: "+asset.name);
                foreach(var source in file.sources)
                {
                    if(source==null||string.IsNullOrEmpty(source.name)||metadata.ContainsKey(source.name))throw new OriginalBindingException("Duplicate/empty C004 source metadata: "+asset.name);
                    metadata.Add(source.name,source);
                }
            }
            var original=(library.graphs??Array.Empty<OriginalGraph>()).ToList();var names=new HashSet<string>(original.Select(g=>g.name),StringComparer.Ordinal);
            var supplemental=new List<OriginalGraph>();
            foreach(var asset in Resources.LoadAll<TextAsset>("FriendsLife/C004Native/Graphs").OrderBy(a=>a.name,StringComparer.Ordinal))
            {
                var chunk=JsonUtility.FromJson<OriginalGraphLibrary>(asset.text);
                if(chunk==null||chunk.schema!=1||chunk.graphs==null||(chunk.eventBindings?.Length??0)!=0||(chunk.demoDefinitions?.Length??0)!=0)
                    throw new OriginalBindingException("Invalid C004 supplemental graph container: "+asset.name);
                foreach(var graph in chunk.graphs)
                {
                    if(graph==null||string.IsNullOrEmpty(graph.name)||!metadata.ContainsKey(graph.name))throw new OriginalBindingException("Supplemental graph has no exact source metadata: "+asset.name);
                    if(!names.Add(graph.name))throw new OriginalBindingException("C004 supplement would replace/duplicate existing graph: "+graph.name);
                    supplemental.Add(graph);
                }
            }
            if(supplemental.Count==0)throw new OriginalBindingException("C004 supplemental original graph resources are absent");
            original.AddRange(supplemental);library.graphs=original.ToArray();sources=metadata;
        }
        public static void RequireGraph(string name)
        {
            if(sources==null)throw new OriginalBindingException("C004 source metadata owner is not initialized");
            if(!sources.TryGetValue(name,out var source))throw new OriginalBindingException("Graph has no C004 source record: "+name);
            // Existing production graphs keep their current execution behavior;
            // newly introduced graph features must never be silently discarded.
            if(source.supplemental&&(source.unsupported?.Length??0)>0)
                throw new OriginalBindingException(name+": unresolved original graph metadata: "+string.Join("; ",source.unsupported));
        }
    }
}
