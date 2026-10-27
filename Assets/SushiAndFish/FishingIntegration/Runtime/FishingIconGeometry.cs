using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SushiAndFish.FishingIntegration {
 // Outlines select original icon pixels without changing the PNG or UI material.
 public static class FishingIconGeometry {
  [Serializable] sealed class GeometryFile {public Entry[] entries;}
  [Serializable] sealed class Entry {public string kind;public int index;public Quad[] quads;}
  [Serializable] internal sealed class Quad {public int x,y,w,h;}
  static MigrationConfig applied;
  static readonly Dictionary<Sprite,Quad[]> outlines=new Dictionary<Sprite,Quad[]>();
  public static bool IsOutlined(Sprite source)=>source&&outlines.ContainsKey(source);
  internal static Quad[] Get(Sprite source)=>source&&outlines.TryGetValue(source,out var data)?data:null;
  public static void Attach(Image image){if(image&&!image.GetComponent<FishingIconOutline>())image.gameObject.AddComponent<FishingIconOutline>();}
  public static int Apply(MigrationConfig config,bool force=false){
   if(!config||!config.iconGeometry)return 0;
   if(!force&&applied==config)return outlines.Count;
   outlines.Clear();
   var data=JsonUtility.FromJson<GeometryFile>(config.iconGeometry.text);int count=0;
   foreach(var entry in data.entries){
    var source=entry.kind=="fish"?config.species[entry.index].original.Sprite:config.equipmentIcons[entry.index];
    if(source&&entry.quads!=null&&entry.quads.Length*4<65535){outlines.Add(source,entry.quads);count++;}
   }
   applied=config;return count;
  }
 }
}
