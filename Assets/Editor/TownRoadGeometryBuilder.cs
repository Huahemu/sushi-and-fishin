using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class IslandRevision20261018
{
    sealed class RoadGeometry {
        public readonly List<Vector3> vertices=new List<Vector3>();public readonly List<Vector2> uv=new List<Vector2>();public readonly List<int> triangles=new List<int>();
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});foreach(var p in new[]{a,b,c,d})uv.Add(new Vector2(p.x*.3f,p.z*.3f));triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
        public void FlatRect(Rect rect,Func<float,float,float> y){Quad(new Vector3(rect.xMin,y(rect.xMin,rect.yMin),rect.yMin),new Vector3(rect.xMin,y(rect.xMin,rect.yMax),rect.yMax),new Vector3(rect.xMax,y(rect.xMax,rect.yMax),rect.yMax),new Vector3(rect.xMax,y(rect.xMax,rect.yMin),rect.yMin));}
        public void Strip(Vector3 a,Vector3 b,float width,float offset,Func<float,float,float> y){var direction=(b-a).normalized;var side=new Vector3(-direction.z,0,direction.x);int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/(correctionBenchBay?.35f:2f)));for(int i=0;i<steps;i++){var p=Vector3.Lerp(a,b,(float)i/steps)+side*offset;var q=Vector3.Lerp(a,b,(float)(i+1)/steps)+side*offset;int across=correctionBenchBay&&Mathf.Abs(width-2.4f)<.01f?8:1;for(int j=0;j<across;j++){float lo=-width/2+width*j/across,hi=-width/2+width*(j+1)/across;var x=p+side*lo;var z=p+side*hi;var w=q+side*hi;var v=q+side*lo;x.y=y(x.x,x.z);z.y=y(z.x,z.z);w.y=y(w.x,w.z);v.y=y(v.x,v.z);Quad(x,z,w,v);}}}
        public void Wall(Vector3 a,Vector3 b,float bottomA,float bottomB){Quad(new Vector3(a.x,bottomA,a.z),new Vector3(b.x,bottomB,b.z),b,a);}
    }
    sealed class DeckSample {public Vector2 point;public float y;}
    static List<DeckSample> roadDeckSamples;
    static TownRoadPlan activeRoadPlan;
    static Terrain[] roadLands;
    static string roadOutput;
    static int roadMeshCount;
    static float DeckY(float x,float z){float value=activeRoadPlan.roadTopY;var p=new Vector2(x,z);foreach(var s in roadDeckSamples)value=Mathf.Max(value,s.y-.06f*Mathf.Max(0,Vector2.Distance(p,s.point)-6.7f));return value;}
    static void PrepareDeck(TownRoadPlan plan,Terrain[] lands){activeRoadPlan=plan;roadLands=lands;roadDeckSamples=new List<DeckSample>();float half=plan.asphaltWidth/2+plan.curbWidth+plan.sidewalkWidth;foreach(var r in plan.roads){var direction=(r.b.Vector-r.a.Vector).normalized;var side=new Vector3(-direction.z,0,direction.x);float len=Vector3.Distance(r.a.Vector,r.b.Vector);int count=Mathf.CeilToInt(len/2);for(int i=0;i<=count;i++){var p=Vector3.Lerp(r.a.Vector,r.b.Vector,(float)i/count);float y=plan.roadTopY;for(float d=-half;d<=half+.01f;d+=.6f){var q=p+side*d;y=Mathf.Max(y,PlanningGround(lands,q.x,q.z)+.05f);}roadDeckSamples.Add(new DeckSample{point=new Vector2(p.x,p.z),y=y});}}}
    static Transform RoadGroup(Transform parent,string name,Vector3 position){var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=position;return go.transform;}
    static GameObject RoadMesh(Transform parent,string name,RoadGeometry g,Material material,bool collision,bool shadow=true){if(g.vertices.Count==0)return null;var mesh=new Mesh{name=name,indexFormat=g.vertices.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(g.vertices.Select(p=>p-parent.position).ToList());mesh.SetUVs(0,g.uv);mesh.SetTriangles(g.triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();var path=roadOutput+"/Meshes/"+name+"_"+(++roadMeshCount).ToString("D3")+".asset";AssetDatabase.CreateAsset(mesh,path);var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=shadow?ShadowCastingMode.On:ShadowCastingMode.Off;renderer.receiveShadows=true;if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;}
    static Material RoadColor(string name,Color color){var material=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/M_asphalt_01 1.mat")){name=name};material.SetTexture("_BaseMap",null);material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.12f);AssetDatabase.CreateAsset(material,roadOutput+"/Materials/"+name+".mat");return material;}
    static List<Vector2> RoadNodes(TownRoadPlan plan){var nodes=new List<Vector2>();void Add(float x,float z){var p=new Vector2(x,z);if(!nodes.Any(q=>Vector2.Distance(p,q)<.02f))nodes.Add(p);}foreach(var r in plan.roads){Add(r.a.x,r.a.z);Add(r.b.x,r.b.z);}foreach(var a in plan.roads.Where(r=>Mathf.Abs(r.a.z-r.b.z)<.01f))foreach(var b in plan.roads.Where(r=>Mathf.Abs(r.a.x-r.b.x)<.01f))if(b.a.x>=Mathf.Min(a.a.x,a.b.x)-.01f&&b.a.x<=Mathf.Max(a.a.x,a.b.x)+.01f&&a.a.z>=Mathf.Min(b.a.z,b.b.z)-.01f&&a.a.z<=Mathf.Max(b.a.z,b.b.z)+.01f)Add(b.a.x,a.a.z);return nodes;}
    static Vector2[] RoadArms(Vector2 node,TownRoadPlan plan){var arms=new List<Vector2>();foreach(var d in new[]{Vector2.right,Vector2.left,Vector2.up,Vector2.down}){var p=node+d*1.5f;if(plan.roads.Any(r=>Mathf.Abs(r.a.x-r.b.x)<.01f?Mathf.Abs(p.x-r.a.x)<.02f&&p.y>=Mathf.Min(r.a.z,r.b.z)&&p.y<=Mathf.Max(r.a.z,r.b.z):Mathf.Abs(p.y-r.a.z)<.02f&&p.x>=Mathf.Min(r.a.x,r.b.x)&&p.x<=Mathf.Max(r.a.x,r.b.x)))arms.Add(d);}return arms.ToArray();}
    static bool OnPerpendicularRoad(Vector3 point,RoadLine current,TownRoadPlan plan){bool horizontal=Mathf.Abs(current.a.z-current.b.z)<.01f;return plan.roads.Any(r=>(Mathf.Abs(r.a.z-r.b.z)<.01f)!=horizontal&&LineRect(r,plan.asphaltWidth/2+.02f).Contains(new Vector2(point.x,point.z)));}
    static void BuildRoadLine(Transform network,RoadLine line,TownRoadPlan plan,Material asphalt,Material sidewalk,Material curb,Material white,Material foundation,List<Vector2> nodes){var middle=(line.a.Vector+line.b.Vector)/2;middle.y=DeckY(middle.x,middle.z);var root=RoadGroup(network,line.id,middle);var a=line.a.Vector;var b=line.b.Vector;var direction=(b-a).normalized;var side=new Vector3(-direction.z,0,direction.x);float length=Vector3.Distance(a,b),half=plan.asphaltWidth/2,total=half+plan.curbWidth+plan.sidewalkWidth;
        var road=new RoadGeometry();road.Strip(a,b,plan.asphaltWidth,0,DeckY);RoadMesh(root,line.id+"_Carriageway",road,asphalt,true);
        var pave=new RoadGeometry();var kerb=new RoadGeometry();var walls=new RoadGeometry();
        var cuts=new List<float>{0,length};foreach(var n in nodes){var p=new Vector3(n.x,0,n.y);float distance=Vector3.Dot(p-a,direction);float lateral=Mathf.Abs(Vector3.Dot(p-a,side));if(lateral<.05f&&distance>=-.05f&&distance<=length+.05f){cuts.Add(Mathf.Clamp(distance-half,0,length));cuts.Add(Mathf.Clamp(distance+half,0,length));}}cuts=cuts.Distinct().OrderBy(x=>x).ToList();
        foreach(float sign in new[]{-1f,1f})for(int i=0;i<cuts.Count-1;i++){float start=cuts[i],end=cuts[i+1];if(end-start<.01f)continue;var probe=a+direction*((start+end)/2)+side*(sign*(half+plan.curbWidth+plan.sidewalkWidth/2));if(OnPerpendicularRoad(probe,line,plan))continue;pave.Strip(a+direction*start,a+direction*end,plan.sidewalkWidth,sign*(half+plan.curbWidth+plan.sidewalkWidth/2),CorrectionPavementY);kerb.Strip(a+direction*start,a+direction*end,plan.curbWidth,sign*(half+plan.curbWidth/2),(x,z)=>DeckY(x,z)+.2f);
            int steps=Mathf.Max(1,Mathf.CeilToInt((end-start)/2));for(int k=0;k<steps;k++){var x=a+direction*Mathf.Lerp(start,end,(float)k/steps)+side*sign*total;var y=a+direction*Mathf.Lerp(start,end,(float)(k+1)/steps)+side*sign*total;x.y=DeckY(x.x,x.z)+.18f;y.y=DeckY(y.x,y.z)+.18f;float bx=PlanningGround(roadLands,x.x,x.z)-.06f,by=PlanningGround(roadLands,y.x,y.z)-.06f;if(sign>0)walls.Wall(y,x,by,bx);else walls.Wall(x,y,bx,by);}}
        RoadMesh(root,line.id+"_Sidewalks",pave,sidewalk,true);RoadMesh(root,line.id+"_Curbs",kerb,curb,true);RoadMesh(root,line.id+"_Roadbed",walls,foundation,true);
        var paint=new RoadGeometry();var skipNodes=nodes.Where(n=>{var arms=RoadArms(n,plan);return arms.Length!=2||Vector2.Dot(arms[0],arms[1])>-.9f;}).ToArray();bool NearNode(Vector3 p)=>skipNodes.Any(n=>Vector2.Distance(new Vector2(p.x,p.z),n)<10.8f);
        for(float d=1;d<length-1;d+=4){var p=a+direction*d;if(NearNode(p))continue;paint.Strip(p-direction,p+direction,.18f,0,(x,z)=>DeckY(x,z)+.015f);}
        for(float d=0;d<length;d+=2){var p=a+direction*Mathf.Min(length,d+1);if(NearNode(p))continue;foreach(float sign in new[]{-1f,1f})paint.Strip(a+direction*d,a+direction*Mathf.Min(length,d+2),.12f,sign*(half-.18f),(x,z)=>DeckY(x,z)+.015f);}
        RoadMesh(root,line.id+"_WhiteLaneMarkings",paint,white,false,false);
    }
}
