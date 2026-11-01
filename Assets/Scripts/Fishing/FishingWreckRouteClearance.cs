using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Remove the cropped racing mainland on the southwest voyage, before depth and hull routing are built.</summary>
    public sealed class FishingWreckRouteClearance : MonoBehaviour
    {
        public static readonly Rect Area=Rect.MinMaxRect(-500,-250,-112,-110);
        private readonly List<(Terrain terrain,TerrainData original,TerrainData copy)> _terrains=new List<(Terrain,TerrainData,TerrainData)>();
        private readonly HashSet<GameObject> _hidden=new HashSet<GameObject>();
        public bool Cleared=>_terrains.Count>0;
        public void Build()
        {
            foreach(var terrain in FindObjectsOfType<Terrain>())
            {
                var original=terrain.terrainData;var p=terrain.transform.position;var size=original.size;
                if(!Area.Overlaps(new Rect(p.x,p.z,size.x,size.z)))continue;
                var copy=Instantiate(original);copy.name="Southwest voyage seabed";
                int n=copy.heightmapResolution;var heights=copy.GetHeights(0,0,n,n);
                float floor=Mathf.Clamp01((-6-p.y)/size.y);
                for(int z=0;z<n;z++)for(int x=0;x<n;x++)
                {
                    float wx=p.x+x*size.x/(n-1),wz=p.z+z*size.z/(n-1);
                    if(wx<Area.xMin || wx>Area.xMax || wz<Area.yMin || wz>Area.yMax)continue;
                    float blend=Mathf.SmoothStep(0,1,Mathf.Min((Area.xMax-wx)/24,(Area.yMax-wz)/24));
                    heights[z,x]=Mathf.Lerp(heights[z,x],Mathf.Min(heights[z,x],floor),blend);
                }
                copy.SetHeights(0,0,heights);terrain.terrainData=copy;
                var collider=terrain.GetComponent<TerrainCollider>();if(collider!=null)collider.terrainData=copy;
                terrain.Flush();_terrains.Add((terrain,original,copy));
            }
            foreach(var root in gameObject.scene.GetRootGameObjects())
            {
                if(root.name!="Island Level")continue;
                var environment=root.transform.Find("Environment Objects");if(environment==null)continue;
                foreach(var renderer in environment.GetComponentsInChildren<Renderer>())
                {
                    var b=renderer.bounds;if(!Area.Contains(new Vector2(b.center.x,b.center.z)))continue;
                    var lod=renderer.GetComponentInParent<LODGroup>();Hide(lod!=null?lod.gameObject:renderer.gameObject);
                }
                foreach(var collider in environment.GetComponentsInChildren<Collider>())
                {var p=collider.bounds.center;if(Area.Contains(new Vector2(p.x,p.z)))Hide(collider.gameObject);}
            }
            Physics.SyncTransforms();
        }
        private void Hide(GameObject go){if(go.activeSelf && _hidden.Add(go))go.SetActive(false);}
        private void OnDestroy()
        {
            foreach(var entry in _terrains)
            {
                if(entry.terrain!=null && entry.terrain.terrainData==entry.copy)
                {
                    entry.terrain.terrainData=entry.original;
                    var collider=entry.terrain.GetComponent<TerrainCollider>();if(collider!=null)collider.terrainData=entry.original;
                    entry.terrain.Flush();
                }
                if(entry.copy!=null)Destroy(entry.copy);
            }
            foreach(var go in _hidden)if(go!=null)go.SetActive(true);
        }
    }
}
