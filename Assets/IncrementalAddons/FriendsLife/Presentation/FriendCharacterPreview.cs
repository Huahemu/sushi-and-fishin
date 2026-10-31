using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bozo.ModularCharacters;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.Visuals;
using UnityEngine;
using UnityEngine.UI;

namespace SushiAndFish.FriendsLife
{
    // Portraits use an independent, neutral vendor rig. A resident sleeping at
    // home, a hidden world model, or delayed actor projection cannot blank them.
    public static class FriendPortraitCache
    {
        sealed class Entry{public int revision=-1,attempts;public bool pending,disposed;public float retryAt;public string sourceJson,lastError;public Texture2D texture;}
        static readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
        static bool rendering;
        public static Texture2D Get(string personId)
        {
            var runtime=FriendsLifeRuntime.Instance;var person=runtime?.GetPerson(personId);if(person==null)return null;
            string key=runtime.State.worldId+"/"+person.personId;
            if(!entries.TryGetValue(key,out var entry))entries[key]=entry=new Entry();
            string json=person.appearanceJson;
            if(person.controllerKind==PersonControllerKind.Human)
            {
                try
                {
                CharacterData actual=null;
                if(runtime.LocalHuman()?.personId==person.personId)actual=PlayerCustomizationSession.GetCurrentCharacterData();
                else if(string.IsNullOrEmpty(json))
                {
                    var root=runtime.TransformOf(person.personId);var player=root?root.GetComponent<PPG_Player>():null;
                    var system=player&&player.CharacterVisuals?player.CharacterVisuals.TpsOutfitSystem:null;
                    if(system&&system.initalized&&!system.isloading&&!system.isMerging&&!system.isUpdatingTexture)actual=BMAC_SaveSystem.GetCharacterData(system);
                }
                if(actual!=null)json=JsonUtility.ToJson(actual);
                }
                catch(Exception error){entry.lastError=error.ToString();entry.retryAt=Time.unscaledTime+1;}
            }
            if((entry.revision!=person.appearanceRevision||entry.sourceJson!=json||!entry.texture)&&!entry.pending&&!rendering&&Time.unscaledTime>=entry.retryAt&&!string.IsNullOrEmpty(json))
                Generate(entry,person.appearanceRevision,json,person.controllerKind==PersonControllerKind.Npc&&!person.existingHostResident?person.personId:null);
            return entry.texture;
        }
        public static void Bind(RawImage image,string personId)
        {var binding=image.GetComponent<FriendPortraitImage>();if(!binding)binding=image.gameObject.AddComponent<FriendPortraitImage>();binding.personId=personId;binding.draftId=null;image.texture=Get(personId);}
        public static Texture2D GetDraft(PhotoFriendDraft draft)
        {
            if(draft?.characterData==null)return null;string key="draft/"+draft.draftId;
            if(!entries.TryGetValue(key,out var entry))entries[key]=entry=new Entry();
            string json=JsonUtility.ToJson(draft.characterData);
            if((entry.revision!=draft.appearanceRevision||entry.sourceJson!=json||!entry.texture)&&!entry.pending&&!rendering&&Time.unscaledTime>=entry.retryAt)
                Generate(entry,draft.appearanceRevision,json);
            return entry.texture;
        }
        public static void Bind(RawImage image,PhotoFriendDraft draft)
        {var binding=image.GetComponent<FriendPortraitImage>();if(!binding)binding=image.gameObject.AddComponent<FriendPortraitImage>();binding.personId=null;binding.draftId=draft.draftId;image.texture=GetDraft(draft);}
        public static void ForgetDraft(string id)
        {if(!entries.TryGetValue("draft/"+id,out var entry))return;entries.Remove("draft/"+id);entry.disposed=true;if(entry.texture)UnityEngine.Object.Destroy(entry.texture);}
        static async void Generate(Entry entry,int revision,string json,string presentationPersonId=null)
        {
            rendering=true;entry.pending=true;entry.attempts++;
            try
            {
                var data=JsonUtility.FromJson<CharacterData>(json);
                if(presentationPersonId!=null)data=FriendResidentBindings.PresentationData(data,presentationPersonId,out _);
                var next=await FriendCharacterPreview.Render(data,512,true);
                if(entry.disposed){UnityEngine.Object.Destroy(next);return;}
                if(entry.texture)UnityEngine.Object.Destroy(entry.texture);entry.texture=next;entry.revision=revision;entry.sourceJson=json;entry.retryAt=0;entry.lastError=null;
            }
            catch(Exception error){entry.lastError=error.ToString();entry.retryAt=Time.unscaledTime+5;Debug.LogWarning("[FriendsLife] Portrait retry: "+error.Message);}
            finally{entry.pending=false;rendering=false;}
        }
    }
    public sealed class FriendPortraitImage:MonoBehaviour
    {
        public string personId,draftId;RawImage image;float next;
        void Awake(){image=GetComponent<RawImage>();}
        void Update(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+.25f;var texture=string.IsNullOrEmpty(draftId)?FriendPortraitCache.Get(personId):FriendPortraitCache.GetDraft(PhotoFriendPipeline.GetOrCreate().Find(draftId));if(image&&image.texture!=texture)image.texture=texture;}
    }
    public static class FriendCharacterPreview
    {
        static readonly System.Threading.SemaphoreSlim renderQueue=new System.Threading.SemaphoreSlim(1,1);
        public static async Task<Texture2D> Render(CharacterData data,int size=512,bool headshot=true,Action<OutfitSystem> configure=null,float yaw=0,int outputHeight=0,float framingScale=1)
        {
            await renderQueue.WaitAsync();
            try{return await RenderOwned(data,size,headshot,configure,yaw,outputHeight,framingScale);}
            finally{renderQueue.Release();}
        }
        static async Task<Texture2D> RenderOwned(CharacterData data,int size,bool headshot,Action<OutfitSystem> configure,float yaw,int outputHeight,float framingScale)
        {
            if(data==null)throw new InvalidOperationException("Portrait appearance is missing");
            var prefab=Resources.Load<OutfitSystem>("BSMC_CharacterMergedBase");if(!prefab)throw new InvalidOperationException("Portrait rig is missing");
            OutfitSystem system=null;GameObject snapshot=null,cameraObject=null;var meshes=new List<Mesh>();RenderTexture target=null;Texture2D image=null;bool completed=false;var old=RenderTexture.active;
            try
            {
                system=UnityEngine.Object.Instantiate(prefab,new Vector3(0,-10000,0),Quaternion.identity);system.name="Friends neutral preview";FriendAppearancePort.MarkOwnedClone(system);MarkTemporary(system.gameObject);system.Init();MarkOwnedResources(system);
                if(system.animator)system.animator.enabled=false;
                await Wait(system);await FriendAppearancePort.Load(system,data);await Wait(system);
                if(system.animator)system.animator.enabled=false;system.RestoreBindPose();BMAC_SaveSystem.LoadBodyMods(system,data);configure?.Invoke(system);
                int frame=Time.frameCount+2;while(Application.isPlaying&&Time.frameCount<frame)await Task.Yield();system.GetComponent<FriendGeneratedTintBridge>()?.Apply();
                MarkTemporary(system.gameObject);MarkOwnedResources(system);snapshot=new GameObject("Friends portrait mesh snapshot"){hideFlags=HideFlags.DontSave};
                var source=system.GetCharacterBody();if(!source||!source.sharedMesh||source.sharedMesh.vertexCount==0)throw new InvalidOperationException("Portrait merged mesh is not ready");
                var mesh=new Mesh{hideFlags=HideFlags.DontSave};meshes.Add(mesh);source.BakeMesh(mesh);var piece=new GameObject("Portrait",typeof(MeshFilter),typeof(MeshRenderer)){hideFlags=HideFlags.DontSave};piece.layer=31;piece.transform.SetParent(snapshot.transform,false);
                piece.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);piece.transform.localScale=source.transform.lossyScale;piece.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=piece.GetComponent<MeshRenderer>();renderer.sharedMaterials=source.sharedMaterials;
                for(int index=0;index<source.sharedMaterials.Length;index++){var block=new MaterialPropertyBlock();source.GetPropertyBlock(block,index);renderer.SetPropertyBlock(block,index);}
                var bounds=renderer.bounds;Vector3 center=bounds.center;float orthographic=Mathf.Max(.4f,bounds.size.y*.55f);
                if(headshot){center.y=bounds.max.y-bounds.size.y*.16f;orthographic=Mathf.Max(.19f,bounds.size.y*.22f);}
                orthographic*=Mathf.Clamp(framingScale,.1f,10f);int height=outputHeight>0?outputHeight:size;
                cameraObject=new GameObject("Friends portrait camera",typeof(Camera)){hideFlags=HideFlags.DontSave};var camera=cameraObject.GetComponent<Camera>();camera.hideFlags=HideFlags.DontSave;camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.93f,.9f,.82f,0);
                camera.orthographic=true;camera.orthographicSize=orthographic;camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.transform.position=center+Quaternion.AngleAxis(yaw,Vector3.up)*system.transform.forward*4;camera.transform.LookAt(center);
                target=RenderTexture.GetTemporary(size,height,24,RenderTextureFormat.ARGB32);camera.targetTexture=target;camera.aspect=(float)size/height;camera.Render();RenderTexture.active=target;
                image=new Texture2D(size,height,TextureFormat.RGBA32,false){name="Friend portrait",hideFlags=HideFlags.DontSave};image.ReadPixels(new Rect(0,0,size,height),0,0);image.Apply();completed=true;return image;
            }
            finally
            {
                RenderTexture.active=old;if(cameraObject)Release(cameraObject);if(target)RenderTexture.ReleaseTemporary(target);if(!completed&&image)Release(image);
                if(snapshot)Release(snapshot);foreach(var mesh in meshes)if(mesh)Release(mesh);if(system){MarkTemporary(system.gameObject);MarkOwnedResources(system);Release(system.gameObject);}
            }
        }
        static async Task Wait(OutfitSystem system)
        {float until=Time.realtimeSinceStartup+25;while((system.isloading||system.isMerging||system.isUpdatingTexture)&&Time.realtimeSinceStartup<until)await Task.Yield();if(system.isloading||system.isMerging)throw new TimeoutException("Portrait loader timed out");}
        static void Release(UnityEngine.Object value){if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        static void MarkTemporary(GameObject root){foreach(var item in root.GetComponentsInChildren<Transform>(true)){item.gameObject.hideFlags=HideFlags.DontSave;foreach(var component in item.GetComponents<Component>())if(component)component.hideFlags=HideFlags.DontSave;}}
        static void MarkOwnedResources(OutfitSystem system)
        {
            // These fields contain objects created by this independent system;
            // generated shared-material caches and prefab assets are excluded.
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            foreach(string name in new[]{"masterMaterial","copyTexture","normalCopyTexture"})
                if(typeof(OutfitSystem).GetField(name,flags)?.GetValue(system) is UnityEngine.Object resource&&resource)resource.hideFlags=HideFlags.DontSave;
            var textures=typeof(OutfitSystem).GetField("textures",flags)?.GetValue(system) as Dictionary<string,RenderTexture>;
            if(textures!=null)foreach(var texture in textures.Values)if(texture)texture.hideFlags=HideFlags.DontSave;
            if(system.copyMaterial)system.copyMaterial.hideFlags=HideFlags.DontSave;if(system.combinedMesh)system.combinedMesh.hideFlags=HideFlags.DontSave;
            if(system.renderTextures!=null)foreach(var texture in system.renderTextures)if(texture)texture.hideFlags=HideFlags.DontSave;
        }
    }
}
