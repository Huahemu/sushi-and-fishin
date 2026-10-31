using System;
using System.Collections.Generic;
using System.Linq;
using SushiAndFish.ResidentialIsland;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    // NEW read-only landing query. It follows the baseline nearest-polygon
    // construction, using the moving actor's real agent filter and body test.
    // Source HousingNpcLanding, original actors and navigation data stay intact.
    public static class FriendHousingSafeLanding
    {
        public static bool Nearest(Vector3 origin,IReadOnlyList<HousingPrivacyVolume> homes,
            NavMeshAgent actualAgent,IReadOnlyList<Vector3> reserved,Func<Vector3,Vector3?> prepareBodyPose,out Vector3 result)
        {
            var mesh=NavMesh.CalculateTriangulation();float bestDistance=float.PositiveInfinity;Vector3 best=default;
            var filter=actualAgent?new NavMeshQueryFilter{agentTypeID=actualAgent.agentTypeID,areaMask=actualAgent.areaMask}:default;
            for(int index=0;index<mesh.indices.Length;index+=3)
            {
                var a=mesh.vertices[mesh.indices[index]];var b=mesh.vertices[mesh.indices[index+1]];var c=mesh.vertices[mesh.indices[index+2]];
                if(Mathf.Min(a.y,b.y,c.y)>origin.y+3||Mathf.Max(a.y,b.y,c.y)<origin.y-3)continue;
                if((Closest(origin,a,b,c)-origin).sqrMagnitude>=bestDistance)continue;
                var polygons=new List<List<Vector3>>{new List<Vector3>{a,b,c}};
                foreach(var home in homes)
                {
                    var next=new List<List<Vector3>>();
                    foreach(var polygon in polygons)
                    {
                        bool outside=false;for(int side=0;side<4;side++)if(polygon.TrueForAll(p=>Signed(home,p,side)>=0)){outside=true;break;}
                        if(outside){next.Add(polygon);continue;}
                        for(int side=0;side<4;side++){var clipped=Clip(polygon,home,side);if(clipped.Count>=3)next.Add(clipped);}
                    }
                    polygons=next;if(polygons.Count==0)break;
                }
                foreach(var polygon in polygons)
                {
                    for(int point=1;point<polygon.Count-1;point++)Consider(Closest(origin,polygon[0],polygon[point],polygon[point+1]));
                    for(int point=0;point<polygon.Count;point++)
                    {
                        var p=polygon[point];var q=polygon[(point+1)%polygon.Count];int count=Mathf.Min(80,Mathf.CeilToInt(Vector3.Distance(p,q)/.4f));
                        for(int sample=0;sample<=count;sample++)Consider(Vector3.Lerp(p,q,sample/(float)Mathf.Max(1,count)));
                    }
                }
            }
            result=best;return !float.IsPositiveInfinity(bestDistance);
            void Consider(Vector3 candidate)
            {
                if((candidate-origin).sqrMagnitude>=bestDistance||Mathf.Abs(candidate.y-origin.y)>3)return;
                NavMeshHit hit;bool found=actualAgent?NavMesh.SamplePosition(candidate,out hit,.12f,filter):NavMesh.SamplePosition(candidate,out hit,.12f,NavMesh.AllAreas);
                if(!found)return;candidate=hit.position;
                foreach(var home in homes)if(home.Distance(candidate)<HousingNpcLanding.WallClearance-.015f)return;
                var prepared=prepareBodyPose(candidate);if(!prepared.HasValue)return;candidate=prepared.Value;
                if(reserved!=null&&reserved.Any(p=>Vector3.Distance(p,candidate)<.9f))return;
                float distance=(candidate-origin).sqrMagnitude;if(distance>=bestDistance)return;best=candidate;bestDistance=distance;
            }
        }
        static float Signed(HousingPrivacyVolume home,Vector3 point,int side)
        {var p=home.Local(point);return side==0?-p.x-home.size.x/2-HousingNpcLanding.WallClearance:side==1?p.x-home.size.x/2-HousingNpcLanding.WallClearance:side==2?-p.z-home.size.y/2-HousingNpcLanding.WallClearance:p.z-home.size.y/2-HousingNpcLanding.WallClearance;}
        static List<Vector3> Clip(List<Vector3> polygon,HousingPrivacyVolume home,int side)
        {
            var result=new List<Vector3>();var previous=polygon[polygon.Count-1];float before=Signed(home,previous,side);
            foreach(var point in polygon){float after=Signed(home,point,side);if((before>=0)!=(after>=0))result.Add(Vector3.Lerp(previous,point,before/(before-after)));if(after>=0)result.Add(point);previous=point;before=after;}return result;
        }
        static Vector3 Closest(Vector3 point,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var ap=point-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
            var bp=point-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            var cp=point-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float inverse=1/(va+vb+vc);return a+ab*(vb*inverse)+ac*(vc*inverse);
        }
    }
}
