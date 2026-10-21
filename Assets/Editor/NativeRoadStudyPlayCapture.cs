using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class NativeRoadStudyPlayCapture
{
    const string Key="SushiAndFish.NativeRoadStudy.";
    static NativeRoadStudyPlayCapture(){if(SessionState.GetBool(Key+"active",false))EditorApplication.update+=Tick;}
    public static void Begin(Camera camera){camera.transform.SetPositionAndRotation(new Vector3(25,180,-28),Quaternion.Euler(90,0,0));camera.orthographic=true;camera.orthographicSize=74;SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"started",false);SessionState.SetBool(Key+"done",false);EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();}
    static void Tick(){if(EditorApplication.isPlaying&&!SessionState.GetBool(Key+"started",false)){SessionState.SetBool(Key+"started",true);Capture().Forget();}if(!EditorApplication.isPlayingOrWillChangePlaymode&&SessionState.GetBool(Key+"done",false)){SessionState.SetBool(Key+"active",false);EditorApplication.update-=Tick;EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");}}
    static async UniTask Photo(Camera c,Vector3 position,Vector3 target,float size,string file){c.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));c.orthographic=size>0;c.orthographicSize=size;await UniTask.Delay(TimeSpan.FromSeconds(2),ignoreTimeScale:true);ScreenCapture.CaptureScreenshot(Path.Combine(IslandRevision20261018.Reports,file));await UniTask.Delay(TimeSpan.FromSeconds(1),ignoreTimeScale:true);}
    static async UniTaskVoid Capture(){try{await UniTask.Delay(TimeSpan.FromSeconds(3),ignoreTimeScale:true);var c=GameObject.Find("NativeRoadReferenceCamera").GetComponent<Camera>();await Photo(c,new Vector3(25,180,-28),new Vector3(25,0,-28),74,"source-native-road-network.png");await Photo(c,new Vector3(4,22,15),new Vector3(-5,3,-6),0,"source-native-greenmarket-join.png");await Photo(c,new Vector3(18,22,-3),new Vector3(-11,3,-19),0,"source-native-fishstore-corner.png");File.WriteAllText(Path.Combine(IslandRevision20261018.Reports,"town-native-road-visual-study.json"),"{\"renderedInEditorPlay\":true,\"views\":3,\"sourceInstances\":277}");}finally{SessionState.SetBool(Key+"done",true);EditorApplication.ExitPlaymode();}}
}
