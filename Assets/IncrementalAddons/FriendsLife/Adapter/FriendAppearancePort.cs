using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bozo.ModularCharacters;
using PaperPixelGames.Core.Player.Visuals;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SushiAndFish.FriendsLife
{
    // Additive caller-owned port. Baseline Resources.Load/BMAC/OutfitSystem
    // methods remain unchanged. Only newly created friend/preview rig clones
    // receive this ownership marker and generated outfit configuration.
    public sealed class FriendAppearanceOwnedRig:MonoBehaviour {}
    public static class FriendAppearancePort
    {
        public static void MarkOwnedClone(OutfitSystem system)
        {if(!system)throw new ArgumentNullException(nameof(system));if(!system.GetComponent<FriendAppearanceOwnedRig>())system.gameObject.AddComponent<FriendAppearanceOwnedRig>();FriendNativeTopDiffuseSupport.Attach(system);}
        public static bool HasGenerated(CharacterData data)
        {return data?.outfitDatas!=null&&data.outfitDatas.Any(o=>!string.IsNullOrEmpty(o.outfit)&&o.outfit.Contains("/FriendsGenerated/"));}
        // Pure appearance-data translation: no rig, Resources lookup, package
        // registration or method interception. Native startup receives this
        // donor document; the NEW binding keeps the full generated document.
        public static CharacterData ToDonorData(CharacterData appearance)
        {
            if(appearance==null)return null;var donor=PlayerCustomizationSession.CloneCharacterDataForRuntime(appearance);if(donor.outfitDatas==null)return donor;var packages=new Dictionary<string,FriendGeneratedPackage>();
            for(int i=0;i<donor.outfitDatas.Count;i++)
            {
                var item=donor.outfitDatas[i];if(string.IsNullOrEmpty(item.outfit)||!item.outfit.Contains("/FriendsGenerated/"))continue;var pieces=item.outfit.Split('/');
                if(pieces.Length!=4||!Guid.TryParseExact(pieces[2],"N",out _))throw new InvalidOperationException("Invalid generated appearance path");string id=pieces[2];
                if(!packages.TryGetValue(id,out var package)){package=JsonUtility.FromJson<FriendGeneratedPackage>(FriendGeneratedModels.ExportPackage(id));packages[id]=package;}
                var part=package.parts.FirstOrDefault(p=>p.path==item.outfit);if(part==null||string.IsNullOrEmpty(part.sourceOutfit))throw new InvalidOperationException("Generated donor metadata missing");item.outfit=part.sourceOutfit;if(item.colors==null)item.colors=Enumerable.Repeat(item.color,9).ToList();donor.outfitDatas[i]=item;
            }
            return donor;
        }
        public static CharacterData Capture(OutfitSystem system)
        {
            if(!system)throw new ArgumentNullException(nameof(system));var result=BMAC_SaveSystem.GetCharacterData(system);
            // New clone naming below makes the unmodified native capture path
            // correct. This check fails visibly if a future caller skips it.
            foreach(var outfit in system.GetOutfits())if(outfit)
            {
                var identity=outfit.GetComponent<FriendGeneratedOutfitIdentity>();if(!identity)continue;
                var captured=result.outfitDatas.FirstOrDefault(o=>o.outfit==identity.resourcePath);
                if(string.IsNullOrEmpty(captured.outfit))throw new InvalidOperationException("Generated clone identity was not configured before native capture");
            }
            return PlayerCustomizationSession.CloneCharacterDataForRuntime(result);
        }
        public static async Task Load(OutfitSystem system,CharacterData appearance,CancellationToken cancellation=default(CancellationToken))
        {
            if(!system||appearance==null)throw new ArgumentNullException("Friend appearance target/data");
            if(!HasGenerated(appearance))
            {
                var oldPhoto=system.GetComponent<FriendGeneratedTintBridge>();if(oldPhoto&&system.GetComponent<FriendAppearanceOwnedRig>()){oldPhoto.enabled=false;UnityEngine.Object.Destroy(oldPhoto);var oldRenderer=system.GetCharacterBody();if(oldRenderer){oldRenderer.SetPropertyBlock(null);for(int i=0;i<oldRenderer.sharedMaterials.Length;i++)oldRenderer.SetPropertyBlock(null,i);}}
                await BMAC_SaveSystem.LoadCharacter(system,appearance,false,system.async,cancellation);return;
            }
            if(!system.GetComponent<FriendAppearanceOwnedRig>())throw new InvalidOperationException("Photo appearance may only be applied to an explicitly owned new rig clone");
            if(!system.initalized||system.isloading||system.isStatic)throw new InvalidOperationException("Owned appearance rig is not ready");
            var original=PlayerCustomizationSession.CloneCharacterDataForRuntime(appearance);
            var donor=ToDonorData(appearance);
            var generated=new List<Tuple<OutfitData,Outfit>>();
            for(int i=0;i<original.outfitDatas.Count;i++)
            {
                var item=original.outfitDatas[i];if(string.IsNullOrEmpty(item.outfit)||!item.outfit.Contains("/FriendsGenerated/"))continue;
                if(!FriendGeneratedModels.TryLoadOutfit(item.outfit,out var template)||!template)throw new InvalidOperationException("Generated package is not available yet: "+item.outfit);
                generated.Add(Tuple.Create(item,template));
            }
            await BMAC_SaveSystem.LoadCharacter(system,donor,false,system.async,cancellation);cancellation.ThrowIfCancellationRequested();
            var created=new List<Outfit>();GameObject staging=null;
            try
            {
                staging=new GameObject("Friend generated outfit staging");if(system.gameObject.scene.IsValid())SceneManager.MoveGameObjectToScene(staging,system.gameObject.scene);staging.SetActive(false);
                foreach(var entry in generated.OrderByDescending(e=>e.Item2.Type&&e.Item2.Type.name=="Head"))
                {
                    cancellation.ThrowIfCancellationRequested();var data=entry.Item1;var clone=UnityEngine.Object.Instantiate(entry.Item2,staging.transform,false);created.Add(clone);
                    string prefix=clone.Type.name+"/";if(!data.outfit.StartsWith(prefix,StringComparison.Ordinal))throw new InvalidOperationException("Generated outfit type/path mismatch");
                    // Outfit.GetOutfitData() already builds Type+'/'+name and
                    // removes '(Clone)'. No original capture hook is needed.
                    clone.name=data.outfit.Substring(prefix.Length);var identity=clone.GetComponent<FriendGeneratedOutfitIdentity>();if(!identity)identity=clone.gameObject.AddComponent<FriendGeneratedOutfitIdentity>();identity.resourcePath=data.outfit;
                    clone.transform.SetParent(system.transform,false);clone.gameObject.SetActive(true);clone.Attach(system);
                    if(data.colors!=null)for(int channel=0;channel<Math.Min(9,data.colors.Count);channel++)clone.SetColor(data.colors[channel],channel+1);
                    if(clone.customShader)clone.SetColor(data.color);
                    await Task.Yield();
                }
                await Ready(system,cancellation);await system.MergeCharacter(cancellation);cancellation.ThrowIfCancellationRequested();
                system.ResetShapes();ApplyShapes(system,original.bodyIDs,original.bodyShapes);ApplyShapes(system,original.faceIDs,original.faceShapes);
                BMAC_SaveSystem.LoadBodyMods(system,original);system.SetStance(original.stance);system.RebuildClothAfterBodyChanges();
                if(!system.GetComponent<FriendGeneratedTintBridge>())system.gameObject.AddComponent<FriendGeneratedTintBridge>();system.GetComponent<FriendGeneratedTintBridge>().Apply();system.OnCharacterLoaded?.Invoke();
            }
            catch{foreach(var outfit in created)if(outfit)UnityEngine.Object.Destroy(outfit.gameObject);throw;}
            finally{if(staging)UnityEngine.Object.Destroy(staging);}
        }
        static void ApplyShapes(OutfitSystem system,List<string> ids,List<float> values)
        {if(ids==null||values==null)return;for(int i=0;i<Math.Min(ids.Count,values.Count);i++)system.SetShape(ids[i],values[i]);}
        static async Task Ready(OutfitSystem system,CancellationToken token)
        {float end=Time.realtimeSinceStartup+30;while(system&&(system.isloading||system.isMerging||system.isUpdatingTexture)){token.ThrowIfCancellationRequested();if(Time.realtimeSinceStartup>end)throw new TimeoutException("Owned rig load/merge did not settle");await Task.Yield();}if(!system)throw new OperationCanceledException("Owned rig was destroyed");}
    }
}
