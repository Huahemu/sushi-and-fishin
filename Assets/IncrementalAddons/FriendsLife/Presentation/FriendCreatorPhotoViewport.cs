using System;
using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SushiAndFish.FriendsLife
{
    // Owned friend-editor presentation only. The actual editable rig stays in
    // use; an isolated live camera uses the same RGB path as the tested photo
    // preview instead of the loaded island's unrestricted main-camera effects.
    public sealed class FriendCreatorPhotoViewport:MonoBehaviour
    {
        const int PhotoLayer=31;
        CharacterCreator creator;Camera source,feed;RenderTexture target;RawImage image;Material material;GameObject cameraObject;
        readonly Dictionary<GameObject,int> layers=new Dictionary<GameObject,int>();
        readonly Dictionary<Camera,int> cameraMasks=new Dictionary<Camera,int>();
        public static FriendCreatorPhotoViewport Ensure(CharacterCreator owner)
        {
            var existing=owner.GetComponent<FriendCreatorPhotoViewport>();if(existing)return existing;
            var value=owner.gameObject.AddComponent<FriendCreatorPhotoViewport>();try{value.Configure(owner);return value;}catch{UnityEngine.Object.Destroy(value);throw;}
        }
        void Configure(CharacterCreator owner)
        {
            creator=owner;var roots=owner.gameObject.scene.GetRootGameObjects();
            source=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c=>c.enabled&&!c.targetTexture&&c.CompareTag("MainCamera"));
            var canvas=roots.SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).FirstOrDefault(c=>c.isRootCanvas&&c.gameObject.activeInHierarchy);
            if(!source||!canvas)throw new InvalidOperationException("Owned friend editor camera/canvas is unavailable");
            int width=Mathf.Clamp(Screen.width,512,2048),height=Mathf.Clamp(Mathf.RoundToInt(width/Mathf.Max(.1f,source.aspect)),384,2048);
            target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="Owned friend editor photo viewport",hideFlags=HideFlags.DontSave,antiAliasing=1};target.Create();
            cameraObject=new GameObject("Owned friend RGB camera",typeof(Camera)){hideFlags=HideFlags.DontSave};SceneManager.MoveGameObjectToScene(cameraObject,owner.gameObject.scene);cameraObject.transform.SetParent(owner.transform,false);feed=cameraObject.GetComponent<Camera>();feed.CopyFrom(source);feed.targetTexture=target;feed.cullingMask=1<<PhotoLayer;feed.clearFlags=CameraClearFlags.SolidColor;var clear=source.backgroundColor;clear.a=0;feed.backgroundColor=clear;feed.depth=source.depth-1;feed.enabled=true;var additional=feed.GetUniversalAdditionalCameraData();additional.renderPostProcessing=false;additional.SetRenderer(-1);
            var rawObject=new GameObject("Owned friend live photo",typeof(RectTransform),typeof(RawImage)){hideFlags=HideFlags.DontSave};SceneManager.MoveGameObjectToScene(rawObject,owner.gameObject.scene);rawObject.layer=canvas.gameObject.layer;var rect=(RectTransform)rawObject.transform;rect.SetParent(canvas.transform,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.SetAsFirstSibling();image=rawObject.GetComponent<RawImage>();image.texture=target;image.color=Color.white;image.raycastTarget=false;
            var shader=Resources.Load<Shader>("FriendsLife/FriendTabletGraphic");if(!shader)throw new InvalidOperationException("Owned friend RGB UI shader is unavailable");material=new Material(shader){name="Owned friend editor RGB UI",hideFlags=HideFlags.DontSave};image.material=material;
            foreach(var camera in roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)))if(camera&&camera!=feed){cameraMasks[camera]=camera.cullingMask;camera.cullingMask=camera.targetTexture?camera.cullingMask|(1<<PhotoLayer):camera.cullingMask&~(1<<PhotoLayer);}
            Refresh();
        }
        void LateUpdate()=>Refresh();
        void Refresh()
        {
            if(!creator||!creator.character||!feed||!source)return;
            foreach(var renderer in creator.character.GetComponentsInChildren<Renderer>(true))if(renderer){var root=renderer.gameObject;if(!layers.ContainsKey(root))layers[root]=root.layer;root.layer=PhotoLayer;}
            feed.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);feed.fieldOfView=source.fieldOfView;feed.orthographic=source.orthographic;feed.orthographicSize=source.orthographicSize;feed.nearClipPlane=source.nearClipPlane;feed.farClipPlane=source.farClipPlane;feed.aspect=source.aspect;
        }
        public void End()
        {
            foreach(var item in layers)if(item.Key)item.Key.layer=item.Value;layers.Clear();foreach(var item in cameraMasks)if(item.Key)item.Key.cullingMask=item.Value;cameraMasks.Clear();if(feed)feed.enabled=false;if(cameraObject)Destroy(cameraObject);cameraObject=null;if(image)Destroy(image.gameObject);image=null;if(material)Destroy(material);material=null;if(target){target.Release();Destroy(target);}target=null;
        }
        void OnDestroy()=>End();
    }
}
