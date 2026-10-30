using System;
using System.Linq;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>The remodelled wreck's visible deck is 1.1 m above the former collision mesh.</summary>
    public sealed class FishingWreckAccess : MonoBehaviour
    {
        public Vector3 Start { get; private set; }
        public Vector3 BridgeTop { get; private set; }
        public Vector3 Landing { get; private set; }
        public Vector3 DeckTurn { get; private set; }
        public Vector3 PortalApproach { get; private set; }
        private Material _wood;
        public static void Install(FishingPrototype c,Transform island)
        {
            if(island.GetComponentInChildren<FishingWreckAccess>()!=null)return;
            var hero=island.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Galleon");
            var hull=Resources.Load<Mesh>("FishingV051/WreckHullCollision");var prefab=Resources.Load<GameObject>("FishingV051/WreckAccess");
            if(hull==null || prefab==null)throw new InvalidOperationException("Wreck boarding geometry has not been baked.");
            hero.GetComponentInChildren<MeshCollider>().sharedMesh=hull;
            var root=new GameObject("Visible wreck boarding stair");root.transform.SetParent(island,false);root.transform.SetPositionAndRotation(hero.position,hero.rotation);
            var access=root.AddComponent<FishingWreckAccess>();access._wood=new Material(c.PaletteShader){name="Salt-weathered wreck access wood"};access._wood.SetColor("_BaseColor",Color.white);access._wood.SetFloat("_Gloss",.08f);
            var model=Instantiate(prefab,root.transform,false);model.name="19 real treads and open-ended handrails";
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                filter.gameObject.layer=10;filter.GetComponent<Renderer>().sharedMaterial=access._wood;
                var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
            }
            // This aisle clears the ship's solid hanging rigging beside the former approach.
            access.Start=root.transform.TransformPoint(new Vector3(-11.8f,.02f,2.5f));access.BridgeTop=root.transform.TransformPoint(new Vector3(-6.1f,2.67f,2.5f));access.Landing=root.transform.TransformPoint(new Vector3(-2.67f,2.37f,2.5f));access.DeckTurn=root.transform.TransformPoint(new Vector3(0,2.38f,2.5f));
            FishingRealmWorld.Marker(hero,"Approach0").position=access.Start;
            var first=FishingRealmWorld.Marker(hero,"Approach1");first.position=new Vector3(first.position.x,hero.position.y+2.38f,first.position.z);
            var second=FishingRealmWorld.Marker(hero,"Approach2");second.position=new Vector3(second.position.x,hero.position.y+2.38f,second.position.z);
            // The remodelled hanging rigging overlaps the old marker's standing capsule; wait on the clear deck aisle.
            second.position+=Vector3.ProjectOnPlane(access.DeckTurn-second.position,Vector3.up).normalized*.6f;access.PortalApproach=second.position;
            var portal=island.Find("V019 island character/Portal");var marker=FishingRealmWorld.Marker(hero,"Portal");marker.position=new Vector3(marker.position.x,hero.position.y+2.38f,marker.position.z);
            portal.position=marker.position;portal.Find("Approach").position=second.position;
            var route=island.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Island approach");
            if(route.childCount>=3)
            {
                route.GetChild(route.childCount-3).position=access.Start;route.GetChild(route.childCount-2).position=access.Landing;route.GetChild(route.childCount-1).position=access.PortalApproach;
                var turn=new GameObject("Clear deck aisle").transform;turn.SetParent(route,false);turn.position=access.DeckTurn;turn.SetSiblingIndex(route.childCount-2);
                for(int i=0;i<route.childCount;i++)route.GetChild(i).name="Point "+i;
            }
            Physics.SyncTransforms();
        }
        private void OnDestroy(){if(_wood!=null)Destroy(_wood);}
    }
}
