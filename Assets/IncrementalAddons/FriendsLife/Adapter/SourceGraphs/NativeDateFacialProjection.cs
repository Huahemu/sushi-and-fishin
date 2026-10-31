using System;
using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using PaperPixelGames.Core.Ai.NPCs;
using UnityEngine;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // Reversible projection through existing host cheek assets and eye-closed
    // morphs. This does not substitute Smile for the source Blush presets.
    public sealed class NativeDateFacialProjection:IDisposable
    {
        sealed class Eye{public SkinnedMeshRenderer renderer;public Mesh mesh;public int index;public float restore,open;}
        readonly ResidentController actor;
        readonly List<Eye> eyes=new List<Eye>();
        OutfitSystem system;Outfit temporary,backup,existingBlush;bool ownsBlush,disposed;
        public string LastError{get;private set;}
        public bool BlushOutfitAttached{get;private set;}
        public bool BlinkShapesBound=>eyes.Count>=4;
        public NativeDateFacialProjection(ResidentController actor){this.actor=actor;}
        static bool EyeName(string name)=>name.EndsWith("Expression_Eyes_UpperClosed_L",StringComparison.Ordinal)||name.EndsWith("Expression_Eyes_UpperClosed_R",StringComparison.Ordinal)||name.EndsWith("Expression_Eyes_LowerClosed_L",StringComparison.Ordinal)||name.EndsWith("Expression_Eyes_LowerClosed_R",StringComparison.Ordinal);
        float CanonicalWeight(string full)
        {
            var data=actor?.ResidentData?.CharacterData?.GetCharacterData();if(data?.faceIDs==null||data.faceShapes==null)return 0;
            for(int i=0;i<Math.Min(data.faceIDs.Count,data.faceShapes.Count);i++)if(full==data.faceIDs[i]||full.EndsWith("."+data.faceIDs[i],StringComparison.Ordinal))return data.faceShapes[i];return 0;
        }
        void BindEyes()
        {
            eyes.RemoveAll(e=>!e.renderer||e.renderer.sharedMesh!=e.mesh);
            foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh=renderer.sharedMesh;if(!mesh||!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                for(int index=0;index<mesh.blendShapeCount;index++)
                {
                    string name=mesh.GetBlendShapeName(index);if(!EyeName(name)||eyes.Any(e=>e.renderer==renderer&&e.index==index))continue;
                    eyes.Add(new Eye{renderer=renderer,mesh=mesh,index=index,restore=renderer.GetBlendShapeWeight(index),open=CanonicalWeight(name)});
                }
            }
        }
        bool BindBlush()
        {
            if(BlushOutfitAttached&&system)
            {
                if(ownsBlush&&temporary&&system.GetOutfit(temporary.Type)==temporary)return true;
                if(!ownsBlush&&existingBlush&&system.GetOutfit(existingBlush.Type)==existingBlush)return true;
                if(backup)UnityEngine.Object.Destroy(backup.gameObject);backup=null;
                if(temporary)UnityEngine.Object.Destroy(temporary.gameObject);temporary=null;existingBlush=null;ownsBlush=false;BlushOutfitAttached=false;
            }
            if(actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r=>r.sharedMaterials.Any(m=>m&&m.shader&&m.shader.name=="SushiAndFish/FriendPhotoSurface")))
            {LastError="The generated photo UV surface requires its explicit cheek-mask projection; stock atlas makeup is not applied over that identity";return false;}
            system=actor.GetComponentsInChildren<OutfitSystem>(true).FirstOrDefault(s=>s.initalized&&!s.isStatic);
            if(!system){LastError="The actual actor has no editable existing host outfit owner";return false;}
            if(system.isloading||system.isMerging||system.isUpdatingTexture||actor.IsCustomizationApplying){LastError="The actual host appearance is still being applied";return false;}
            string resource=system.ContainsTag("Anime")?"MakeUpCheeks/MakeUpCheeks_SimpleBlush":system.ContainsTag("Stylized")?"MakeUpCheeks/MakeUpCheeks_BasicBlush":null;
            var prefab=resource==null?null:Resources.Load<GameObject>(resource);var template=prefab?prefab.GetComponent<Outfit>():null;
            if(!template||template.Type==null){LastError="No matching original host Blush cheek resource for the current head tags";return false;}
            var old=system.GetOutfit(template.Type);
            if(old&&old.OutfitName==template.OutfitName&&old.material==template.material){existingBlush=old;BlushOutfitAttached=true;return true;}
            if(old)
            {
                if(old.additionalBones!=null&&old.additionalBones.Length>0){LastError="The current cheek slot has an unsupported bone owner";return false;}
                var clone=UnityEngine.Object.Instantiate(old.gameObject);clone.name="Source date previous cheek slot";clone.SetActive(false);backup=clone.GetComponent<Outfit>();
                // Awake initializes colors from its material; restore the exact
                // previous instance colors after that initialization.
                backup.colors=old.colors?.ToArray();backup.defaultColors=old.defaultColors?.ToArray();backup.patternColors=old.patternColors?.ToArray();backup.pattern=old.pattern;backup.patternSize=old.patternSize;
                backup.decalDatas=old.decalDatas==null?null:new List<DecalData>(old.decalDatas);backup.attached=false;backup.Initalized=false;
            }
            temporary=UnityEngine.Object.Instantiate(prefab,system.transform).GetComponent<Outfit>();temporary.name="Source date Blush hold";temporary.attached=false;temporary.Attach(system);ownsBlush=system.GetOutfit(template.Type)==temporary;BlushOutfitAttached=ownsBlush;
            if(!ownsBlush){UnityEngine.Object.Destroy(temporary.gameObject);temporary=null;if(backup)UnityEngine.Object.Destroy(backup.gameObject);backup=null;LastError="Host outfit owner did not accept the original Blush slot";return false;}
            return true;
        }
        public bool Apply(int sourceCode)
        {
            LastError=null;if(disposed||!actor)return false;string name=NativeAsExpressionNames.RowName(sourceCode);
            if(name!="Blush"&&name!="BlushBlink"){LastError="The current Date facial projection does not own source preset "+name;return false;}
            BindEyes();bool blush=BindBlush();if(!BlinkShapesBound){LastError="The actual host eye-closed morph owner is incomplete";return false;}
            bool blink=name=="BlushBlink";
            foreach(var eye in eyes)if(eye.renderer&&eye.renderer.sharedMesh==eye.mesh)eye.renderer.SetBlendShapeWeight(eye.index,blink?100:eye.open);
            return blush;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            foreach(var eye in eyes)if(eye.renderer&&eye.renderer.sharedMesh==eye.mesh)eye.renderer.SetBlendShapeWeight(eye.index,eye.restore);eyes.Clear();
            // Restore only while this very temporary slot still owns the host
            // slot. A later user appearance edit must never be overwritten.
            if(ownsBlush&&system&&temporary&&system.GetOutfit(temporary.Type)==temporary)
            {
                var type=temporary.Type;system.RemoveOutfit(type,backup==null);
                if(backup){backup.gameObject.SetActive(true);backup.attached=false;backup.Attach(system);backup=null;}
            }
            else if(backup){UnityEngine.Object.Destroy(backup.gameObject);backup=null;}
            temporary=null;BlushOutfitAttached=false;
        }
    }
}
