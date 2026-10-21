using UnityEngine;
namespace SushiAndFish.FrontEnd {
 public sealed class CasinoMenuLighting:MonoBehaviour {
  public Transform nativeMapPose;public Texture3D nativeLightMap;public Vector3 nativeGridOffset;public Vector3Int rotatedGridResolution;public float nativeSpacing,brightness,ambienceStrength,ambienceMin;
  void LateUpdate(){if(!nativeMapPose||!nativeLightMap)return;Shader.SetGlobalTexture("_LightMap",nativeLightMap);Shader.SetGlobalVector("gridOffset",nativeMapPose.TransformPoint(nativeGridOffset));Shader.SetGlobalVector("gridRes",(Vector3)rotatedGridResolution);Shader.SetGlobalFloat("raySpacing",nativeSpacing*Mathf.Abs(nativeMapPose.lossyScale.x));Shader.SetGlobalFloat("brightness",brightness);Shader.SetGlobalFloat("ambienceStrength",ambienceStrength);Shader.SetGlobalFloat("ambienceMin",ambienceMin);}
 }
}
