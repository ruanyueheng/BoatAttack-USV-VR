using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.EditorTools
{
    public static class FishingV027Validation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Run()=>FishingV027Assets.Run();
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("[V027] CHECK: "+message);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(UnityEditor.PlayerSettings.bundleVersion==FishingPrototype.Version,"Home regression runs against the current release version");
            string evidence=Path.GetFullPath("Logs/v027-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
            for(int warm=0;warm<20;warm++)yield return null;
            int triangles=0;
            foreach(string name in new[]{"IslandGround","IslandDetails","HouseShell"})
            {
                var model=Resources.Load<GameObject>("FishingV027/Home/"+name+"View");Check(model!=null,"New coastal model resolves: "+name);
                var filters=model.GetComponentsInChildren<MeshFilter>();Check(filters.Length>0,"New model has geometry: "+name);
                foreach(var filter in filters){triangles+=filter.sharedMesh.triangles.Length/3;Check(filter.sharedMesh.colors32.Length==filter.sharedMesh.vertexCount,"Painted vertex colours survive Unity import: "+name);}
                if(name!="HouseShell")Check(model.GetComponentsInChildren<MeshCollider>().Length>0,"Static ground/detail collision exists: "+name);
            }
            Check(triangles<65000,"Island, cottage and lighthouse remain within 65000 triangles");
            Check(c.Home.Origin==new Vector3(-180,2.2f,10),"Existing home and saved station coordinates are retained");
            var shellFilter=c.Home.House.GetComponentInChildren<MeshFilter>();
            var shellProbe=new GameObject("V027 temporary interior mesh probe");
            try
            {
                shellProbe.transform.SetParent(shellFilter.transform,false);
                var collider=shellProbe.AddComponent<MeshCollider>();collider.sharedMesh=shellFilter.sharedMesh;
                foreach(float side in new[]{-1f,1f})
                {
                    var ray=new Ray(c.Home.Origin+Vector3.up*4.2f,Vector3.forward*side);
                    Check(collider.Raycast(ray,out var hit,6) && hit.distance>4.8f && Vector3.Dot(hit.normal,ray.direction)<-.9f,"Cottage gable closes the room from inside: "+side);
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(shellProbe);}
            ValidateShoreMigration(c);
            foreach(var t in Terrain.activeTerrains)Debug.Log("[V027] TERRAIN: "+t.name+" position="+t.transform.position+" size="+t.terrainData.size);
            if(Physics.Raycast(new Vector3(-146,60,-3),Vector3.down,out var shoreProbe,100,~((1<<5)|(1<<4)),QueryTriggerInteraction.Ignore))
                Debug.Log("[V027] OLD SHORE: "+Hierarchy(shoreProbe.transform)+" type="+shoreProbe.collider.GetType().Name+" point="+shoreProbe.point);
            foreach(var site in c.Shore.Sites)if(site.Available)
            {
                Check(c.Shore.Ground(site.Landing,out var standing) && Mathf.Abs(standing.y-site.Landing.y)<.5f,"Scenery clearing preserves a named landing");
                foreach(var point in site.Tasks)Check(c.Shore.Ground(point,out standing) && Mathf.Abs(standing.y-point.y)<.5f,"Scenery clearing preserves a survey task surface");
            }
            var terrain=c.Home.transform.Find("V027 IslandGround");Check(terrain!=null,"Home builds the new playable island");
            foreach(var renderer in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
            {
                if(renderer.transform.IsChildOf(c.Home.transform) || renderer.bounds.size.y<10 || renderer.bounds.size.x+renderer.bounds.size.z<18)continue;
                if(Vector3.Distance(renderer.bounds.ClosestPoint(c.Home.Origin),c.Home.Origin)<85)
                    Debug.Log("[V027] NEIGHBOUR: "+Hierarchy(renderer.transform)+"; centre="+renderer.bounds.center+"; size="+renderer.bounds.size+"; local="+renderer.transform.position);
            }
            foreach(var p in new[]{new Vector3(0,0,0),new Vector3(-12,0,10),new Vector3(12,0,10),new Vector3(-20,0,0)})
            {
                Vector3 origin=c.Home.Origin+p+Vector3.up*30;
                bool hit=Physics.Raycast(origin,Vector3.down,out var ground,50,~(1<<5),QueryTriggerInteraction.Ignore);
                Check(hit && ground.point.y>0,"Island has a physical dry surface: "+p);
            }
            Capture(c,c.Home.Origin+new Vector3(38,28,-57),c.Home.Origin+new Vector3(-6,5,2),Path.Combine(evidence,"home-island-overview.png"));
            Capture(c,c.Home.Origin+new Vector3(4,2.6f,-14),c.Home.Origin+new Vector3(0,2,-4),Path.Combine(evidence,"cottage-front.png"));
            Capture(c,c.Home.Origin+new Vector3(26,11,7),c.Home.Origin+new Vector3(1,2,0),Path.Combine(evidence,"cottage-side.png"));
            foreach(var checks in new[]{FishingV026HomeValidation.Checks(c),FishingV026SwimmingValidation.Checks(c),FishingV025Validation.Checks(c)})
            {try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}}
            var notebook=c.XRNotebook;var type=typeof(FishingXRNotebook);
            var page=type.GetField("_page",Private);var offset=type.GetField("_offset",Private);var refresh=type.GetMethod("Refresh",Private);
            var canvas=(Canvas)type.GetField("_canvas",Private).GetValue(notebook);
            bool active=canvas.gameObject.activeSelf;
            try
            {
                foreach(int p in new[]{0,1,2,3,4,5,6,7,9,10,11,12,13,14,15,16})
                {
                    page.SetValue(notebook,p);offset.SetValue(notebook,0);refresh.Invoke(notebook,null);canvas.gameObject.SetActive(true);Canvas.ForceUpdateCanvases();
                    if(p!=0)Check(((Text)type.GetField("_status",Private).GetValue(notebook)).transform.GetSiblingIndex()
                        >((GameObject)type.GetField("_pageDecor",Private).GetValue(notebook)).transform.GetSiblingIndex(),"Feature information draws above the paper leaf");
                    foreach(var text in canvas.GetComponentsInChildren<Text>())
                    {
                        if(string.IsNullOrEmpty(text.text))continue;
                        Check(text.fontSize>=20,"VR text size at least 20 on page "+p+": "+text.text.Replace('\n',' '));
                        Check(text.preferredHeight<=text.rectTransform.rect.height+.5f,"VR text fits page "+p+": "+text.text.Replace('\n',' '));
                        foreach(char ch in text.text)if(!char.IsWhiteSpace(ch))Check(text.font.HasCharacter(ch),"VR glyph available: "+ch);
                    }
                    FishingXRPresentationValidation.CaptureCanvas(canvas,Path.Combine(evidence,"ui-page-"+p+".png"));
                    if(p==0)Check(((Text[])type.GetField("_homeCaptions",Private).GetValue(notebook))[0].text=="装备与物资","Returning home restores its original card captions");
                }
            }
            finally{page.SetValue(notebook,0);offset.SetValue(notebook,0);refresh.Invoke(notebook,null);canvas.gameObject.SetActive(active);notebook.Close();}
            Check(FishingUITheme.WorldMaterial.renderQueue==3200,"All shared world UI stays after the sea guidance");
            Check(FishingUITheme.Heading!=null,"Handwritten Chinese heading font is bundled");
            var map=BoatAttack.Editor.FishingMapBaker.BakeInitializedScene();
            Check(map.sources.Any(s=>s.Contains(c.ShopStaticRoot.name)),"Baked map retains the live tackle-shop destination");
            Debug.Log("[V027] EVIDENCE: "+evidence);
        }

        private static string Hierarchy(Transform transform)
        {string path=transform.name;while(transform.parent!=null){transform=transform.parent;path=transform.name+"/"+path;}return path;}

        private static void ValidateShoreMigration(FishingPrototype c)
        {
            var original=c.Career.Snapshot();
            try
            {
                var fixture=c.Career.Snapshot();fixture.workshop.stations.RemoveAll(s=>!s.permanent);fixture.workshop.nextId=1000;
                for(int i=0;i<FishingWorkshop.MaxPlayerStations;i++)fixture.workshop.stations.Add(new WorkshopStationData
                {id=500+i,kind=i==0?CraftStation.Furnace:CraftStation.Workbench,x=-145+i%4*2,y=22,z=-7+i/4*2,
                    recipe=i==0?"iron":"",remaining=i==0?1:0,ready=i==0?1:0,progress=i==0?.42f:0});
                c.RestoreShopTestProfile(fixture);
                var migrate=typeof(FishingHome).GetMethod("RelocateClearedStations",Private|BindingFlags.Public);
                migrate.Invoke(c.Home,null);
                var stations=c.WorkshopWorld.State.Stations.Where(s=>s.id>=500).ToArray();
                Check(stations.Length==FishingWorkshop.MaxPlayerStations,"Shore upgrade preserves every player facility up to the 32-station limit");
                Check(stations.All(s=>s.y<15 && FishingPrototype.FlatDistance(new Vector3(s.x,s.y,s.z),c.Home.Origin)<40),"Former cliff facilities migrate onto the new island");
                var furnace=c.WorkshopWorld.State.Station(500);
                Check(furnace.recipe=="iron" && furnace.remaining==1 && furnace.ready==1 && furnace.progress==.42f,"Relocation preserves reserved material, ready output and processing progress");
                string first=string.Join(";",stations.Select(s=>s.id+":"+s.x+":"+s.y+":"+s.z));
                migrate.Invoke(c.Home,null);
                Check(first==string.Join(";",c.WorkshopWorld.State.Stations.Where(s=>s.id>=500).Select(s=>s.id+":"+s.x+":"+s.y+":"+s.z)),"Reloading the upgraded shore never migrates a supported facility twice");
                fixture.workshop.stations.RemoveAll(s=>!s.permanent);c.RestoreShopTestProfile(fixture);
                var terrain=(Terrain)typeof(FishingHome).GetField("_shoreTerrain",Private).GetValue(c.Home);
                var changed=(Rect)typeof(FishingHome).GetField("_changedShoreArea",Private).GetValue(c.Home);
                var ground=c.Home.transform.Find("V027 IslandGround").GetComponentInChildren<MeshCollider>();
                var corners=new[]{new Vector3(-.7f,0,-.55f),new Vector3(.7f,0,-.55f),new Vector3(-.7f,0,.55f),new Vector3(.7f,0,.55f)};
                var rotation=Quaternion.Euler(0,45,0);Vector3 edge=default;bool foundEdge=false;
                for(float x=14;x<=24 && !foundEdge;x+=.25f)for(float z=-15;z<=25 && !foundEdge;z+=.25f)
                {
                    if(!ground.Raycast(new Ray(c.Home.Origin+new Vector3(x,35,z),Vector3.down),out var hit,50) || hit.point.y<.62f || hit.normal.y<.69f)continue;
                    var point=hit.point+Vector3.up*.02f;
                    if(corners.Any(offset=>!c.Shore.Ground(point+rotation*offset,out _))){edge=point;foundEdge=true;}
                }
                Check(foundEdge,"Boundary fixture finds an island centre with an unsupported rotated corner");
                float sampleSize=terrain.terrainData.size.x/(terrain.terrainData.heightmapResolution-1);
                var cases=new[]{new Vector3(-123.8f,22,-7),new Vector3(-124+sampleSize,22,7),edge};
                for(int i=0;i<cases.Length;i++)fixture.workshop.stations.Add(new WorkshopStationData
                    {id=600+i,kind=CraftStation.Workbench,x=cases[i].x,y=cases[i].y,z=cases[i].z,yaw=45});
                var untouched=new Vector3(changed.xMax+3,22,changed.center.y);
                fixture.workshop.stations.Add(new WorkshopStationData{id=603,kind=CraftStation.Workbench,x=untouched.x,y=untouched.y,z=untouched.z});
                c.RestoreShopTestProfile(fixture);
                Check(ground.Raycast(new Ray(edge+Vector3.up*.05f,Vector3.down),out _,.4f),"Centre-only migration would incorrectly keep the edge fixture");
                migrate.Invoke(c.Home,null);
                for(int i=0;i<cases.Length;i++)
                {
                    var station=c.WorkshopWorld.State.Station(600+i);var point=new Vector3(station.x,station.y,station.z);
                    Check(Vector3.Distance(point,cases[i])>.1f,"Shore boundary facility migrates: "+i+" from "+cases[i]);
                    Check(station.yaw==45 && corners.All(offset=>c.Shore.Ground(point+rotation*offset,out var floor) && Mathf.Abs(floor.y-point.y)<=.3f),"Migrated boundary facility keeps rotation and complete footing: "+i);
                }
                var outside=c.WorkshopWorld.State.Station(603);
                Check(new Vector3(outside.x,outside.y,outside.z)==untouched,"Shore repair leaves unrelated facilities outside the affected footprint unchanged");
                first=string.Join(";",c.WorkshopWorld.State.Stations.Where(s=>s.id>=600).Select(s=>s.id+":"+s.x+":"+s.y+":"+s.z));
                migrate.Invoke(c.Home,null);
                Check(first==string.Join(";",c.WorkshopWorld.State.Stations.Where(s=>s.id>=600).Select(s=>s.id+":"+s.x+":"+s.y+":"+s.z)),"Boundary repairs are stable on a repeated migration");
            }
            finally{c.RestoreShopTestProfile(original);}
        }

        private static void Capture(FishingPrototype c,Vector3 position,Vector3 target,string path)
        {
            var camera=c.viewCamera;var p=camera.transform.position;var q=camera.transform.rotation;float fov=camera.fieldOfView,aspect=camera.aspect;
            var oldTarget=camera.targetTexture;var old=RenderTexture.active;
            var rt=new RenderTexture(1600,1000,24);var photo=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try
            {
                camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=58;camera.aspect=1.6f;camera.targetTexture=rt;
                camera.Render();camera.Render();RenderTexture.active=rt;photo.ReadPixels(new Rect(0,0,1600,1000),0,0);photo.Apply();File.WriteAllBytes(path,photo.EncodeToPNG());
            }
            finally{camera.transform.SetPositionAndRotation(p,q);camera.fieldOfView=fov;camera.aspect=aspect;camera.targetTexture=oldTarget;RenderTexture.active=old;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(photo);}
        }
    }
}
