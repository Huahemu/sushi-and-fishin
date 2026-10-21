using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ParkCasinoSavedPreview
{
    public static void Run()
    {
        Camera camera=null;RenderTexture target=null;Texture2D image=null;
        try
        {
            EditorSceneManager.OpenScene("Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity");
            OriginalNativeShaderRestoration.Apply();
            camera=new GameObject("Casino saved-scene preview").AddComponent<Camera>();
            camera.transform.position=new Vector3(-101,95,-220);
            camera.transform.LookAt(new Vector3(-101,65,-10));
            camera.fieldOfView=68;camera.farClipPlane=1200;camera.cullingMask=~((1<<5)|(1<<6)|(1<<7)|(1<<10));
            target=RenderTexture.GetTemporary(1280,900,24,RenderTextureFormat.ARGB32);camera.targetTexture=target;
            CasinoNativeShaderEditorSupport.Begin();camera.Render();CasinoNativeShaderEditorSupport.End();
            var old=RenderTexture.active;RenderTexture.active=target;image=new Texture2D(1280,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1280,900),0,0);image.Apply();RenderTexture.active=old;
            File.WriteAllBytes(Path.Combine(IslandRevision20261018.Reports,"casino-saved-park-overview.png"),image.EncodeToPNG());
            Debug.Log("CASINO_SAVED_PREVIEW_PASS");
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);return;}
        finally
        {
            if(camera)UnityEngine.Object.DestroyImmediate(camera.gameObject);
            if(image)UnityEngine.Object.DestroyImmediate(image);
            if(target)RenderTexture.ReleaseTemporary(target);
        }
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");EditorApplication.Exit(0);
    }
}
