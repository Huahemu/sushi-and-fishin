using System;
using System.IO;
using System.Linq;
using ECM2;
using ECM2.Examples.FirstPerson;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class RecoveryGroundJumpGameplayAudit
{
    const string Key="SushiRecovery.GroundJumpGameplay.";
    static string Reports=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../reports"));
    static double gameSeen,phaseStarted;
    static int phase;
    static FirstPersonCharacter character;
    static float startY,peakY;
    static bool leftGround,released;
    static Report report;
    [Serializable] public class Report
    {
        public string utc,error,terrainShader,saveProfile;public Vector3 beachTarget,jumpPosition;
        public bool terrainShaderSupported,localPlayerFound,playerGroundedBefore,jumpLanded,beachFrameSaved;
        public float jumpApexMetres,jumpImpulse,gravityMagnitude,landingOffset,minimumObservedFps=10000;
    }
    [Serializable] class Startup {public bool reachedMenu,reachedGame,loadingVisible;public float timeScale;public string[] errors;}
    static RecoveryGroundJumpGameplayAudit(){if(SessionState.GetBool(Key+"active",false))EditorApplication.update+=Update;}
    public static void Start()
    {
        if(Application.isBatchMode)throw new InvalidOperationException("Original game bootstrap verification requires the interactive Editor frame loop; use the normal GUI launcher.");
        if(PlayerSettings.productName=="Deep Blue Sushi Recovery QA")PlayerSettings.productName="Deep Blue Sushi";
        SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"finished",false);
        EditorApplication.update+=Update;RecoveryPlayAudit.StartAndLaunchGame();
    }
    static void Update()
    {
        if(!SessionState.GetBool(Key+"active",false))return;
        if(report==null)report=new Report{utc=DateTime.UtcNow.ToString("o"),saveProfile=Application.persistentDataPath};
        try
        {
            if(EditorApplication.isPlaying && SceneManager.GetSceneByName("GameState").isLoaded && !SessionState.GetBool(Key+"finished",false))
            {
                if(gameSeen==0)gameSeen=EditorApplication.timeSinceStartup;
                if(EditorApplication.timeSinceStartup-gameSeen<30 || Time.timeScale<0.9f)return;
                if(phase==0)
                {
                    var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/TerrainLit.mat");report.terrainShader=material.shader.name;report.terrainShaderSupported=material.shader.isSupported;
                    var beach=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t=>t.name.StartsWith("BeachChair",StringComparison.Ordinal));
                    report.beachTarget=beach.position;
                    var camera=new GameObject("Isolated repaired beach verification camera").AddComponent<Camera>();
                    camera.nearClipPlane=0.1f;camera.farClipPlane=2000;camera.fieldOfView=55;camera.transform.position=beach.position+new Vector3(0,7,-12);camera.transform.LookAt(beach.position+Vector3.up);
                    camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                    RecoveryGroundDiagnostic.Capture(camera,"ground-repaired-in-game.png");UnityEngine.Object.Destroy(camera.gameObject);
                    report.beachFrameSaved=File.Exists(Path.Combine(Reports,"ground-repaired-in-game.png"));
                    character=UnityEngine.Object.FindFirstObjectByType<FirstPersonCharacter>();report.localPlayerFound=character!=null;
                    if(!character)throw new InvalidOperationException("Original local player not found");
                    Vector3 spot=beach.position+new Vector3(4,0,3);
                    var terrain=Terrain.activeTerrains.First(t=>spot.x>=t.transform.position.x && spot.z>=t.transform.position.z && spot.x<t.transform.position.x+t.terrainData.size.x && spot.z<t.transform.position.z+t.terrainData.size.z);
                    spot.y=terrain.SampleHeight(spot)+terrain.transform.position.y+0.05f;report.jumpPosition=spot;
                    character.StopJumping();character.SetVelocity(Vector3.zero);character.characterMovement.constrainToGround=true;
                    character.SetPosition(spot,true);character.SetMovementMode(Character.MovementMode.Walking);
                    phase=1;phaseStarted=EditorApplication.timeSinceStartup;
                }
                else if(phase==1 && EditorApplication.timeSinceStartup-phaseStarted>4)
                {
                    report.playerGroundedBefore=character.IsGrounded();
                    if(!report.playerGroundedBefore)throw new InvalidOperationException("Original player did not settle on beach ground; position="+character.GetPosition()+" mode="+character.movementMode);
                    startY=peakY=character.GetPosition().y;report.jumpImpulse=character.jumpImpulse;report.gravityMagnitude=character.GetGravityMagnitude();
                    character.Jump();phase=2;phaseStarted=EditorApplication.timeSinceStartup;
                }
                else if(phase==2)
                {
                    report.minimumObservedFps=Mathf.Min(report.minimumObservedFps,1f/Mathf.Max(Time.deltaTime,0.0001f));
                    peakY=Mathf.Max(peakY,character.GetPosition().y);if(!character.IsGrounded())leftGround=true;
                    double elapsed=EditorApplication.timeSinceStartup-phaseStarted;
                    if(!released && elapsed>0.3f){character.StopJumping();released=true;}
                    if(leftGround && character.IsGrounded() && elapsed>0.4f)
                    {
                        report.jumpLanded=true;report.jumpApexMetres=peakY-startY;report.landingOffset=character.GetPosition().y-startY;
                        File.WriteAllText(Path.Combine(Reports,"ground-jump-gameplay-check.json"),JsonUtility.ToJson(report,true));
                        SessionState.SetBool(Key+"finished",true);
                    }
                    else if(elapsed>20)throw new InvalidOperationException("Original player jump did not complete");
                }
            }
            if(!EditorApplication.isPlayingOrWillChangePlaymode && !SessionState.GetBool("DeepBlueSushi.RecoveryPlayAudit.active",false))
            {
                var startup=JsonUtility.FromJson<Startup>(File.ReadAllText(Path.Combine(Reports,"unity-original-play-audit.json")));
                bool passed=string.IsNullOrEmpty(report.error) && report.beachFrameSaved && report.terrainShaderSupported && report.terrainShader=="SushiRecovery/Universal Render Pipeline/Terrain/Lit" && report.playerGroundedBefore && report.jumpLanded && report.jumpApexMetres>1.0f && report.jumpApexMetres<1.6f && startup.reachedMenu && startup.reachedGame && !startup.loadingVisible && startup.timeScale==1 && startup.errors.Length==0;
                File.WriteAllText(Path.Combine(Reports,"ground-jump-gameplay-check.json"),JsonUtility.ToJson(report,true));
                SessionState.SetBool(Key+"active",false);EditorApplication.Exit(passed?0:2);
            }
        }
        catch(Exception exception)
        {
            report.error=exception.ToString();SessionState.SetBool(Key+"finished",true);
            File.WriteAllText(Path.Combine(Reports,"ground-jump-gameplay-check.json"),JsonUtility.ToJson(report,true));
        }
    }
}
