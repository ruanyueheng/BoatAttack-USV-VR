using System;
using System.Linq;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingV018WorldBuilder
    {
        public static void Install()
        {
            try
            {
                var scene=EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
                var existing=scene.GetRootGameObjects().FirstOrDefault(go=>go.name==FishingIslandWorld.WorldName);
                if(existing!=null)Object.DestroyImmediate(existing);
                {
                    const string folder="Assets/Resources/FishingIslands";
                    if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Resources","FishingIslands");
                    var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"/IslandPalette.mat");
                    if(material==null){material=new Material(Resources.Load<Shader>("FishingPalette")){name="Island vertex palette"};AssetDatabase.CreateAsset(material,folder+"/IslandPalette.mat");}
                    var owned=new System.Collections.Generic.List<Object>();
                    var root=FishingIslandWorld.CreateWorld(material,owned);
                    for(int i=0;i<owned.Count;i++)
                    {
                        string path=folder+"/IslandMesh-"+i.ToString("00")+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if(saved==null)AssetDatabase.CreateAsset(owned[i],path);
                        else
                        {
                            EditorUtility.CopySerialized(owned[i],saved);EditorUtility.SetDirty(saved);
                            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh==owned[i])filter.sharedMesh=saved;
                            foreach(var col in root.GetComponentsInChildren<MeshCollider>())if(col.sharedMesh==owned[i])col.sharedMesh=saved;
                            Object.DestroyImmediate(owned[i]);
                        }
                    }
                    Physics.SyncTransforms();
                    for(int island=0;island<4;island++)
                    {
                        int count=island==1?16:island==0?5:3;
                        for(int tree=0;tree<count;tree++)
                        {
                            float a=tree*137.5f*Mathf.Deg2Rad,r=island==1?9+tree%3*5:18+tree%2*3;
                            var p=FishingIslandWorld.Center(island)+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
                            if(p.z>FishingIslandWorld.Center(island).z+18 && Mathf.Abs(p.x-FishingIslandWorld.Center(island).x)<6)continue;
                            if(!Physics.Raycast(p+Vector3.up*30,Vector3.down,out var hit,35) || hit.point.y<.6f)continue;
                            string asset="Assets/Objects/environment/Trees/Prefabs/Palm_0"+(tree%3+1)+".prefab";
                            var palm=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(asset),root.GetChild(island));
                            palm.name="Coastal palm "+tree;palm.transform.position=hit.point;palm.transform.rotation=Quaternion.Euler(0,tree*73,0);palm.transform.localScale=Vector3.one*(.55f+tree%3*.12f);
                        }
                    }
                    foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.isStatic=t.name=="Walkable beach and interior" || t.name=="Distinct natural landmark" || t.name=="Landing jetty";
                }
                var houses=Object.FindObjectsOfType<Transform>(true).Where(t=>
                    PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).Contains("/houses/Prefabs/House"))
                    .OrderBy(t=>t.position.x).ThenBy(t=>t.position.z).ToArray();
                int removed=0;
                for(int i=0;i<houses.Length;i++)
                {
                    if(i%3==0)continue;
                    houses[i].gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(houses[i].gameObject);removed++;
                }
                AssetDatabase.SaveAssets();
                if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Cannot save v18 scene.");
                Debug.Log("[V018-WORLD] PASS: four physical themed islands installed; "+removed+" duplicate house instances removed from fishing scene; docks retained.");
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);}
        }
    }
}
