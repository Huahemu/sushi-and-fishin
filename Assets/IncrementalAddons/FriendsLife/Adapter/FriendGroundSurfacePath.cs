using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    // NavMeshPath corners are funnel turns, not a sampled height profile.
    // Follow the actual same-type detail surface, retaining all physical gates.
    public static class FriendGroundSurfacePath
    {
        sealed class Failure
        {public string code,reason;public Vector3 raw,actual,prior,direction;}
        public static bool Validate(FriendGroundRoutePhysics shape,NavMeshPath path,Bounds region,Vector3 linkA,Vector3 linkB,bool hasLink,out string reason)
        {bool ok=Trace(shape,path,region,linkA,linkB,hasLink,out var failure);reason=failure?.reason;return ok;}
        public static bool TryPlan(FriendGroundRoutePhysics shape,NavMeshPath path,Bounds region,Vector3 linkA,Vector3 linkB,bool hasLink,out Vector3[] targets,out string reason)
        {
            targets=Array.Empty<Vector3>();reason=null;
            if(Trace(shape,path,region,linkA,linkB,hasLink,out var failure))
            {targets=new[]{path.corners[path.corners.Length-1]};return true;}
            reason=failure?.reason;
            // A bounded real detour is distinct from accepting the obstructed
            // straight route. Structural sample/link/climb failures stay rejected.
            if(failure==null||failure.code!="body-blocked"||path.corners.Length<2)return false;
            var start=path.corners[0];var end=path.corners[path.corners.Length-1];
            if(TryColliderOutline(shape,start,end,failure.actual,region,linkA,linkB,hasLink,out targets,out var outlineReason))
            {reason=outlineReason;return true;}
            var direction=Vector3.ProjectOnPlane(failure.direction,Vector3.up).normalized;
            if(direction.sqrMagnitude<.99f)return false;
            var side=Vector3.Cross(Vector3.up,direction);
            float[] pads={Mathf.Max(.6f,shape.Radius*2+.2f),Mathf.Max(1,shape.Radius*3+.3f)};
            float[] offsets={shape.Radius*1.5f+.1f,shape.Radius*2+.2f,shape.Radius*3+.2f,shape.Radius*4+.2f,2.4f,3f};
            // Keep the original24 first, then a finite36 widening bend. Actual
            // source queries proved the same-offset pair misses the ramp turn.
            var proposals=new List<(float pad,float first,float last,float sign)>();
            foreach(float pad in pads)foreach(float offset in offsets)foreach(float sign in new[]{1f,-1f})
                proposals.Add((pad,offset,offset,sign));
            foreach(float pad in pads)foreach(float first in new[]{.7f,1f,1.4f})foreach(float last in new[]{1.8f,2.4f,3f})foreach(float sign in new[]{1f,-1f})
                proposals.Add((pad,first,last,sign));
            int attempts=0;
            foreach(var proposal in proposals)
            {
                attempts++;
                var before=failure.actual-direction*proposal.pad+side*(proposal.first*proposal.sign);before.y=failure.prior.y;
                var after=failure.actual+direction*proposal.pad+side*(proposal.last*proposal.sign);after.y=failure.actual.y;
                if(!Inside(region,before)||!Inside(region,after)||!SampleWaypoint(shape,before,out var first)||!SampleWaypoint(shape,after,out var second))continue;
                var legs=new[]{first,second,end};var prior=start;bool pass=true;
                foreach(var target in legs)
                {
                    var leg=new NavMeshPath();
                    if(!NavMesh.CalculatePath(prior,target,shape.Filter,leg)||leg.status!=NavMeshPathStatus.PathComplete||
                        leg.corners.Length==0||Vector3.Distance(leg.corners[0],prior)>.15f||Vector3.Distance(leg.corners[leg.corners.Length-1],target)>.1f||
                        !Trace(shape,leg,region,linkA,linkB,hasLink,out _))
                    {pass=false;break;}
                    prior=target;
                }
                if(!pass)continue;
                targets=legs;reason="same-surface physical detour verified / attempts="+attempts+" / first="+first.ToString("F4")+" / second="+second.ToString("F4");return true;
            }
            reason="bounded same-surface detour unavailable / attempts="+attempts+" / outline="+outlineReason+" / original="+reason;return false;
        }
        static bool TryColliderOutline(FriendGroundRoutePhysics shape,Vector3 start,Vector3 end,Vector3 blocked,Bounds region,Vector3 linkA,Vector3 linkB,bool hasLink,out Vector3[] targets,out string reason,HashSet<int> seen=null,int remaining=1)
        {
            targets=Array.Empty<Vector3>();reason=null;
            var blockers=Physics.OverlapCapsule(blocked+Vector3.up*(shape.Radius+.04f),blocked+Vector3.up*(shape.Height-shape.Radius+.04f),shape.Radius,~0,QueryTriggerInteraction.Ignore)
                .Where(c=>c&&!c.transform.IsChildOf(shape.Actor.transform)&&!shape.IsCharacter(c)).ToArray();
            if(blockers.Length==0||blockers.Length>32)return false;
            if(seen!=null&&blockers.Any(c=>seen.Contains(c.GetInstanceID()))){reason="same source obstacle already visited";return false;}
            var visited=seen==null?new HashSet<int>():new HashSet<int>(seen);
            var bounds=blockers[0].bounds;foreach(var collider in blockers.Skip(1))bounds.Encapsulate(collider.bounds);
            // A real adjacent prop can close the side of a desk. Read only the
            // first blocker's one-hop body-width neighbourhood, not a grid or
            // progressively larger radius. Low possible steps are not promoted
            // into a tall obstacle; all actual Trace checks still apply.
            var center=bounds.center;center.y=blocked.y+shape.Height*.5f+.04f;
            var extents=bounds.extents;extents.x+=shape.Radius*2;extents.z+=shape.Radius*2;extents.y=shape.Height*.5f;
            var neighbours=Physics.OverlapBox(center,extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore)
                .Where(c=>c&&!c.transform.IsChildOf(shape.Actor.transform)&&!shape.IsCharacter(c)&&
                    c.bounds.max.y>blocked.y+shape.Climb+.005f&&Inside(region,c.bounds.center)).ToArray();
            if(neighbours.Length>32)return false;
            foreach(var collider in neighbours)bounds.Encapsulate(collider.bounds);
            foreach(var collider in blockers.Concat(neighbours))visited.Add(collider.GetInstanceID());
            // A physical-only radius can put a proposed turn inside the
            // original baked navigation erosion. Retain that original radius
            // and one original voxel when proposing the outline; never relax
            // the unchanged edge, support or capsule acceptance checks.
            var settings=NavMesh.GetSettingsByID(shape.Agent.agentTypeID);
            float margin=Mathf.Max(shape.Radius,settings.agentRadius)+settings.voxelSize;var min=bounds.min;var max=bounds.max;
            var corners=new[]{new Vector3(min.x-margin,blocked.y,min.z-margin),new Vector3(max.x+margin,blocked.y,min.z-margin),
                new Vector3(max.x+margin,blocked.y,max.z+margin),new Vector3(min.x-margin,blocked.y,max.z+margin)};
            var proposals=new List<Vector3[]>();
            for(int i=0;i<4;i++)
            {
                proposals.Add(new[]{corners[i],end});
                proposals.Add(new[]{corners[i],corners[(i+1)%4],end});
                proposals.Add(new[]{corners[i],corners[(i+3)%4],end});
            }
            int bestPrefix=-1;string bestReason=null;
            foreach(var proposal in proposals.OrderBy(p=>RouteLength(start,p)))
            {
                var actual=new List<Vector3>();bool pass=true;
                for(int i=0;i<proposal.Length-1;i++)
                {
                    if(!Inside(region,proposal[i])||!SampleWaypoint(shape,proposal[i],out var point))
                    {if(bestPrefix<0)bestReason="outline waypoint rejected / point="+proposal[i].ToString("F5");pass=false;break;}actual.Add(point);
                }
                if(!pass)continue;actual.Add(end);var prior=start;var assembled=new List<Vector3>();int repairs=remaining;
                foreach(var target in actual)
                {
                    var leg=new NavMeshPath();var privacy=FriendHousingIsland.Instance?.GetComponent<FriendHousingPrivacy>();
                    if(!NavMesh.CalculatePath(prior,target,shape.Filter,leg)||leg.status!=NavMeshPathStatus.PathComplete||leg.corners.Length==0||
                        Vector3.Distance(leg.corners[0],prior)>.15f||Vector3.Distance(leg.corners[leg.corners.Length-1],target)>.1f||
                        privacy&&privacy.ExcludesPathFor(shape.Actor,leg))
                    {if(assembled.Count>bestPrefix){bestPrefix=assembled.Count;bestReason="outline complete path/endpoint/privacy rejected / target="+target.ToString("F5");}pass=false;break;}
                    if(!Trace(shape,leg,region,linkA,linkB,hasLink,out var failure))
                    {
                        string nextReason=null;
                        // The actual desk-to-machine read found a second real
                        // body obstacle. Repair at most that one later blocker
                        // with the same finite outline, retaining every Trace.
                        if(repairs>0&&failure?.code=="body-blocked"&&TryColliderOutline(shape,prior,target,failure.actual,region,linkA,linkB,hasLink,out var next,out nextReason,visited,repairs-1))
                        {assembled.AddRange(next);prior=target;repairs=0;continue;}
                        if(assembled.Count>bestPrefix){bestPrefix=assembled.Count;bestReason="outline leg rejected / "+failure?.reason+" / later="+nextReason;}
                        pass=false;break;
                    }
                    assembled.Add(target);prior=target;
                }
                if(!pass)continue;
                targets=assembled.ToArray();reason="actual source collider-outline detour / blockers="+blockers.Length+" / neighbours="+neighbours.Length+" / turns="+(targets.Length-1)+" / laterRepair="+(repairs<remaining);return true;
            }
            reason=bestReason;return false;
        }
        static float RouteLength(Vector3 start,Vector3[] targets)
        {float length=0;foreach(var target in targets){length+=Vector3.Distance(start,target);start=target;}return length;}
        static bool SampleWaypoint(FriendGroundRoutePhysics shape,Vector3 requested,out Vector3 point)
        {
            point=default;
            if(!NavMesh.SamplePosition(requested+Vector3.up*.04f,out var nav,shape.Climb+.12f,shape.Filter)||
                FlatDistance(nav.position,requested)>.05f||!shape.Foot(nav.position,nav.position.y+.12f,out var floor)||
                Mathf.Abs(floor.point.y-nav.position.y)>.12f||!shape.Clear(nav.position))return false;
            point=nav.position;return true;
        }
        static bool Trace(FriendGroundRoutePhysics shape,NavMeshPath path,Bounds region,Vector3 linkA,Vector3 linkB,bool hasLink,out Failure failure)
        {
            failure=null;
            if(shape==null||path==null||path.status!=NavMeshPathStatus.PathComplete||path.corners.Length==0)
            {failure=new Failure{code="nav-status",reason="Actual complete source navigation path required"};return false;}
            if(hasLink&&!shape.ShortStep(linkA,linkB,out var seamReason))
            {failure=new Failure{code="source-seam",reason="Original physical short seam rejected / "+seamReason};return false;}
            var corners=path.corners;Vector3 prior=corners[0];bool havePrior=false,priorSeam=false;int total=0;
            if(corners.Length==1)return Point(shape,corners[0],ref prior,ref havePrior,ref priorSeam,region,linkA,linkB,hasLink,Vector3.forward,out failure);
            for(int n=1;n<corners.Length;n++)
            {
                var a=corners[n-1];var b=corners[n];int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/.1f));
                for(int i=n==1?0:1;i<=count;i++)
                {
                    var raw=Vector3.Lerp(a,b,(float)i/count);
                    bool sourcePoint=Inside(region,raw)||hasLink&&OnSeam(raw,linkA,linkB,out _);
                    if(sourcePoint&&++total>4096){failure=new Failure{code="scope",reason="Source route exceeds the bounded4096 surface-query scope"};return false;}
                    if(!Point(shape,raw,ref prior,ref havePrior,ref priorSeam,region,linkA,linkB,hasLink,b-a,out failure))return false;
                }
            }
            return true;
        }
        static bool Point(FriendGroundRoutePhysics shape,Vector3 raw,ref Vector3 prior,ref bool havePrior,ref bool priorSeam,Bounds region,Vector3 linkA,Vector3 linkB,bool hasLink,Vector3 direction,out Failure failure)
        {
            failure=null;bool seam=hasLink&&OnSeam(raw,linkA,linkB,out float seamT)&&
                Mathf.Abs(raw.y-Vector3.Lerp(linkA,linkB,seamT).y)<=shape.Climb+.12f;Vector3 actual;
            // Public island legs retain the original NPC path authority. All
            // source-region and verified seam samples keep the strict gates.
            if(!seam&&!Inside(region,raw)){prior=raw;havePrior=false;priorSeam=false;return true;}
            if(seam)
            {
                OnSeam(raw,linkA,linkB,out float t);actual=Vector3.Lerp(linkA,linkB,t);
                if(!shape.Foot(actual,actual.y+shape.Climb,out var support)||Mathf.Abs(support.point.y-actual.y)>shape.Climb+.05f)
                    return Reject("source-seam","Source seam support/climb",raw,actual,prior,direction,out failure);
                var body=actual;body.y=Mathf.Max(actual.y,Mathf.Max(linkA.y,linkB.y));
                if(!shape.Clear(body))return Reject("source-seam","Original raised short-step capsule",raw,actual,prior,direction,out failure);
            }
            else
            {
                var hint=new Vector3(raw.x,havePrior?prior.y:raw.y,raw.z);
                if(!NavMesh.SamplePosition(hint+Vector3.up*.04f,out var nav,shape.Climb+.12f,shape.Filter))
                    return Reject("nav-sample","Actual same-type surface sample missing",raw,raw,prior,direction,out failure);
                actual=nav.position;
                if(FlatDistance(actual,raw)>.05f)return Reject("xz","Actual surface sample changed path XZ",raw,actual,prior,direction,out failure);
                if(havePrior&&Mathf.Abs(actual.y-prior.y)>shape.Climb+.005f)
                    return Reject("climb","Actual consecutive surface exceeds original climb",raw,actual,prior,direction,out failure);
                if(havePrior&&!priorSeam&&NavMesh.Raycast(prior,actual,out _,shape.Filter))
                    return Reject("nav-edge","Actual source surface crosses a navigation edge",raw,actual,prior,direction,out failure);
                if(Inside(region,actual))
                {
                    if(!shape.Foot(actual,actual.y+.12f,out var floor)||Mathf.Abs(floor.point.y-actual.y)>.12f)
                        return Reject("support","Actual source surface support mismatch",raw,actual,prior,direction,out failure);
                    if(!shape.Clear(actual))return Reject("body-blocked","Actual surface capsule blocked",raw,actual,prior,direction,out failure);
                }
            }
            prior=actual;havePrior=true;priorSeam=seam;return true;
        }
        static bool Reject(string code,string text,Vector3 raw,Vector3 actual,Vector3 prior,Vector3 direction,out Failure failure)
        {failure=new Failure{code=code,raw=raw,actual=actual,prior=prior,direction=direction,reason=text+" / raw="+raw.ToString("F5")+" / surface="+actual.ToString("F5")+" / prior="+prior.ToString("F5")};return false;}
        static bool Inside(Bounds b,Vector3 p)=>p.x>=b.min.x&&p.x<=b.max.x&&p.z>=b.min.z&&p.z<=b.max.z;
        static float FlatDistance(Vector3 a,Vector3 b)=>Vector3.ProjectOnPlane(a-b,Vector3.up).magnitude;
        static bool OnSeam(Vector3 p,Vector3 a,Vector3 b,out float t)
        {
            var delta=Vector3.ProjectOnPlane(b-a,Vector3.up);float length=delta.sqrMagnitude;
            t=length>0?Mathf.Clamp01(Vector3.Dot(Vector3.ProjectOnPlane(p-a,Vector3.up),delta)/length):0;
            return length>.000001f&&FlatDistance(p,Vector3.Lerp(a,b,t))<.18f&&
                Vector3.Dot(Vector3.ProjectOnPlane(p-a,Vector3.up),delta)>=-.01f&&Vector3.Dot(Vector3.ProjectOnPlane(p-b,Vector3.up),delta)<=.01f;
        }
    }
}
