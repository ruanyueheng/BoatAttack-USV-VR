using System;
using System.Collections;
using System.Collections.Generic;
using BoatAttack.Fishing;
using UnityEngine;
namespace BoatAttack.EditorTools
{
    public static class FishingCoastArtValidation
    {
        public static void Run()=>FishingV011Validation.Begin("COAST-ART");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException("[COAST-ART] "+label);Debug.Log("[COAST-ART] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Only isolated production state is used");
            for(int id=0;id<2;id++)
            {
                var island=c.CoastPresentation.transform.Find(FishingCoastalPresentation.IsletName(id));
                Check(island!=null && island.position==FishingCoastalPresentation.Islets[id],"Stable island origin "+id);
                var land=island.Find("Walkable sculpted shoreline");var collider=land.GetComponent<MeshCollider>();
                Check(land.gameObject.layer==10 && collider!=null && collider.sharedMesh.vertexCount==1088,"Water depth and walkable shore mesh remain intact "+id);
                bool sameTerrain=true;foreach(var vertex in collider.sharedMesh.vertices)if(Mathf.Abs(vertex.y-FishingCoastalPresentation.Height(vertex.x,vertex.z,id))>=.0001f)sameTerrain=false;
                Check(sameTerrain,"Original terrain height "+id);
                var ground=land.GetComponent<Renderer>().sharedMaterial;
                Check(ground.HasProperty("_GroundDetail") && ground.GetFloat("_GroundDetail")>.99f && ground.GetTexture("_BaseMap")!=null,"Actual coastal sand/grass detail texture is active "+id);
                Capture(c,island,id);GroundColours(c,collider,island,id);
                int trees=0,shelters=0;var meshes=new HashSet<Mesh>();int triangles=0;bool rejected=false;
                foreach(var child in island.GetComponentsInChildren<Transform>())
                {
                    rejected|=child.name=="tree_oak" || child.name=="tree_palmDetailedTall" || child.name=="plant_bush" || child.name=="flower_purpleA" || child.name=="tent_detailedOpen";
                    if(child.name=="CoastalPine" || child.name=="DunePalm")trees++;
                    if(child.name=="Weathered timber shelter")shelters++;
                }
                Check(!rejected,"Rejected prototype props are absent "+id);
                foreach(var filter in island.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null && meshes.Add(filter.sharedMesh))
                    for(int submesh=0;submesh<filter.sharedMesh.subMeshCount;submesh++)triangles+=(int)filter.sharedMesh.GetIndexCount(submesh)/3;
                Check(trees>=4 && shelters==1,"Detailed atlas vegetation and one weathered shelter are present "+id);
                Check(triangles>20000 && triangles<140000,"Static geometry stays within the island art budget "+id+" triangles="+triangles);
                int batched=0,total=0;
                foreach(var renderer in island.GetComponentsInChildren<MeshRenderer>())if(renderer.GetComponent<TextMesh>()==null){total++;if(renderer.isPartOfStaticBatch)batched++;}
                Check(total>0 && batched>=total*.9f,"Static props are combined rather than submitted separately "+id+" "+batched+"/"+total);
                if(id==1)
                {
                    for(int angle=0;angle<360;angle+=20)for(int radius=0;radius<=20;radius+=4)
                    {
                        var offset=Quaternion.Euler(0,angle,0)*Vector3.forward*radius;
                        var seed=island.position+offset;seed.y=0;
                        Check(c.Shore.Ground(seed,out var floor) && c.Shore.Clearance(floor),"Actual challenge arena has supported empty standing room "+offset);
                    }
                }
                var berth=FishingCoastalPresentation.Berth(id);Check(berth==island.position+Vector3.forward*29,"Northern berth contract "+id);
                Capture(c,island,id);
                var visit=VisitCamp(c,island,id);try{while(visit.MoveNext())yield return visit.Current;}finally{(visit as IDisposable)?.Dispose();}
            }
            Check(c.Shore.Ground(new Vector3(-409,0,224.05f),out var beach) && c.Shore.Clearance(beach),"Known White Sail marine beach remains open");
            Debug.Log("[COAST-ART] LIMIT: structural checks do not replace same-view/reverse/close visual review or native frame measurements.");
            yield return null;
        }
        private static void Capture(FishingPrototype c,Transform island,int id)
        {
            System.IO.Directory.CreateDirectory("Logs/CoastArt");
            var camera=c.viewCamera.transform;var position=camera.position;var rotation=camera.rotation;
            try
            {
                camera.SetPositionAndRotation(island.position+new Vector3(35,19,38),Quaternion.LookRotation(new Vector3(-35,-18,-38)));
                FishingV016Validation.Capture(c,"Logs/CoastArt","islet-"+id+"-overview-camera-only");
                camera.SetPositionAndRotation(island.position+new Vector3(-26,12,-28),Quaternion.LookRotation(new Vector3(26,-11,28)));
                FishingV016Validation.Capture(c,"Logs/CoastArt","islet-"+id+"-reverse-camera-only");
            }
            finally{camera.SetPositionAndRotation(position,rotation);}
        }
        private static void GroundColours(FishingPrototype c,MeshCollider terrain,Transform island,int id)
        {
            var vertices=terrain.sharedMesh.vertices;var colours=terrain.sharedMesh.colors;int grass=0,sand=0,green=-1,brown=-1;float bestGreen=0,bestBrown=0;
            for(int i=0;i<colours.Length;i++)
            {
                float difference=colours[i].linear.g-colours[i].linear.r;
                if(difference>.10f)grass++;if(difference<-.03f)sand++;
                if(vertices[i].y>.8f && difference>bestGreen){bestGreen=difference;green=i;}
                if(vertices[i].y>.8f && -difference>bestBrown){bestBrown=-difference;brown=i;}
            }
            Check(grass>100 && sand>100 && green>=0 && brown>=0,"Vertex colours select both real atlas surfaces after linear conversion "+id+" grass="+grass+" sand="+sand);
            Debug.Log("[COAST-ART] GROUND PROBE: "+id+" green="+vertices[green]+" colour="+colours[green]+" sand="+vertices[brown]+" colour="+colours[brown]);
            var grassy=GroundPixel(c,island.TransformPoint(vertices[green]),"islet-"+id+"-grass-probe");var sandy=GroundPixel(c,island.TransformPoint(vertices[brown]),"islet-"+id+"-sand-probe");
            Check(grassy.g>grassy.r*1.08f && sandy.r>=sandy.g && sandy.r>sandy.b*1.15f && sandy.g>sandy.b*1.15f,"Actual shader pixels preserve green grass and warm sand "+id+" grass="+grassy+" sand="+sandy);
        }
        private static Color GroundPixel(FishingPrototype c,Vector3 point,string name)
        {
            var camera=c.viewCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;var target=camera.targetTexture;var active=RenderTexture.active;
            bool orthographic=camera.orthographic;float size=camera.orthographicSize;int mask=camera.cullingMask;
            var buffer=RenderTexture.GetTemporary(64,64,24);var pixels=new Texture2D(64,64,TextureFormat.RGB24,false);
            try
            {
                camera.cullingMask=1<<10;camera.orthographic=true;camera.orthographicSize=.25f;camera.transform.SetPositionAndRotation(point+Vector3.up*8,Quaternion.LookRotation(Vector3.down,Vector3.forward));
                camera.targetTexture=buffer;camera.Render();RenderTexture.active=buffer;pixels.ReadPixels(new Rect(0,0,64,64),0,0);pixels.Apply();
                System.IO.File.WriteAllBytes("Logs/CoastArt/"+name+".png",pixels.EncodeToPNG());
                Color mean=Color.black;foreach(var colour in pixels.GetPixels())mean+=colour;return mean/(64*64);
            }
            finally{camera.targetTexture=target;camera.orthographic=orthographic;camera.orthographicSize=size;camera.cullingMask=mask;camera.transform.SetPositionAndRotation(position,rotation);RenderTexture.active=active;RenderTexture.ReleaseTemporary(buffer);UnityEngine.Object.Destroy(pixels);}
        }
        private static IEnumerator VisitCamp(FishingPrototype c,Transform island,int id)
        {
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.CloseHomeChart(true);if(c.Paused)c.TogglePause();c.ReturnToHarbor();
            var rb=c.Player.engine.RB;var berth=FishingCoastalPresentation.Berth(id)+Vector3.up*.7f;rb.position=berth;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.SetPositionAndRotation(berth,Quaternion.identity);Physics.SyncTransforms();
            for(int frame=0;frame<20;frame++)yield return null;Check(c.Shore.TryDisembark(),"The repaired islet retains its actual boat landing "+id);if(!c.FirstPerson)c.ToggleView();
            var camp=island.Find("Weathered timber shelter");
            foreach(float x in new[]{-.6f,0,.6f})foreach(float z in new[]{-.45f,0,.45f})
            {
                var seed=camp.TransformPoint(new Vector3(x,0,z));var local=island.InverseTransformPoint(seed);seed.y=FishingCoastalPresentation.Height(local.x,local.z,id);
                Check(c.Shore.Ground(seed,out var floor) && c.Shore.Clearance(floor) && c.Shore.Relocate(floor,Quaternion.LookRotation(camp.forward)),"Shelter has real supported standing space "+id+" "+x+","+z);
            }
            for(int frame=0;frame<8;frame++){c.Shore.Move(Vector2.zero,false,false,false,.02f);c.PresentCharacterFrame();yield return null;}
            FishingV036FarmValidation.Aim(c,camp.position+camp.forward*5+Vector3.up*1.5f);c.PresentCharacterFrame();FishingV016Validation.Capture(c,"Logs/CoastArt","islet-"+id+"-shelter-interior-standing");
            var approach=camp.position+camp.forward*3.5f;var at=island.InverseTransformPoint(approach);approach.y=FishingCoastalPresentation.Height(at.x,at.z,id);
            Check(c.Shore.Ground(approach,out var feet) && c.Shore.Clearance(feet) && c.Shore.Relocate(feet,Quaternion.LookRotation(-camp.forward)),"Shelter entrance has a supported clear viewing approach "+id);
            for(int frame=0;frame<8;frame++){c.Shore.Move(Vector2.zero,false,false,false,.02f);c.PresentCharacterFrame();yield return null;}
            FishingV036FarmValidation.Aim(c,camp.position+Vector3.up*1.7f);c.PresentCharacterFrame();
            Check(c.viewCamera.transform.position.y>c.Shore.Position.y+1,"Shelter capture uses the real standing eye "+id);
            FishingV016Validation.Capture(c,"Logs/CoastArt","islet-"+id+"-camp-standing");
            FishingV016Validation.Capture(c,"Logs/CoastArt","islet-"+id+"-camp-camera-only");
        }
    }
}
