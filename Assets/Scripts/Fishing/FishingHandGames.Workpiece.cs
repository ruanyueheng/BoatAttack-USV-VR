using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHandGames
    {
        private sealed class WorkpieceVisual
        {
            public Transform Root;
            public Mesh Mesh,Debris;
            public MeshRenderer DebrisRenderer;
            public float Progress=-1,ResetAt=-1;
            public int Strokes;
            public readonly Vector3[] DebrisStart=new Vector3[48],Velocity=new Vector3[48];
            public readonly float[] Age=new float[48],Life=new float[48];
            public int Next;
        }
        private readonly WorkpieceVisual[] _workpieces=new WorkpieceVisual[2];
        private Material _workpieceMaterial,_metalWorkpieceMaterial,_workDebrisMaterial;
        private readonly List<Vector3> _workVertices=new List<Vector3>(512);
        private readonly List<Color> _workColors=new List<Color>(512);
        private readonly List<int> _workIndices=new List<int>(1024);
        public Transform Workpiece(int station)=>station>=2 && station<=3?_workpieces[station-2]?.Root:null;
        private static float ForgeSurface(float progress)=>1.035f-.040f*Mathf.Clamp01(progress);

        private void BuildWorkpiece(int station,Transform original)
        {
            if(_workpieceMaterial==null)
            {
                _workpieceMaterial=new Material(_c.PaletteShader){name="Cut timber and worked metal"};
                _workpieceMaterial.SetFloat("_Shadows",1);_workpieceMaterial.SetFloat("_Gloss",.2f);
                _metalWorkpieceMaterial=new Material(_c.PaletteShader){name="Cool steel billet with forged highlights"};
                _metalWorkpieceMaterial.SetFloat("_Shadows",1);_metalWorkpieceMaterial.SetFloat("_Gloss",.72f);_metalWorkpieceMaterial.SetFloat("_Rim",.13f);
                _workDebrisMaterial=new Material(_c.PaletteShader){name="Bright forge sparks and fresh sawdust"};
                _workDebrisMaterial.SetFloat("_Unlit",1);_workDebrisMaterial.SetFloat("_Cull",0);
            }
            foreach(var renderer in original.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            var root=new GameObject(station==2?"Timber with progressive through kerf":"Iron billet shaped by actual hammer blows").transform;
            root.SetParent(_stations[station],false);
            var visual=new WorkpieceVisual{Root=root,Mesh=new Mesh{name="Dynamic handcraft workpiece"},Debris=new Mesh{name="Pooled bilateral work debris"}};
            root.gameObject.AddComponent<MeshFilter>().sharedMesh=visual.Mesh;
            root.gameObject.AddComponent<MeshRenderer>().sharedMaterial=station==3?_metalWorkpieceMaterial:_workpieceMaterial;
            var debris=new GameObject(station==2?"Saw chips at the cut":"Sparks thrown from both sides of the billet",typeof(MeshFilter),typeof(MeshRenderer));
            debris.transform.SetParent(_stations[station],false);debris.GetComponent<MeshFilter>().sharedMesh=visual.Debris;
            visual.DebrisRenderer=debris.GetComponent<MeshRenderer>();visual.DebrisRenderer.sharedMaterial=_workDebrisMaterial;
            visual.DebrisRenderer.shadowCastingMode=ShadowCastingMode.Off;visual.DebrisRenderer.receiveShadows=false;
            visual.Mesh.MarkDynamic();visual.Debris.MarkDynamic();
            for(int i=0;i<visual.Age.Length;i++)visual.Age[i]=-1;
            _workpieces[station-2]=visual;UpdateWorkpiece(station,visual,0);
        }
        private void BeginWorkpiece(int station)
        {
            if(station<2)return;var visual=_workpieces[station-2];
            visual.ResetAt=-1;visual.Strokes=0;UpdateWorkpiece(station,visual,0);
            for(int i=0;i<visual.Age.Length;i++)visual.Age[i]=-1;
            visual.Debris.Clear();visual.DebrisRenderer.enabled=false;
        }
        private void ReadWorkpieceProgress(FishingHandTask task)
        {
            if(task==null || _index<2)return;var visual=_workpieces[_index-2];
            UpdateWorkpiece(_index,visual,task.Progress);
            if(task.Strokes<=visual.Strokes)return;
            // Only the production gesture rule advances Strokes. Hand motion in the air never emits impact effects.
            EmitWorkDebris(_index,visual,task.Strokes);visual.Strokes=task.Strokes;
        }
        private void EndWorkpiece(FishingHandTask task)
        {
            if(_index<2)return;
            ReadWorkpieceProgress(task);
            // Keep the completed cut/shape visible after the inventory transaction closes the craft modal.
            _workpieces[_index-2].ResetAt=Time.unscaledTime+(task.Claimed?3f:1.25f);
        }
        private void PresentWorkpieces(float dt)
        {
            if(_workpieces[0]==null)return;
            if(Active)ReadWorkpieceProgress(_task);
            for(int station=2;station<4;station++)
            {
                var visual=_workpieces[station-2];
                if(visual.ResetAt>=0 && Time.unscaledTime>=visual.ResetAt)
                {visual.ResetAt=-1;visual.Strokes=0;UpdateWorkpiece(station,visual,0);}
                PresentWorkDebris(station,visual,_c.Paused?0:Mathf.Min(dt,.05f));
            }
        }
        private void ClearWorkGeometry(){_workVertices.Clear();_workColors.Clear();_workIndices.Clear();}
        private void WorkQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
        {
            int start=_workVertices.Count;_workVertices.Add(a);_workVertices.Add(b);_workVertices.Add(c);_workVertices.Add(d);
            for(int i=0;i<4;i++)_workColors.Add(color);
            _workIndices.Add(start);_workIndices.Add(start+1);_workIndices.Add(start+2);
            _workIndices.Add(start);_workIndices.Add(start+2);_workIndices.Add(start+3);
        }
        private void WorkBox(Vector3 minimum,Vector3 maximum,Color color,bool cutLeft=false,bool cutRight=false)
        {
            if(maximum.y-minimum.y<.00001f || maximum.x-minimum.x<.00001f)return;
            float x=minimum.x,X=maximum.x,y=minimum.y,Y=maximum.y,z=minimum.z,Z=maximum.z;
            WorkQuad(new Vector3(x,Y,z),new Vector3(x,Y,Z),new Vector3(X,Y,Z),new Vector3(X,Y,z),color*1.1f);
            WorkQuad(new Vector3(x,y,Z),new Vector3(x,y,z),new Vector3(X,y,z),new Vector3(X,y,Z),color*.68f);
            WorkQuad(new Vector3(x,y,z),new Vector3(x,Y,z),new Vector3(X,Y,z),new Vector3(X,y,z),color*.84f);
            WorkQuad(new Vector3(X,y,Z),new Vector3(X,Y,Z),new Vector3(x,Y,Z),new Vector3(x,y,Z),color*.93f);
            var fresh=new Color(.90f,.69f,.37f);
            WorkQuad(new Vector3(x,y,Z),new Vector3(x,Y,Z),new Vector3(x,Y,z),new Vector3(x,y,z),cutLeft?fresh:color*.78f);
            WorkQuad(new Vector3(X,y,z),new Vector3(X,Y,z),new Vector3(X,Y,Z),new Vector3(X,y,Z),cutRight?fresh:color*.88f);
        }
        private void UploadWorkGeometry(Mesh mesh)
        {
            mesh.Clear();mesh.SetVertices(_workVertices);mesh.SetColors(_workColors);mesh.SetTriangles(_workIndices,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
        }
        private void UpdateWorkpiece(int station,WorkpieceVisual visual,float progress)
        {
            progress=Mathf.Clamp01(progress);if(Mathf.Abs(visual.Progress-progress)<.0001f)return;visual.Progress=progress;
            ClearWorkGeometry();
            if(station==2)
            {
                const float cut=-.12f,bottom=.975f,top=1.025f;
                float cutY=top-progress*(top-bottom),gap=progress<=0?0:Mathf.Lerp(.003f,.014f,progress);
                float split=Mathf.SmoothStep(0,.035f,Mathf.InverseLerp(.9f,1,progress));
                var wood=new Color(.59f,.32f,.14f);
                // The uncut lower web becomes thinner on every valid stroke; at 100% it is gone.
                WorkBox(new Vector3(-.30f,bottom,-.09f),new Vector3(.30f,cutY,.09f),wood);
                WorkBox(new Vector3(-.30f-split,cutY,-.09f),new Vector3(cut-gap*.5f-split,top,.09f),wood,cutRight:progress>0);
                WorkBox(new Vector3(cut+gap*.5f+split,cutY,-.09f),new Vector3(.30f+split,top,.09f),wood,cutLeft:progress>0);
                for(int line=0;line<5;line++)
                {
                    float z=-.071f+line*.035f;var grain=new Color(.31f,.15f,.055f);
                    WorkBox(new Vector3(-.29f-split,top+.0002f,z),new Vector3(cut-gap*.5f-split-.002f,top+.0006f,z+.0012f),grain);
                    WorkBox(new Vector3(cut+gap*.5f+split+.002f,top+.0002f,z),new Vector3(.29f+split,top+.0006f,z+.0012f),grain);
                }
            }
            else
            {
                // Cross sections spread and flatten while the middle develops a hammer depression.
                // Every point comes from actual committed stroke progress, never from elapsed animation time.
                const int sections=8;var ring=new Vector3[(sections+1)*4];
                float length=Mathf.Lerp(.20f,.33f,progress),thickness=Mathf.Lerp(.07f,.035f,progress);
                for(int section=0;section<=sections;section++)
                {
                    float t=section/(float)sections,x=(t-.5f)*length;
                    float taper=Mathf.Lerp(1,Mathf.Lerp(.43f,1,Mathf.Sin(t*Mathf.PI)),progress);
                    float width=Mathf.Lerp(.12f,.145f,progress)*taper;
                    float dent=Mathf.Pow(Mathf.Sin(t*Mathf.PI),6)*progress*.005f;
                    float floor=.965f,roof=floor+thickness-dent;
                    ring[section*4]=new Vector3(-.10f+x,floor,-.12f-width*.5f);
                    ring[section*4+1]=new Vector3(-.10f+x,roof,-.12f-width*.5f);
                    ring[section*4+2]=new Vector3(-.10f+x,roof,-.12f+width*.5f);
                    ring[section*4+3]=new Vector3(-.10f+x,floor,-.12f+width*.5f);
                }
                for(int section=0;section<sections;section++)for(int side=0;side<4;side++)
                {
                    int a=section*4+side,b=section*4+(side+1)%4,c=b+4,d=a+4;
                    // Keep the body recognizably steel under the warm porch light. Only the struck
                    // middle of the top face takes a restrained red tint; the ends and sides stay cool.
                    var steel=side==1?new Color(.43f,.52f,.63f):new Color(.28f,.35f,.44f);
                    float hammerMark=side==1 && section%2==0?1:.88f;
                    float heat=side==1 && (section==3 || section==4)?progress*.34f:0;
                    var color=Color.Lerp(steel,new Color(.68f,.16f,.085f),heat)*hammerMark;
                    WorkQuad(ring[a],ring[b],ring[c],ring[d],color);
                }
                WorkQuad(ring[3],ring[2],ring[1],ring[0],new Color(.24f,.30f,.38f));
                int last=sections*4;WorkQuad(ring[last],ring[last+1],ring[last+2],ring[last+3],new Color(.36f,.43f,.52f));
            }
            UploadWorkGeometry(visual.Mesh);
        }
        private void EmitWorkDebris(int station,WorkpieceVisual visual,int stroke)
        {
            int count=station==3?32:12;
            for(int particle=0;particle<count;particle++)
            {
                int i=visual.Next++%visual.Age.Length;float side=particle%2==0?-1:1;
                float scatter=Mathf.Repeat((particle+stroke*7)*.618034f,1);
                visual.Age[i]=0;visual.Life[i]=station==3?.30f+scatter*.32f:.35f+scatter*.2f;
                visual.DebrisStart[i]=station==3?new Vector3(-.10f,ForgeSurface(visual.Progress),-.12f):new Vector3(-.12f,1.025f,-.065f+scatter*.13f);
                visual.Velocity[i]=station==3?new Vector3(side*(.55f+scatter*.65f),.4f+scatter*.8f,(Mathf.Repeat(particle*.381966f,1)-.5f)*.8f)
                    :new Vector3(side*(.08f+scatter*.15f),.15f+scatter*.25f,(scatter-.5f)*.3f);
            }
            PresentWorkDebris(station,visual,0);
        }
        private void PresentWorkDebris(int station,WorkpieceVisual visual,float dt)
        {
            ClearWorkGeometry();bool any=false;
            for(int i=0;i<visual.Age.Length;i++)
            {
                if(visual.Age[i]<0)continue;float age=visual.Age[i]+=dt;
                if(age>visual.Life[i]){visual.Age[i]=-1;continue;}any=true;
                var position=visual.DebrisStart[i]+visual.Velocity[i]*age+Vector3.down*(station==3?2.2f:1.3f)*age*age;
                float fade=1-age/visual.Life[i],radius=(station==3?.0025f:.002f)*Mathf.Max(.2f,fade);
                var axis=(visual.Velocity[i]+Vector3.down*age*4).normalized*(station==3?.018f:.0035f)*fade;
                var side=Vector3.Cross(axis,Vector3.up).normalized*radius;var up=Vector3.Cross(axis,side).normalized*radius;
                var a=position-axis;var b=position+axis;
                var color=station==3?Color.Lerp(new Color(.8f,.10f,.008f),new Color(3.5f,2.0f,.35f),fade):new Color(.82f,.58f,.27f)*(.6f+fade*.4f);
                // Two crossed ribbons, visible from either eye and either face without coincident triangles.
                WorkQuad(a+side,b+side,b-side,a-side,color);WorkQuad(a+up,b+up,b-up,a-up,color);
            }
            visual.DebrisRenderer.enabled=any;
            if(any)UploadWorkGeometry(visual.Debris);else visual.Debris.Clear();
        }
        private void DisposeWorkpieces()
        {
            foreach(var visual in _workpieces)if(visual!=null){if(visual.Mesh!=null)Destroy(visual.Mesh);if(visual.Debris!=null)Destroy(visual.Debris);}
            if(_workpieceMaterial!=null)Destroy(_workpieceMaterial);if(_metalWorkpieceMaterial!=null)Destroy(_metalWorkpieceMaterial);if(_workDebrisMaterial!=null)Destroy(_workDebrisMaterial);
        }
    }
}
