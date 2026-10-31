using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bozo.ModularCharacters;
using PaperPixelGames.Core.Staff;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Pure-render clone for a NEW owned employee only. The original donor
    // animator continues to run the unchanged work/held-item logic. No original
    // animator/RPC/body field is replaced; only this new employee's donor skin
    // renderer is hidden while its friend render clone mirrors bone rotations.
    public sealed class FriendStaffVisual:MonoBehaviour
    {
        static readonly FieldInfo AnimatorField=typeof(StaffNpc).GetField("animator",BindingFlags.Instance|BindingFlags.NonPublic);
        StaffNpc npc;GameObject owned;string personId;float retry;
        readonly List<Tuple<Transform,Transform>> pairs=new List<Tuple<Transform,Transform>>();
        readonly Dictionary<Renderer,bool> hidden=new Dictionary<Renderer,bool>();
        void Awake(){npc=GetComponent<StaffNpc>();}
        void LateUpdate()
        {
            var economy=FriendsEconomyService.Instance;
            if(!npc||!economy||!FriendStaffPort.IsOwnedEntry(npc.EmployeeId.Value)||!economy.BoundEmployee(npc.EmployeeId.Value)){Destroy(this);return;}
            if(!owned)
            {
                if(Time.unscaledTime<retry)return;retry=Time.unscaledTime+.5f;personId=economy.PersonForEmployee(npc.EmployeeId.Value);var source=FriendsLifeRuntime.Instance?.ActorOf(personId)?.VisualAnimator;var donor=AnimatorField?.GetValue(npc)as Animator;if(!source||!donor)return;
                Build(source,donor);
            }
            foreach(var pair in pairs)if(pair.Item1&&pair.Item2)pair.Item2.localRotation=pair.Item1.localRotation;
        }
        void Build(Animator source,Animator donor)
        {
            owned=new GameObject("Friend owned employee visual");owned.transform.SetParent(transform,false);owned.transform.localPosition=donor.transform.localPosition;owned.transform.localRotation=donor.transform.localRotation;owned.transform.localScale=source.transform.localScale;
            var sourceSystem=source.GetComponentInParent<OutfitSystem>();var sourceRoot=sourceSystem?sourceSystem.transform:source.transform;
            var map=new Dictionary<Transform,Transform>();Copy(sourceRoot,owned.transform,map,true);var donorBones=donor.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            foreach(var item in map)if(item.Key!=source.transform&&donorBones.TryGetValue(item.Key.name,out var driver))pairs.Add(Tuple.Create(driver,item.Value));
            int rendered=0;
            foreach(var skin in sourceRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var system=skin.GetComponentInParent<OutfitSystem>();if(system&&system.GetCharacterBody()!=skin)continue;
                if(!system&&!RelativeActive(skin.transform,sourceRoot))continue;
                if(!map.TryGetValue(skin.transform,out var target)||skin.bones.Any(b=>b&&!map.ContainsKey(b)))continue;var render=target.gameObject.AddComponent<SkinnedMeshRenderer>();render.sharedMesh=skin.sharedMesh;render.sharedMaterials=skin.sharedMaterials;render.bones=skin.bones.Select(b=>b?map[b]:null).ToArray();render.rootBone=skin.rootBone&&map.ContainsKey(skin.rootBone)?map[skin.rootBone]:null;render.localBounds=skin.localBounds;render.updateWhenOffscreen=true;render.shadowCastingMode=skin.shadowCastingMode;render.receiveShadows=skin.receiveShadows;
                for(int i=0;i<skin.sharedMaterials.Length;i++){var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block,i);render.SetPropertyBlock(block,i);}if(skin.sharedMesh)for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)render.SetBlendShapeWeight(i,skin.GetBlendShapeWeight(i));
                rendered++;
            }
            if(rendered==0){Destroy(owned);owned=null;pairs.Clear();return;}
            foreach(var render in donor.GetComponentsInChildren<SkinnedMeshRenderer>(true)){hidden[render]=render.enabled;render.enabled=false;}
        }
        static void Copy(Transform source,Transform target,Dictionary<Transform,Transform> map,bool root)
        {
            map[source]=target;if(!root){target.localPosition=source.localPosition;target.localRotation=source.localRotation;target.localScale=source.localScale;}
            foreach(Transform child in source){var clone=new GameObject(child.name).transform;clone.SetParent(target,false);Copy(child,clone,map,false);clone.gameObject.SetActive(child.gameObject.activeSelf);}
        }
        static bool RelativeActive(Transform item,Transform root){while(item&&item!=root){if(!item.gameObject.activeSelf)return false;item=item.parent;}return item==root;}
        void OnDestroy(){foreach(var item in hidden)if(item.Key)item.Key.enabled=item.Value;hidden.Clear();pairs.Clear();if(owned)Destroy(owned);}
    }
}
