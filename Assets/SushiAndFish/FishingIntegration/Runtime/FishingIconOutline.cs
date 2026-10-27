using UnityEngine;
using UnityEngine.UI;

namespace SushiAndFish.FishingIntegration {
 [RequireComponent(typeof(Image))]
 public sealed class FishingIconOutline:BaseMeshEffect {
  public override void ModifyMesh(VertexHelper vertices){
   if(!IsActive()||!(graphic is Image image)||!image.sprite||image.type!=Image.Type.Simple)return;
   var quads=FishingIconGeometry.Get(image.sprite);if(quads==null)return;
   var source=image.sprite.rect;var texture=image.sprite.texture;var draw=image.GetPixelAdjustedRect();
   if(image.preserveAspect){float aspect=source.width/source.height;var before=draw.size;if(before.x/before.y>aspect){draw.width=before.y*aspect;draw.x+=(before.x-draw.width)*image.rectTransform.pivot.x;}else{draw.height=before.x/aspect;draw.y+=(before.y-draw.height)*image.rectTransform.pivot.y;}}
   vertices.Clear();Color32 tint=image.color;
   foreach(var q in quads){
    int start=vertices.currentVertCount;float x=draw.x+(q.x-source.x)/source.width*draw.width,y=draw.y+(q.y-source.y)/source.height*draw.height,w=q.w/source.width*draw.width,h=q.h/source.height*draw.height;
    float u=q.x/(float)texture.width,v=q.y/(float)texture.height,du=q.w/(float)texture.width,dv=q.h/(float)texture.height;
    vertices.AddVert(new Vector3(x,y),tint,new Vector2(u,v));vertices.AddVert(new Vector3(x+w,y),tint,new Vector2(u+du,v));vertices.AddVert(new Vector3(x,y+h),tint,new Vector2(u,v+dv));vertices.AddVert(new Vector3(x+w,y+h),tint,new Vector2(u+du,v+dv));
    vertices.AddTriangle(start,start+2,start+1);vertices.AddTriangle(start+1,start+2,start+3);
   }
  }
 }
}
