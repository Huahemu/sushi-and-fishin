using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ECM2;
using ECM2.Examples.FirstPerson;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RecoveryJumpDiagnostic
{
    const string Key="SushiRecovery.JumpDiagnostic";
    static string Reports=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../reports"));
    [Serializable] public class Trial
    {
        public string simulation;public int fps;public float apexHeight,expectedMaximum,landingHeight,flightSeconds;
        public bool groundedBefore,landed;public Vector3 scale;
    }
    [Serializable] public class Report
    {
        public string utc,method,error;public bool productionSubstepMethodPresent;public float impulse,gravity,holdTime;public Trial[] trials;
    }
    static RecoveryJumpDiagnostic(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Update;}
    public static void Start()
    {
        SessionState.SetBool(Key,true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.update+=Update;EditorApplication.EnterPlaymode();
    }
    static void Update()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;
        SessionState.SetBool(Key,false);EditorApplication.update-=Update;
        var report=new Report{utc=DateTime.UtcNow.ToString("o"),method="Actual recovered FirstPersonCharacter and ECM2 movement component on a flat BoxCollider; scripted Jump/StopJumping with deterministic render-frame intervals. No original saves or input devices are used."};
        try
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/PlayerNGO.prefab");
            var original=prefab.GetComponent<FirstPersonCharacter>();
            report.impulse=original.jumpImpulse;report.gravity=original.GetGravityMagnitude();report.holdTime=original.jumpMaxHoldTime;
            var production=typeof(FirstPersonCharacter).GetMethod("SimulateFrame",BindingFlags.Public|BindingFlags.Instance);
            report.productionSubstepMethodPresent=production!=null;
            var floor=new GameObject("Jump diagnostic floor");var collider=floor.AddComponent<BoxCollider>();collider.size=new Vector3(100,1,100);floor.transform.position=new Vector3(0,-0.5f,0);Physics.SyncTransforms();
            var results=new List<Trial>();
            foreach(int fps in new[]{120,60,30,15,5,3})
            foreach(bool stable in new[]{false,true})
            {
                var go=new GameObject("Original first person jump fixture");go.SetActive(false);go.transform.position=new Vector3(0,2,0);
                var character=go.AddComponent<FirstPersonCharacter>();
                EditorUtility.CopySerialized(prefab.GetComponent<CharacterMovement>(),go.GetComponent<CharacterMovement>());
                EditorUtility.CopySerialized(original,character);
                character.cameraParent=null;
                var fields=new SerializedObject(character);fields.FindProperty("_camera").objectReferenceValue=null;fields.FindProperty("_animator").objectReferenceValue=null;fields.ApplyModifiedPropertiesWithoutUndo();
                go.SetActive(true);
                character.enableAutoSimulation=false;character.jumpImpulse=original.jumpImpulse;character.jumpMaxHoldTime=original.jumpMaxHoldTime;
                character.gravityScale=original.gravityScale;character.SetGravityVector(new Vector3(0,-9.81f,0));character.jumpMaxCount=original.jumpMaxCount;
                character.jumpMaxPreGroundedTime=original.jumpMaxPreGroundedTime;character.jumpMaxPostGroundedTime=original.jumpMaxPostGroundedTime;
                character.useRootMotion=false;character.SetMovementMode(Character.MovementMode.Falling);
                for(int i=0;i<360;i++){character.Simulate(1f/120);Physics.SyncTransforms();}
                if(!character.IsGrounded())throw new InvalidOperationException("Jump fixture did not settle on its floor: "+character.GetPosition()+" velocity="+character.GetVelocity()+" mask="+(int)character.characterMovement.collisionLayers);
                float initial=character.GetPosition().y,peak=initial,dt=1f/fps;
                var trial=new Trial{simulation=stable?(production!=null?"production_substeps":"proposed_120Hz_substeps"):"original_whole_frame",fps=fps,groundedBefore=character.IsGrounded(),scale=go.transform.lossyScale,expectedMaximum=character.GetMaxJumpHeightWithJumpTime()};
                character.Jump();bool leftGround=false;float clock=0;
                for(int i=0;i<fps*3;i++)
                {
                    if(clock>=0.3f)character.StopJumping();
                    if(stable && production!=null)production.Invoke(character,new object[]{dt});
                    else if(stable){int count=Mathf.CeilToInt(dt/(1f/120));for(int step=0;step<count;step++)character.Simulate(dt/count);}
                    else character.Simulate(dt);
                    Physics.SyncTransforms();clock+=dt;peak=Mathf.Max(peak,character.GetPosition().y);
                    if(!character.IsGrounded())leftGround=true;
                    if(leftGround && character.IsGrounded() && clock>0.15f){trial.landed=true;trial.flightSeconds=clock;break;}
                }
                trial.apexHeight=peak-initial;trial.landingHeight=character.GetPosition().y-initial;results.Add(trial);
                UnityEngine.Object.DestroyImmediate(go);
            }
            UnityEngine.Object.DestroyImmediate(floor);report.trials=results.ToArray();
            foreach(var trial in results)
            {
                if(!trial.groundedBefore || !trial.landed || Mathf.Abs(trial.landingHeight)>0.03f)throw new InvalidOperationException("Jump trial did not return to its original floor");
                if(trial.simulation=="production_substeps" && (trial.apexHeight<1.25f || trial.apexHeight>1.45f))throw new InvalidOperationException("Repaired jump height outside original-parameter envelope at "+trial.fps+" FPS: "+trial.apexHeight);
            }
        }
        catch(Exception exception){report.error=exception.ToString();}
        var name=report.productionSubstepMethodPresent?"jump-after-repair.json":"jump-before-repair.json";
        File.WriteAllText(Path.Combine(Reports,name),JsonUtility.ToJson(report,true));
        Debug.Log("RECOVERY_JUMP_DIAGNOSTIC_COMPLETE "+JsonUtility.ToJson(report));
        EditorApplication.Exit(string.IsNullOrEmpty(report.error)?0:2);
    }
}
