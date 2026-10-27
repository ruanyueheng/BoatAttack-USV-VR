using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BoatAttack.Fishing
{
    public sealed class FishingIslandWorld : MonoBehaviour
    {
        public const string WorldName = "V018 explorable islands";
        public const float Radius = 30f;
        private static readonly Vector3[] Centers = { new Vector3(-210,0,180), new Vector3(210,0,160), new Vector3(-350,0,-340), new Vector3(350,0,-340) };
        public static Vector3 Center(int island) => Centers[Mathf.Clamp(island,0,3)];
        public static Vector3 Berth(int island) => Center(island) + new Vector3(0,0,Radius+7);
        public static Vector3 Landing(int island) => Center(island) + new Vector3(0,1.08f,Radius*.88f);
        private FishingPrototype _c;
        private Transform _world;
        private Material _signalMaterial;
        private readonly Transform[] _targets = new Transform[12];
        private readonly float[] _chestOpening=new float[4];
        private FishingCareer _chestProfile;
        public Transform WorldRoot => _world;
        public string NearbyHint => TryTarget(out int id, out _) ? id%3==0 ? "E 阅读航海来信 · 发现岛屿 +10"
            : id%3==1 ? "E 打开旧货箱 · 寻宝 +40" : "E 转动航向盘 / F 点亮信标 · " + FishingAdventure.DirectionName(_c.Career.Adventure.Direction(id/3)) : null;

        public void Build(FishingPrototype c)
        {
            _c=c;
            foreach (var root in gameObject.scene.GetRootGameObjects()) if (root.name==WorldName) _world=root.transform;
            if (_world==null) throw new System.InvalidOperationException("Install the v18 island scene with FishingV018WorldBuilder.Install first.");
            for(int i=0;i<4;i++)
            {
                _world.GetChild(i).Find("Walkable beach and interior").gameObject.layer=10;
                for(int j=0;j<3;j++)_targets[i*3+j]=_world.GetChild(i).Find(j==0?"Voyage letter":j==1?"Hidden treasure":"Compass beacon");
                var chest=_targets[i*3+1];var body=chest.GetComponent<BoxCollider>();if(body==null)body=chest.gameObject.AddComponent<BoxCollider>();body.center=Vector3.up*.23f;body.size=new Vector3(.8f,.46f,.5f);
                var lid=chest.Find("Lid");var cover=lid.GetComponent<BoxCollider>();if(cover==null)cover=lid.gameObject.AddComponent<BoxCollider>();cover.center=new Vector3(0,0,-.25f);cover.size=new Vector3(.84f,.12f,.52f);
            }
            _signalMaterial=new Material(c.PaletteShader){name="Restored island signals"};_signalMaterial.SetFloat("_Unlit",1);
            for(int i=0;i<4;i++)_targets[i*3+2].Find("Lit signal").GetComponent<Renderer>().sharedMaterial=_signalMaterial;
            Refresh();
        }
        private void OnDestroy(){if(_signalMaterial!=null)Destroy(_signalMaterial);}
        public bool NearIsland(out int island)
        {
            island=-1;
            for(int i=0;i<4;i++)if(FishingPrototype.FlatDistance(_c.ExplorerPosition,Center(i))<Radius*1.5f){island=i;return true;}
            return false;
        }
        public bool TryTarget(out int id,out Transform root)
        {
            id=-1;root=null;if(_c==null || !_c.Ashore)return false;
            float best=2.6f;
            for(int i=0;i<_targets.Length;i++)
            {
                var t=_targets[i];if(t==null)continue;
                if(i%3==1 && _c.Career.Adventure.TreasureFound(i/3))continue;
                float d=Vector3.Distance(_c.ExplorerPosition,t.position);
                if(d>=best || !_c.Shore.ClearSight(_c.ExplorerPosition+Vector3.up*1.3f,t.position+Vector3.up*.8f))continue;
                id=i;root=t;best=d;
            }
            return id>=0;
        }
        public bool InteractAt(int id)
        {
            if(_c.Paused || _c.StoryBlocksGameplay || _c.InventoryOpen || _c.JournalOpen || _c.ShopBlocksGameplay || !_c.Ashore || id<0 || id>=12 || !TryTarget(out int near,out _) || near!=id)return false;
            int island=id/3;var a=_c.Career.Adventure;int pay=0;
            if(id%3==0)pay=_c.Career.DiscoverIsland(island);
            else if(!a.Visited(island)){_c.Notice("先阅读码头附近的航海来信，找到这座岛的线索。",5);return false;}
            else if(id%3==1)pay=_c.Career.OpenIslandTreasure(island);
            else a.Rotate(island);
            Refresh();_c.Save();
            if(id%3==1 && pay>0)_c.WorldAudio?.Play("MechanismClick",_targets[id].position,.45f);
            if(id%3==0)_c.Letter.ShowIsland(island);
            _c.Notice(id%3==2 ? (a.Restored(island)?"信标已经点亮，航线恢复。":"航向盘朝"+FishingAdventure.DirectionName(a.Direction(island))+" · E 转动，F 确认。"+FishingAdventure.Clue(island))
                : FishingAdventure.Name(island)+" · "+(pay>0?"+"+pay+" 金币 · ":"")+FishingAdventure.Clue(island),9);
            return true;
        }
        public bool ConfirmNearby()
        {
            if(_c.Paused || _c.StoryBlocksGameplay || _c.InventoryOpen || _c.JournalOpen || _c.HasPendingCatch || _c.WorldActionBlocksGameplay || _c.ShopBlocksGameplay
                || !TryTarget(out int id,out _) || id%3!=2)return false;
            int pay=_c.Career.RestoreIslandBeacon(id/3);
            if(pay==0){_c.Notice("灯头未对准来信里的方向；E 转动航向盘，F 再次确认。",5);return false;}
            Refresh();_c.Save();_c.Notice("群岛信标点亮 +75 金币！"+(_c.Career.Adventure.ReadyForFinale?"四灯已亮，返港 E 提交群岛航记 +150。":"可继续自由寻宝或航向另一座岛。"),7);return true;
        }
        public int CompleteAtHarbor()
        {
            if(!_c.AtHarbor || _c.Paused)return 0;
            int pay=_c.Career.CompleteIslandVoyage();
            if(pay>0){_c.Save();_c.Notice("四座岛的灯重新连成航线。群岛航记完成 +150 金币；仍可自由捕鱼、寻宝和升级。",9);}
            return pay;
        }
        public void Refresh()
        {
            bool changed=_chestProfile!=_c.Career;_chestProfile=_c.Career;
            for(int i=0;i<4;i++)
            {
                if(changed)_chestOpening[i]=_c.Career.Adventure.TreasureFound(i)?1:0;
                if(_targets[i*3+1]!=null)_targets[i*3+1].Find("Lid").localRotation=Quaternion.Euler(100*Mathf.SmoothStep(0,1,_chestOpening[i]),0,0);
                if(_targets[i*3+2]==null)continue;
                _targets[i*3+2].Find("Direction pointer").localRotation=Quaternion.Euler(0,_c.Career.Adventure.Direction(i)*90,0);
                _targets[i*3+2].Find("Lit signal").gameObject.SetActive(_c.Career.Adventure.Restored(i));
            }
        }
        private void Update()
        {
            if(_c==null || _c.Paused)return;
            for(int i=0;i<4;i++)
            {
                float next=Mathf.MoveTowards(_chestOpening[i],_c.Career.Adventure.TreasureFound(i)?1:0,Time.deltaTime/.75f);
                if(next==_chestOpening[i] || _targets[i*3+1]==null)continue;
                _chestOpening[i]=next;_targets[i*3+1].Find("Lid").localRotation=Quaternion.Euler(100*Mathf.SmoothStep(0,1,next),0,0);
            }
        }

        // Editor-created meshes are persisted as assets. Player runtime only updates the three interactive targets.
        public static Transform CreateWorld(Material material,List<Object> owned)
        {
            var root=new GameObject(WorldName).transform;
            Color sand=new Color(.83f,.75f,.53f),grass=new Color(.26f,.45f,.33f),stone=new Color(.42f,.47f,.48f),wood=new Color(.38f,.24f,.13f),gold=new Color(.91f,.68f,.29f);
            for(int island=0;island<4;island++)
            {
                var t=new GameObject(FishingAdventure.Name(island)).transform;t.SetParent(root,false);t.position=Center(island);
                Mesh terrain=TerrainMesh(island);owned.Add(terrain);var ground=MeshObject("Walkable beach and interior",t,terrain,material);
                ground.gameObject.layer=10;
                ground.gameObject.AddComponent<MeshCollider>().sharedMesh=terrain;
                var art=new FishingMeshBuilder();
                for(int k=0;k<22;k++)
                {
                    float a=k*137.5f*Mathf.Deg2Rad,r=8+(k%4)*4;var p=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
                    p.y=Height(island,r/Radius);
                    if(p.z>14 && Mathf.Abs(p.x)<6)continue;
                    if(island==1)
                    {
                        for(int leaf=0;leaf<7;leaf++)
                        {
                            float angle=leaf*Mathf.PI*2/7;var tip=p+new Vector3(Mathf.Cos(angle)*1.1f,.5f,Mathf.Sin(angle)*1.1f);
                            art.Fin(p,tip+Vector3.right*.2f,tip-Vector3.right*.2f,grass);
                        }
                    }
                    else if(k%3==0)art.Ellipsoid(p+Vector3.up*.25f,new Vector3(1.4f,.7f,1),stone,8,4);
                    else
                    {
                        art.Tube(p,p+Vector3.up*.6f,.025f,.01f,grass,5);
                        art.Ellipsoid(p+Vector3.up*.7f,new Vector3(.18f,.1f,.18f),island==0?gold:grass,6,3);
                    }
                }
                Vector3 feature=new Vector3(8,Height(island,.4f),-7);
                if(island==0)
                {
                    art.Ellipsoid(feature+new Vector3(-3,3,0),new Vector3(1.7f,3,1.6f),stone,9,5);
                    art.Ellipsoid(feature+new Vector3(3,3,0),new Vector3(1.7f,3,1.6f),stone,9,5);
                    art.Ellipsoid(feature+Vector3.up*6,new Vector3(4.1f,1.1f,1.6f),stone,10,5);
                    for(int j=0;j<6;j++)art.Tube(feature+new Vector3(j*.32f-1,5,0),feature+new Vector3(j*.32f-1,4-j*.12f,0),.028f,.028f,gold,6);
                }
                else if(island==2)
                {
                    for(int j=0;j<11;j++)
                    {
                        float z=(j-5)*.6f,w=1.8f-Mathf.Abs(j-5)*.18f;
                        art.Tube(feature+new Vector3(-w,.2f,z),feature+new Vector3(-w*.6f,1.5f,z),.08f,.08f,wood);
                        art.Tube(feature+new Vector3(w,.2f,z),feature+new Vector3(w*.6f,1.5f,z),.08f,.08f,wood);
                        art.Box(feature+new Vector3(0,.12f,z),new Vector3(w*2,.12f,.52f),wood);
                    }
                    art.Tube(feature,feature+new Vector3(-2,5,1),.18f,.11f,wood);
                }
                else if(island==3)
                {
                    for(int j=0;j<6;j++)
                    {
                        var p=feature+new Vector3((j%3-1)*4,0,j/3*5);
                        art.Tube(p,p+Vector3.up*(j==4?1.2f:4),.55f,.45f,stone,12);
                        art.Box(p+Vector3.up*(j==4?1.2f:4),new Vector3(1.3f,.25f,1.3f),sand);
                    }
                    art.Tube(feature+new Vector3(-4,4,0),feature+new Vector3(4,4,0),.48f,.48f,stone,6);
                }
                Mesh scenery=art.Finish("Island landmark "+island);owned.Add(scenery);MeshObject("Distinct natural landmark",t,scenery,material);
                Target(t,"Voyage letter",new Vector3(3,Height(island,.78f),23),material,owned,0);
                Target(t,"Hidden treasure",feature+new Vector3(0,0,-2),material,owned,1);
                Target(t,"Compass beacon",new Vector3(-6,Height(island,.33f),8),material,owned,2);
                var ramp=new FishingMeshBuilder();ramp.Box(Vector3.zero,new Vector3(2.4f,.16f,15),wood);
                var rm=ramp.Finish("Island landing ramp "+island);owned.Add(rm);
                var jetty=MeshObject("Landing jetty",t,rm,material);jetty.localPosition=new Vector3(0,1.04f,29);
                jetty.gameObject.AddComponent<BoxCollider>().size=new Vector3(2.4f,.16f,15);
            }
            return root;
        }
        private static Transform Target(Transform parent,string name,Vector3 position,Material material,List<Object> owned,int kind)
        {
            var b=new FishingMeshBuilder();var wood=new Color(.38f,.24f,.13f);var brass=new Color(.91f,.68f,.29f);
            if(kind==0){b.Box(new Vector3(0,.6f,0),new Vector3(.06f,1.2f,.06f),wood);b.Box(new Vector3(0,1.05f,0),new Vector3(.55f,.38f,.06f),new Color(.9f,.84f,.64f));}
            else if(kind==1){b.Box(new Vector3(0,.23f,0),new Vector3(.8f,.46f,.5f),wood);b.Box(new Vector3(0,.24f,-.26f),new Vector3(.09f,.3f,.035f),brass);}
            else {b.Tube(Vector3.zero,Vector3.up*.75f,.32f,.24f,new Color(.34f,.41f,.43f),12);b.Tube(Vector3.up*.75f,Vector3.up*.81f,.38f,.38f,brass,16);}
            var mesh=b.Finish(name);owned.Add(mesh);var t=MeshObject(name,parent,mesh,material);t.localPosition=position;
            if(kind==1){var lid=new FishingMeshBuilder();lid.Box(new Vector3(0,0,-.25f),new Vector3(.84f,.12f,.52f),brass);var lm=lid.Finish("Chest lid");owned.Add(lm);MeshObject("Lid",t,lm,material).localPosition=new Vector3(0,.48f,.25f);}
            if(kind==2)
            {
                var arrow=new FishingMeshBuilder();arrow.Box(new Vector3(0,0,.16f),new Vector3(.07f,.04f,.38f),brass);arrow.Fin(new Vector3(-.14f,0,.26f),new Vector3(0,0,.46f),new Vector3(.14f,0,.26f),brass);
                var am=arrow.Finish("Compass arrow");owned.Add(am);MeshObject("Direction pointer",t,am,material).localPosition=Vector3.up*.88f;
                var light=new FishingMeshBuilder();light.Ellipsoid(Vector3.up*1.7f,new Vector3(.18f,.6f,.18f),new Color(1,.85f,.25f),10,5);
                var lm=light.Finish("Restored signal");owned.Add(lm);MeshObject("Lit signal",t,lm,material);
            }
            return t;
        }
        private static Transform MeshObject(string name,Transform parent,Mesh mesh,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;go.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            return go.transform;
        }
        private static float Height(int island,float r) => r>.93f?Mathf.Lerp(.92f,-1.5f,Mathf.InverseLerp(.93f,1.12f,r)) : .92f+Mathf.Max(0,.72f-r)*(island==3?3.3f:2.5f);
        private static Mesh TerrainMesh(int island)
        {
            const int sides=64;float[] rings={0,.2f,.4f,.6f,.72f,.86f,.93f,1.02f,1.12f};
            var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
            for(int r=0;r<rings.Length;r++)for(int j=0;j<=sides;j++)
            {
                float a=j*Mathf.PI*2/sides,f=1+.045f*Mathf.Sin(a*3+island)+.025f*Mathf.Cos(a*7+island);
                if(island==0)f*=1-.42f*Mathf.Pow(Mathf.Max(0,Mathf.Cos(a)),4);
                if(island==1)f*=1+.17f*Mathf.Sin(a*2);
                if(island==3)f*=.87f+.09f*Mathf.Cos(a*5);
                vertices.Add(new Vector3(Mathf.Cos(a)*rings[r]*Radius*f*(island==2?1.3f:1),Height(island,rings[r]),Mathf.Sin(a)*rings[r]*Radius*f*(island==2?.88f:1)));
                colors.Add(rings[r]<.72f?new Color(.32f,.47f,.32f):new Color(.83f,.75f,.53f));
                if(r==0 || j==sides)continue;int k=r*(sides+1)+j;
                triangles.AddRange(new[]{k,k-sides-1,k+1,k+1,k-sides-1,k-sides});
            }
            var mesh=new Mesh{name="Physical island "+island};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
