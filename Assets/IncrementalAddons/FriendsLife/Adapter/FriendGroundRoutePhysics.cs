using System;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Staff;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    // Queries the existing physical world only. The copied capsule is the body
    // envelope; the smaller navigation radius is not substituted for it.
    public sealed class FriendGroundRoutePhysics
    {
        public readonly ResidentController Actor;
        public readonly NavMeshAgent Agent;
        public readonly Transform OriginalNpc;
        public readonly float Radius, Height, Climb, SlopeCos;
        public readonly NavMeshQueryFilter Filter;
        public FriendGroundRoutePhysics(ResidentController actor, Transform original)
        {
            Actor=actor;OriginalNpc=original;Agent=actor.GetComponent<NavMeshAgent>();
            var shape=actor.GetComponent<CapsuleCollider>();
            if(!shape||shape.direction!=1||Vector3.Dot(actor.transform.up,Vector3.up)<.999f)throw new InvalidOperationException("Upright actual capsule required.");
            if(Mathf.Abs(shape.center.x)>.001f||Mathf.Abs(shape.center.z)>.001f||Mathf.Abs(shape.center.y-shape.height*.5f)>.001f)
                throw new InvalidOperationException("Actual root-to-feet capsule convention differs; keep route pending.");
            Radius=shape.radius*Mathf.Max(Mathf.Abs(actor.transform.lossyScale.x),Mathf.Abs(actor.transform.lossyScale.z));
            Height=Mathf.Max(Radius*2,shape.height*Mathf.Abs(actor.transform.lossyScale.y));
            var settings=NavMesh.GetSettingsByID(Agent.agentTypeID);Climb=settings.agentClimb;SlopeCos=Mathf.Cos(settings.agentSlope*Mathf.Deg2Rad);
            Filter=new NavMeshQueryFilter{agentTypeID=Agent.agentTypeID,areaMask=Agent.areaMask};
        }
        public bool IsCharacter(Collider c)=>c.GetComponentInParent<ResidentController>()||c.GetComponentInParent<PPG_Player>()||c.GetComponentInParent<StaffNpc>()||
            c.GetComponentInParent<SushiAndFish.Casino.Original.NPC>()||OriginalNpc&&c.transform.IsChildOf(OriginalNpc);
        public bool Clear(Vector3 foot)
            =>!Physics.OverlapCapsule(foot+Vector3.up*(Radius+.04f),foot+Vector3.up*(Height-Radius+.04f),Radius,~0,QueryTriggerInteraction.Ignore)
                .Any(c=>c&&!c.transform.IsChildOf(Actor.transform));
        public bool Foot(Vector3 around,float maximumY,out RaycastHit hit)
        {
            hit=Physics.RaycastAll(new Vector3(around.x,maximumY+1,around.z),Vector3.down,Height+8,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.collider&&!IsCharacter(h.collider)&&h.normal.y>=SlopeCos&&h.point.y<=maximumY+.005f)
                .OrderBy(h=>Mathf.Abs(h.point.y-around.y)).FirstOrDefault();
            return hit.collider;
        }
        public bool SamplePhysical(Vector3 foot,float radius,out Vector3 point)
        {
            point=default;
            if(!NavMesh.SamplePosition(foot+Vector3.up*.04f,out var nav,radius,Filter)||Mathf.Abs(nav.position.y-foot.y)>.12f||!Clear(nav.position))return false;
            point=nav.position;return true;
        }
        // A short source-ground step may use the original climb allowance.
        // The body is checked at that permitted foot lift, retaining lateral
        // walls/headroom and continuous real support instead of adding a ramp.
        public bool ShortStep(Vector3 a,Vector3 b)
            =>ShortStep(a,b,out _);
        public bool ShortStep(Vector3 a,Vector3 b,out string reason)
        {
            reason=null;
            if(Vector3.Distance(a,b)>Radius*4+1||Mathf.Abs(a.y-b.y)>Climb+.005f){reason="endpoint-distance-or-climb";return false;}
            int count=Mathf.CeilToInt(Vector3.Distance(a,b)/.1f);
            float raised=Mathf.Max(a.y,b.y);
            for(int i=0;i<=count;i++)
            {
                var q=Vector3.Lerp(a,b,(float)i/Mathf.Max(1,count));
                if(!Foot(new Vector3(q.x,raised,q.z),raised+.04f,out var ground)){reason="step-floor-missing@"+q.ToString("F4");return false;}
                if(Mathf.Abs(ground.point.y-q.y)>Climb+.05f){reason="step-height@"+q.ToString("F4")+"/physical="+ground.point.ToString("F4");return false;}
                var body=ground.point;body.y=Mathf.Max(raised,ground.point.y);
                if(!Clear(body)){reason="step-body@"+body.ToString("F4");return false;}
            }
            return true;
        }
        public bool ValidatePath(NavMeshPath path,Bounds region,Vector3 linkA,Vector3 linkB,bool hasLink)
            =>ValidatePath(path,region,linkA,linkB,hasLink,out _);
        public bool ValidatePath(NavMeshPath path,Bounds region,Vector3 linkA,Vector3 linkB,bool hasLink,out string reason)
            =>FriendGroundSurfacePath.Validate(this,path,region,linkA,linkB,hasLink,out reason);
        static float DistanceToSegment(Vector3 p,Vector3 a,Vector3 b)
        {var d=b-a;float t=d.sqrMagnitude>0?Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude):0;return Vector3.Distance(p,a+d*t);}
    }
}
