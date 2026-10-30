using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>World-space stationery. Its pose is fixed when opened, including in a tracked headset.</summary>
    public sealed class FishingLetter : MonoBehaviour
    {
        public const float OpenSeconds = 3.8f;
        private const float PaperScale = .0014f;
        private FishingPrototype _c;
        private Transform _root, _envelope, _flap, _seal, _paper;
        private CanvasGroup _paperFade;
        private Text _heading, _body, _sender, _hint;
        private readonly List<Object> _owned = new List<Object>();
        private readonly FishingPressEdge _advance = new FishingPressEdge(), _close = new FishingPressEdge();
        private Renderer[] _sourceRenderers;
        private bool[] _sourceEnabled;
        public bool Open { get; private set; }
        public float Age { get; private set; }
        public bool Ready => Open && Age >= OpenSeconds;
        public bool CanRead => !Open && !_c.DialogueOpen && !_c.HasPendingCatch && !_c.Prologue.Open && !_c.Home.Sleeping && (!_c.Paused || _c.JournalOpen);
        public Transform Envelope => _envelope;
        public Transform Flap => _flap;
        public Transform Paper => _paper;

        public void Build(FishingPrototype c)
        {
            _c = c; _root = new GameObject("V022 floating letter").transform; _root.SetParent(transform, false);
            _envelope = new GameObject("Linen envelope").transform; _envelope.SetParent(_root, false);
            var paper = new Color(.98f, .94f, .83f); var fold = new Color(.87f, .81f, .65f);
            Quad("Envelope back", _envelope, new[] { new Vector3(-.34f,-.18f,.012f),new Vector3(.34f,-.18f,.012f),new Vector3(.34f,.18f,.012f),new Vector3(-.34f,.18f,.012f) }, paper);
            Quad("Folded pocket", _envelope, new[] { new Vector3(-.34f,.18f,-.006f),new Vector3(0,-.04f,-.02f),new Vector3(.34f,.18f,-.006f),new Vector3(.34f,-.18f,-.02f),new Vector3(-.34f,-.18f,-.02f) }, fold);
            _flap = new GameObject("Hinged envelope flap").transform; _flap.SetParent(_envelope, false); _flap.localPosition = new Vector3(0,.18f,-.024f);
            Quad("Triangular flap", _flap, new[] { new Vector3(-.34f,0,0),new Vector3(0,-.23f,0),new Vector3(.34f,0,0) }, paper);
            var wax = new Material(c.PaletteShader); wax.SetFloat("_Unlit",1); wax.SetColor("_BaseColor",new Color(.37f,.44f,.20f)); _owned.Add(wax);
            _seal = FishingArt.Shape("Wax seal",PrimitiveType.Sphere,_envelope,new Vector3(0,-.055f,-.04f),new Vector3(.069f,.069f,.014f),wax).transform;
            var waxCanvas=new GameObject("Embossed star and tide wax",typeof(RectTransform),typeof(Canvas));waxCanvas.transform.SetParent(_seal,false);
            waxCanvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;waxCanvas.transform.localPosition=new Vector3(0,0,-.55f);waxCanvas.transform.localScale=Vector3.one/256;
            waxCanvas.GetComponent<RectTransform>().sizeDelta=new Vector2(256,256);
            FishingPaperDesign.Image(waxCanvas.transform,"Tidal wax stamp",new Rect(0,0,256,256),new Vector2(256,256),FishingPaperDesign.Seal,Color.white);
            var stamp=new GameObject("Coastal postage and cancellation",typeof(RectTransform),typeof(Canvas),typeof(Image));
            stamp.transform.SetParent(_envelope,false);stamp.transform.localPosition=new Vector3(.16f,.015f,-.033f);stamp.transform.localScale=Vector3.one*.00048f;
            stamp.GetComponent<RectTransform>().sizeDelta=new Vector2(640,320);
            stamp.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            var postage=stamp.GetComponent<Image>();postage.sprite=Stationery("CoastalPostmark");postage.raycastTarget=false;
            var canvas = new GameObject("Letter paper", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup),typeof(CanvasScaler));
            canvas.GetComponent<CanvasScaler>().dynamicPixelsPerUnit=4;
            canvas.layer=5;
            _paper = canvas.transform; _paper.SetParent(_root,false); _paper.localScale = Vector3.one * PaperScale;
            var rect = canvas.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(760,600);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; canvas.GetComponent<Canvas>().worldCamera = c.viewCamera;
            _paperFade = canvas.GetComponent<CanvasGroup>();
            var image = canvas.AddComponent<Image>(); image.sprite=FishingPaperDesign.Vellum;image.type=Image.Type.Sliced;image.color=Color.white; image.raycastTarget = false;image.material=FishingUITheme.WorldMaterial;
            FishingPaperDesign.Frame(_paper,rect.sizeDelta,94,true);
            FishingPaperDesign.Image(_paper,"Sea grass letter crest",new Rect(342,21,76,61),rect.sizeDelta,FishingPaperDesign.Crest,Color.white);
            FishingPaperDesign.Image(_paper,"Title hairline",new Rect(103,153,554,1),rect.sizeDelta,null,new Color(.63f,.51f,.29f,.4f));
            _heading = Label("Title",new Vector2(80,-87),new Vector2(600,59),36,TextAnchor.MiddleCenter);
            Label("Letterhead",new Vector2(68,-35),new Vector2(235,26),15,TextAnchor.UpperLeft).text = "回 潮 海  /  航 海 来 信";
            _body = Label("Letter body",new Vector2(76,-183),new Vector2(608,288),24,TextAnchor.UpperLeft);
            _sender = Label("Signature",new Vector2(76,-486),new Vector2(608,40),22,TextAnchor.UpperRight);
            _hint = Label("Reading hint",new Vector2(76,-546),new Vector2(608,30),16,TextAnchor.MiddleCenter);
            _root.gameObject.SetActive(false);
        }
        private Sprite Stationery(string name)
        {
            var texture=Resources.Load<Texture2D>("FishingStationery/"+name);
            if(texture==null)throw new System.InvalidOperationException("Missing stationery: "+name);
            var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f));_owned.Add(sprite);return sprite;
        }
        private Text Label(string name, Vector2 position, Vector2 size, int font, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));go.layer=5; go.transform.SetParent(_paper,false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<Text>(); text.font = FishingUITheme.Heading ?? FishingUIFont.Font; text.fontSize = font;text.lineSpacing=1f; // Noto Serif SC already carries ~1.44em line height
            text.color = new Color(.23f,.29f,.20f); text.alignment = alignment; text.raycastTarget = false;text.material=FishingUITheme.WorldMaterial;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        private void Quad(string name, Transform parent, Vector3[] vertices, Color color)
        {
            var mesh = new Mesh { name = name }; mesh.vertices = vertices;
            var triangles = new List<int>();
            for (int i=1;i<vertices.Length-1;i++) { triangles.Add(0);triangles.Add(i+1);triangles.Add(i); triangles.Add(0);triangles.Add(i);triangles.Add(i+1); }
            mesh.SetTriangles(triangles,0); var colors = new Color[vertices.Length]; for(int i=0;i<colors.Length;i++)colors[i]=color;
            mesh.colors=colors; mesh.RecalculateNormals(); mesh.RecalculateBounds(); _owned.Add(mesh);
            var uv=new Vector2[vertices.Length];
            for(int i=0;i<uv.Length;i++)uv[i]=new Vector2((vertices[i].x+.34f)/.68f,(vertices[i].y+.18f)/.36f);
            mesh.uv=uv;
            var go = new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var material = FishingArt.Palette(_owned,_c.PaletteShader,"Stationery linen",0,0); material.SetFloat("_Unlit",1);
            material.SetTexture("_BaseMap",Resources.Load<Texture2D>("FishingStationery/EnvelopePaper"));material.SetFloat("_Textured",1);
            go.GetComponent<MeshRenderer>().sharedMaterial=material; go.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        public void ShowIsland(int island) => Show(FishingAdventure.Name(island)+"的来信",FishingLore.ResidentName(island),
            "致回潮村的新村长：\n\n"+FishingAdventure.Clue(island)+"\n\n守望者仍保存着这座岛的失落图页、遗物与设施蓝图。家中的秘密旧海图，会把四段航路连起来。\n\n愿你带着远航的收获，也带着家园新的生活归来。",_c.Islands.WorldRoot.GetChild(island).Find("Voyage letter"));
        internal void Show(string title, string sender, string body, Transform source=null)
        {
            if (!CanRead || _c.Paused) return;
            _c.XRNotebook.Close(); _c.StoryModalChanged();
            _heading.text=title; _sender.text="—— "+sender; _body.text=body;
            _sourceRenderers=source!=null?source.GetComponentsInChildren<Renderer>():null;
            if(_sourceRenderers!=null){_sourceEnabled=new bool[_sourceRenderers.Length];for(int i=0;i<_sourceRenderers.Length;i++){_sourceEnabled[i]=_sourceRenderers[i].enabled;_sourceRenderers[i].enabled=false;}}
            var eye=_c.viewCamera.transform;
            float viewHeight=2*1.2f*Mathf.Tan(_c.viewCamera.fieldOfView*Mathf.Deg2Rad*.5f);
            float scale=XRSettings.isDeviceActive?1:Mathf.Min(viewHeight*.66f/.84f,viewHeight*_c.viewCamera.aspect*.85f/1.24f);
            _root.localScale=Vector3.one*scale;
            _root.SetPositionAndRotation(eye.position+eye.forward*1.2f-eye.up*.02f,eye.rotation);
            Open=true;Age=0;_advance.Reset();_close.Reset();_root.gameObject.SetActive(true);Pose();
        }
        public void Reveal() { if(!Open)return;Age=OpenSeconds;Pose(); }
        public void Close()
        {
            if(!Open)return;Open=false;_root.gameObject.SetActive(false);_advance.Reset();_close.Reset();
            if(_sourceRenderers!=null)for(int i=0;i<_sourceRenderers.Length;i++)if(_sourceRenderers[i]!=null)_sourceRenderers[i].enabled=_sourceEnabled[i];
            _sourceRenderers=null;_sourceEnabled=null;_c.StoryModalChanged();
        }
        public void Tick(float dt, bool xr, bool input)
        {
            if(!Open)return;
            if(!float.IsNaN(dt) && !float.IsInfinity(dt) && dt>0)Age=Mathf.Min(OpenSeconds,Age+dt);
            Pose();
            if(!xr || !input)return;
            var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if(!right.isValid){_advance.Reset();_close.Reset();return;}
            right.TryGetFeatureValue(CommonUsages.primaryButton,out bool advance);
            right.TryGetFeatureValue(CommonUsages.secondaryButton,out bool close);
            if(_close.Step(true,close))Close();
            else if(_advance.Step(true,advance)){if(Ready)Close();else Reveal();}
        }
        private void Pose()
        {
            float rise=Mathf.SmoothStep(0,1,Mathf.Clamp01(Age/.7f));
            float seal=Mathf.SmoothStep(0,1,Mathf.Clamp01((Age-.75f)/.55f));
            float flap=Mathf.SmoothStep(0,1,Mathf.Clamp01((Age-1.2f)/1.1f));
            float extract=Mathf.SmoothStep(0,1,Mathf.Clamp01((Age-2.3f)/1.5f));
            _envelope.localPosition=new Vector3(-extract*.26f,Mathf.Lerp(-.22f,0,rise)-extract*.29f,0);
            _envelope.localRotation=Quaternion.Euler(12*(1-rise),0,-5*(1-rise)-14*extract);
            _seal.localPosition=new Vector3(seal*.12f,-.055f-seal*.10f,-.04f-seal*.09f);
            _seal.localScale=new Vector3(.069f,.069f,.014f)*(1-seal*.15f);
            _flap.localRotation=Quaternion.Euler(-165*flap,0,0);
            _paper.localPosition=new Vector3(.08f*extract,Mathf.Lerp(-.10f,.02f,extract),-.06f);
            _paper.localScale=new Vector3(PaperScale,PaperScale*Mathf.Lerp(.12f,1,extract),PaperScale);
            _paperFade.alpha=Mathf.Clamp01(extract*2);
            _hint.text=XRSettings.isDeviceActive?"A 收起来信  /  B 关闭":"E / Esc 收起来信 · J 日志可重读";
        }
        private void OnDestroy() { foreach(var item in _owned)if(item!=null)Destroy(item); }
    }
}
