using System;
using System.Collections.Generic;
using BoatAttack.Fishing;
using UnityEngine;
using Object=UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingXRCollectionValidation
    {
        private static void Check(bool ok,string label)
        {if(!ok)throw new InvalidOperationException(label);Debug.Log("[XR-COLLECTION] CHECK: "+label);}

        public static void Checks(FishingPrototype c)
        {
            var root=new GameObject("Collection contact regression");
            var mesh=new Mesh();GameObject wall=null;
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>();
                mesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};
                mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();
                var palm=new Vector3(.12f,.17f,.03f);
                float nearestVertex=float.MaxValue;
                foreach(var vertex in mesh.vertices)nearestVertex=Mathf.Min(nearestVertex,Vector3.Distance(vertex,palm));
                Check(nearestVertex>FishingXRGrab.Reach,"The face-centre fixture exposes the old nearest-vertex rejection");
                Check(Vector3.Distance(FishingContactGeometry.ClosestPoint(root.transform,palm),new Vector3(.12f,.17f,0))<.0001f,
                    "A palm or pick touching a face centre reaches the actual triangle, not a remote vertex");
                Check(Vector3.Distance(FishingContactGeometry.ClosestPoint(root.transform,new Vector3(1.2f,0,.1f)),Vector3.right)<.0001f,
                    "Outside-face contact clamps to the real edge");
                palm=new Vector3(.12f,.17f,.3f);
                Check(Vector3.Distance(palm,FishingContactGeometry.ClosestPoint(root.transform,palm))>FishingXRGrab.Reach,
                    "An open hand beyond the unchanged 24cm reach cannot pick up the face");
                root.transform.SetPositionAndRotation(new Vector3(700,100,-800),Quaternion.Euler(20,40,-15));
                root.transform.localScale=new Vector3(-2,.5f,1.2f);
                palm=root.transform.TransformPoint(new Vector3(.2f,-.1f,.04f));
                Check(Vector3.Distance(FishingContactGeometry.ClosestPoint(root.transform,palm),root.transform.TransformPoint(new Vector3(.2f,-.1f,0)))<.001f,
                    "Rotated, mirrored and nonuniformly scaled surfaces retain their real contact point");
                var remote=root.transform.TransformPoint(new Vector3(.2f,-.1f,.8f));
                Check(Vector3.Distance(remote,FishingContactGeometry.ClosestPoint(root.transform,remote))>.10f,
                    "Contact with a different spatial target cannot pass the unchanged 10cm pick threshold");
                mesh.Clear(); // Remove the quad's index 3 before shrinking its vertex buffer.
                mesh.vertices=new[]{Vector3.zero,Vector3.zero,Vector3.right};mesh.triangles=new[]{0,1,2};mesh.RecalculateBounds();
                Check(mesh.vertexCount==3 && mesh.GetIndexCount(0)==3,"The degenerate fixture has its intended three vertices and indices");
                root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=Vector3.one;
                Check(Vector3.Distance(FishingContactGeometry.ClosestPoint(root.transform,new Vector3(.4f,.1f,0)),new Vector3(.4f,0,0))<.0001f,
                    "Degenerate triangles fall back to their finite edge");
                if(c!=null)
                {
                    var contact=new Vector3(4000,20,4000);var hand=contact+Vector3.back*.3f;
                    Check(c.Shore.ClearSight(hand,contact,root.transform),"The isolated contact ray begins unobstructed");
                    wall=new GameObject("Collection contact solid wall",typeof(BoxCollider));wall.transform.position=(hand+contact)*.5f;
                    wall.transform.localScale=new Vector3(.4f,.4f,.025f);Physics.SyncTransforms();
                    Check(!c.Shore.ClearSight(hand,contact,root.transform),"A solid wall still blocks contact behind it");
                    wall.SetActive(false);Physics.SyncTransforms();
                    SelectionChecks(c,root.transform,mesh);
                }
            }
            finally
            {
                if(wall!=null)Object.DestroyImmediate(wall);
                Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);Physics.SyncTransforms();
            }
        }
        private static void SelectionChecks(FishingPrototype c,Transform parent,Mesh mesh)
        {
            Check(c.HasIsolatedProfile && !c.Paused,"Contact selection checks use a running isolated profile");
            var nodes=(List<FishingWorkshopWorld.ResourceNode>)c.WorkshopWorld.Nodes;
            var pickups=(List<FishingFieldWorld.Pickup>)c.FieldWorld.Pickups;
            var savedNodes=nodes.ToArray();var savedPickups=pickups.ToArray();
            var available=pickups.FindAll(p=>!c.Career.Backpack.Collected(p.Id));
            Check(available.Count>=2,"Two uncollected production pickup identities exist for the selection fixture");
            bool ashore=c.Ashore,bodyEnabled=c.Shore.Body.enabled;
            var feet=c.Shore.Actor.position;var camera=c.viewCamera.transform.position;
            var a=new GameObject("Near-avatar decoy",typeof(MeshFilter),typeof(MeshRenderer)).transform;
            var b=new GameObject("Touched resource",typeof(MeshFilter),typeof(MeshRenderer)).transform;
            var wall=new GameObject("Touched resource occluder",typeof(BoxCollider));
            a.SetParent(parent,false);b.SetParent(parent,false);
            try
            {
                mesh.vertices=new[]{new Vector3(-.15f,-.15f,0),new Vector3(.15f,-.15f,0),new Vector3(.15f,.15f,0),new Vector3(-.15f,.15f,0)};
                mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();
                a.GetComponent<MeshFilter>().sharedMesh=b.GetComponent<MeshFilter>().sharedMesh=mesh;
                c.Shore.Body.enabled=false;c.Shore.Actor.position=new Vector3(-4000,20,-4000);
                typeof(FishingShore).GetProperty("Ashore").SetValue(c.Shore,true);
                c.viewCamera.transform.position=c.Shore.Actor.position+Vector3.up*1.2f;
                a.position=c.Shore.Actor.position+new Vector3(0,.35f,.3f);b.position=c.Shore.Actor.position+new Vector3(0,.35f,1.1f);
                var decoy=new FishingWorkshopWorld.ResourceNode{Id=0,Item=FieldItem.Stone,Root=a,Feet=a.position};
                var touched=new FishingWorkshopWorld.ResourceNode{Id=1,Item=FieldItem.Stone,Root=b,Feet=b.position};
                nodes.Clear();nodes.Add(decoy);nodes.Add(touched);
                pickups.Clear();
                pickups.Add(new FishingFieldWorld.Pickup{Id=available[0].Id,Item=available[0].Item,Count=available[0].Count,Root=a,Feet=a.position});
                var picked=new FishingFieldWorld.Pickup{Id=available[1].Id,Item=available[1].Item,Count=available[1].Count,Root=b,Feet=b.position};pickups.Add(picked);
                var palm=b.position+Vector3.back*.03f;Physics.SyncTransforms();
                Check(c.WorkshopWorld.TryHarvestTarget(palm,.10f,out var node) && node==touched,
                    "The actual pick contact wins over a different resource nearer the avatar");
                Check(c.FieldWorld.TryPickupTarget(palm,out var pickup) && pickup==picked,
                    "The actual palm contact wins over a different pickup nearer the avatar");
                wall.transform.position=b.position+Vector3.back*.015f;wall.transform.localScale=new Vector3(.4f,.4f,.01f);Physics.SyncTransforms();
                Check(!c.WorkshopWorld.TryHarvestTarget(palm,.10f,out _) && !c.FieldWorld.TryPickupTarget(palm,out _),
                    "A wall between the hand and target rejects both real collection selectors");
                wall.SetActive(false);Physics.SyncTransforms();
                Check(!c.WorkshopWorld.TryHarvestTarget(palm+Vector3.right*.5f,.10f,out _) && !c.FieldWorld.TryPickupTarget(palm+Vector3.right*.5f,out _),
                    "Neither collection selector accepts an empty-space touch beside the target");
            }
            finally
            {
                nodes.Clear();nodes.AddRange(savedNodes);pickups.Clear();pickups.AddRange(savedPickups);
                c.Shore.Actor.position=feet;c.viewCamera.transform.position=camera;
                typeof(FishingShore).GetProperty("Ashore").SetValue(c.Shore,ashore);c.Shore.Body.enabled=bodyEnabled;
                Object.DestroyImmediate(wall);Object.DestroyImmediate(a.gameObject);Object.DestroyImmediate(b.gameObject);Physics.SyncTransforms();
            }
        }
    }
}
