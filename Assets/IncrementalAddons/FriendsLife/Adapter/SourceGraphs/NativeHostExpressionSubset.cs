using System;using System.Collections.Generic;using System.Linq;using UnityEngine;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // Explicit HOST semantic counterpart subset. Raw native expression and
    // ownership remain in NativeMiiFacialOwner. This is not75 Mii raster/parts
    // fidelity and never maps an unknown expression to Smile.
    public sealed class NativeHostExpressionSubset:IDisposable
    {
        [Serializable]public sealed class Curve{public string name;public float weight;}
        [Serializable]public sealed class Preset{public int sourceCode;public string hostPreset,sourceClip;public bool neutral;public Curve[] curves;}
        [Serializable]sealed class Catalog{public int schema;public string domain;public Preset[] presets;}
        sealed class Target{public SkinnedMeshRenderer renderer;public Mesh mesh;public int[] indices;public string[] names;public float[] before,last;public bool written;public int[] geometricCodes;}
        static Catalog catalog;readonly List<Target> targets=new List<Target>();bool disposed;
        static Catalog Data
        {
            get
            {
                if(catalog!=null)return catalog;var text=Resources.Load<TextAsset>("FriendsLife/NativeExpressions/host-semantic-subset");if(!text)throw new OriginalBindingException("Actual host expression subset resource unavailable");var result=JsonUtility.FromJson<Catalog>(text.text);if(result==null||result.schema!=1||result.presets==null||!result.presets.Select(p=>p.sourceCode).OrderBy(x=>x).SequenceEqual(new[]{0,2,4,6,8,10,44,47,57}))throw new OriginalBindingException("Unknown host semantic expression recipe");foreach(var p in result.presets)if(p.curves==null||p.curves.Any(c=>c==null||string.IsNullOrEmpty(c.name)||!c.name.StartsWith("Expression_",StringComparison.Ordinal)||float.IsNaN(c.weight)||float.IsInfinity(c.weight)))throw new OriginalBindingException("Malformed host expression control recipe");catalog=result;return catalog;
            }
        }
        public static bool SupportsCode(int raw)=>raw==0||raw==2||raw==4||raw==6||raw==8||raw==10||raw==44||raw==47||raw==57;
        static string Canonical(string name){int dot=name.LastIndexOf('.');return dot>=0?name.Substring(dot+1):name;}
        public static bool TryPrepare(Transform actualBody,int raw,out NativeHostExpressionSubset projection,out string reason)
        {
            projection=null;reason=null;if(!SupportsCode(raw)){reason="No actual host semantic counterpart for raw Mii expression"+raw;return false;}if(!actualBody){reason="Actual source actor body unavailable";return false;}
            try
            {
                var data=Data;var required=data.presets.SelectMany(p=>p.curves).Select(c=>c.name).Distinct().ToArray();var candidate=new NativeHostExpressionSubset();
                foreach(var renderer in actualBody.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mesh=renderer.sharedMesh;if(!mesh||!renderer.enabled||!renderer.gameObject.activeInHierarchy||renderer.forceRenderingOff||mesh.vertexCount==0||renderer.sharedMaterials.All(m=>!m||!m.shader||!m.shader.isSupported))continue;
                    var names=Enumerable.Range(0,mesh.blendShapeCount).Select(mesh.GetBlendShapeName).ToArray();var canonical=names.Select(Canonical).ToArray();var available=required.Where(n=>canonical.Count(x=>x==n)==1).ToArray();if(available.Length==0)continue;
                    var control=Enumerable.Range(0,names.Length).Where(i=>canonical[i].StartsWith("Expression_",StringComparison.Ordinal)).ToArray();if(control.Length==0)continue;
                    // Float parameters and retained NAMES are insufficient: a
                    // visible actual mesh must contain real expression deltas.
                    var delta=new Vector3[mesh.vertexCount];var geometric=new HashSet<string>(StringComparer.Ordinal);
                    foreach(string requiredName in available){int index=Array.IndexOf(canonical,requiredName);if(mesh.GetBlendShapeFrameCount(index)==0)continue;mesh.GetBlendShapeFrameVertices(index,mesh.GetBlendShapeFrameCount(index)-1,delta,null,null);if(delta.Any(d=>d.sqrMagnitude>0.000000000001f))geometric.Add(requiredName);}
                    var codes=data.presets.Where(p=>p.neutral?geometric.Count>0:p.curves.Where(c=>c.weight!=0).All(c=>geometric.Contains(c.name))&&p.curves.Any(c=>c.weight!=0)).Select(p=>p.sourceCode).ToArray();
                    if(!codes.Contains(raw))continue;candidate.targets.Add(new Target{renderer=renderer,mesh=mesh,indices=control,names=control.Select(i=>canonical[i]).ToArray(),before=control.Select(renderer.GetBlendShapeWeight).ToArray(),last=new float[control.Length],geometricCodes=codes});
                }
                if(candidate.targets.Count==0){reason="No visible actual mesh with all required source-named expression controls and nonzero geometric deltas for EVERY nonzero control in the requested preset";return false;}projection=candidate;return true;
            }catch(Exception e){reason=e.Message;return false;}
        }
        public bool Apply(int raw,out string reason)
        {
            reason=null;if(disposed){reason="Expression projection disposed";return false;}var preset=Data.presets.SingleOrDefault(p=>p.sourceCode==raw);if(preset==null){reason="Unsupported raw Mii expression"+raw;return false;}
            if(targets.Any(t=>!t.renderer||!t.renderer.enabled||t.renderer.forceRenderingOff||t.renderer.sharedMesh!=t.mesh||!t.renderer.gameObject.activeInHierarchy||!t.geometricCodes.Contains(raw))){reason="Actual expression mesh/appearance changed";return false;}
            var values=preset.curves.ToDictionary(c=>c.name,c=>c.weight);foreach(var target in targets){for(int i=0;i<target.indices.Length;i++){float value=values.TryGetValue(target.names[i],out var authored)?authored:0;target.renderer.SetBlendShapeWeight(target.indices[i],value);target.last[i]=value;}target.written=true;}return true;
        }
        public int BoundRendererCount=>targets.Count;
        public int BoundShapeCount=>targets.Sum(t=>t.indices.Length);
        public void Dispose()
        {
            if(disposed)return;disposed=true;foreach(var target in targets)if(target.written&&target.renderer&&target.renderer.sharedMesh==target.mesh)for(int i=0;i<target.indices.Length;i++)if(Mathf.Abs(target.renderer.GetBlendShapeWeight(target.indices[i])-target.last[i])<.001f)target.renderer.SetBlendShapeWeight(target.indices[i],target.before[i]);targets.Clear();
        }
    }
}
