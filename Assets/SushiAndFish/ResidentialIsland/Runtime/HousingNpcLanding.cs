using System.Collections.Generic;
using PaperPixelGames.Core.Ai.NPCs;
using UnityEngine;
using UnityEngine.AI;
namespace SushiAndFish.ResidentialIsland
{
    public struct HousingPrivacyVolume
    {
        public Vector3 center;public Quaternion rotation;public Vector2 size;
        public Vector3 Local(Vector3 p)=>Quaternion.Inverse(rotation)*(p-center);
        public bool Contains(Vector3 p,float margin)
        {var v=Local(p);return v.y>-3&&v.y<8&&Mathf.Abs(v.x)<size.x/2+margin&&Mathf.Abs(v.z)<size.y/2+margin;}
        public float Distance(Vector3 p)
        {var v=Local(p);return new Vector2(Mathf.Max(0,Mathf.Abs(v.x)-size.x/2),Mathf.Max(0,Mathf.Abs(v.z)-size.y/2)).magnitude;}
        public static HousingPrivacyVolume Of(HousingHouse house)=>new HousingPrivacyVolume{center=house.transform.position,rotation=house.transform.rotation,size=house.definition.footprint};
    }
    public static class HousingNpcLanding
    {
        public const float WallClearance=2;
        // Clip the actual NavMesh triangles against the four outside halfplanes
        // of each expanded home. The closest point of the remaining polygons is
        // considered first, with edge samples for dynamic capsule obstructions.
        public static bool Nearest(Vector3 origin,List<HousingPrivacyVolume> volumes,ResidentController moving,IReadOnlyList<Vector3> reserved,out Vector3 result)
        {
            var mesh=NavMesh.CalculateTriangulation();Vector3 best=origin;float distance=float.PositiveInfinity;
            for(int i=0;i<mesh.indices.Length;i+=3)
            {
                var a=mesh.vertices[mesh.indices[i]];var b=mesh.vertices[mesh.indices[i+1]];var c=mesh.vertices[mesh.indices[i+2]];
                if(Mathf.Min(a.y,b.y,c.y)>origin.y+3||Mathf.Max(a.y,b.y,c.y)<origin.y-3)continue;
                var pieces=new List<List<Vector3>>{new List<Vector3>{a,b,c}};
                foreach(var volume in volumes)
                {
                    var next=new List<List<Vector3>>();
                    foreach(var polygon in pieces)
                    {
                        bool whollyOutside=false;
                        for(int side=0;side<4;side++)if(polygon.TrueForAll(p=>Signed(volume,p,side)>=0)){whollyOutside=true;break;}
                        if(whollyOutside){next.Add(polygon);continue;}
                        for(int side=0;side<4;side++){var clipped=Clip(polygon,volume,side);if(clipped.Count>=3)next.Add(clipped);}
                    }
                    pieces=next;if(pieces.Count==0)break;
                }
                foreach(var polygon in pieces)
                {
                    for(int n=1;n<polygon.Count-1;n++)Consider(Closest(origin,polygon[0],polygon[n],polygon[n+1]));
                    for(int n=0;n<polygon.Count;n++)
                    {
                        var p=polygon[n];var q=polygon[(n+1)%polygon.Count];int count=Mathf.Min(80,Mathf.CeilToInt(Vector3.Distance(p,q)/.4f));
                        for(int k=0;k<=count;k++)Consider(Vector3.Lerp(p,q,(float)k/Mathf.Max(1,count)));
                    }
                }
            }
            result=best;return !float.IsPositiveInfinity(distance);
            void Consider(Vector3 point)
            {
                float d=(point-origin).sqrMagnitude;if(d>=distance||Mathf.Abs(point.y-origin.y)>3)return;
                if(!NavMesh.SamplePosition(point,out var hit,.12f,NavMesh.AllAreas))return;point=hit.position;
                foreach(var volume in volumes)if(volume.Distance(point)<WallClearance-.015f)return;
                if(reserved!=null)foreach(var p in reserved)if(Vector3.Distance(p,point)<.9f)return;
                foreach(var collider in Physics.OverlapCapsule(point+Vector3.up*.34f,point+Vector3.up*1.5f,.28f,~0,QueryTriggerInteraction.Ignore))
                {
                    if(moving&&collider.transform.IsChildOf(moving.transform))continue;
                    return;
                }
                best=point;distance=(point-origin).sqrMagnitude;
            }
        }
        static float Signed(HousingPrivacyVolume v,Vector3 point,int side)
        {var p=v.Local(point);return side==0?-p.x-v.size.x/2-WallClearance:side==1?p.x-v.size.x/2-WallClearance:side==2?-p.z-v.size.y/2-WallClearance:p.z-v.size.y/2-WallClearance;}
        static List<Vector3> Clip(List<Vector3> source,HousingPrivacyVolume v,int side)
        {
            var output=new List<Vector3>();var previous=source[source.Count-1];float before=Signed(v,previous,side);
            foreach(var point in source)
            {
                float after=Signed(v,point,side);
                if((before>=0)!=(after>=0))output.Add(Vector3.Lerp(previous,point,before/(before-after)));
                if(after>=0)output.Add(point);previous=point;before=after;
            }
            return output;
        }
        static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float denominator=1/(va+vb+vc);return a+ab*(vb*denominator)+ac*(vc*denominator);
        }
    }
}
