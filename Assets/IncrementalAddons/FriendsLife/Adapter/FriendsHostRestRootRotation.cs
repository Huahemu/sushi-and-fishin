using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SushiAndFish.FriendsLife
{
    // The existing host pose clips encode their whole-body recline in RootQ.
    // Our furniture graph disables root motion to pin the actual furniture
    // anchor. Reapply only that original authored rotation, not its translation,
    // to the visual root before measuring/alignment of the real pelvis.
    public static class FriendsHostRestRootRotation
    {
        [Serializable] sealed class Row{public string clip,source;public Quaternion rotation;}
        [Serializable] sealed class Table{public Row[] rows;}
        static Table table;
        static Table Data
        {
            get
            {
                if(table!=null)return table;
                var asset=Resources.Load<TextAsset>("FriendsLife/HostTemplates/RestRootRotations");
                if(!asset)throw new InvalidOperationException("Original host rest RootQ data is unavailable");
                table=JsonUtility.FromJson<Table>(asset.text);
                if(table?.rows==null||table.rows.Length!=4)throw new InvalidOperationException("Original host rest RootQ data is incomplete");
                return table;
            }
        }
        public static bool Ready
        {get{try{return Data.rows.All(r=>!string.IsNullOrEmpty(r.clip)&&r.rotation.x*r.rotation.x+r.rotation.y*r.rotation.y+r.rotation.z*r.rotation.z+r.rotation.w*r.rotation.w>.99f);}catch{return false;}}}
        public static Quaternion Blend(AnimationMixerPlayable mixer,AnimationClipPlayable[] clips)
        {
            var sum=Vector4.zero;float total=0;
            for(int i=0;i<clips.Length;i++)
            {
                float weight=mixer.GetInputWeight(i);if(weight<=0)continue;
                var clip=clips[i].GetAnimationClip();var row=Data.rows.FirstOrDefault(r=>r.clip==clip.name);
                var q=row==null?Quaternion.identity:row.rotation;if(q.w<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                sum+=new Vector4(q.x,q.y,q.z,q.w)*weight;total+=weight;
            }
            if(total<=0)return Quaternion.identity;
            return Quaternion.Normalize(new Quaternion(sum.x,sum.y,sum.z,sum.w));
        }
    }
}
