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
        public Transform Envelope => _envelope;
        public Transform Flap => _flap;
        public Transform Paper => _paper;

        public void Build(FishingPrototype c)
        {
            _c = c; _root = new GameObject("V022 floating letter").transform; _root.SetParent(transform, false);
            _envelope = new GameObject("Linen envelope").transform; _envelope.SetParent(_root, false);
            var paper = new Color(.88f, .79f, .60f); var fold = new Color(.73f, .62f, .43f);
            Quad("Envelope back", _envelope, new[] { new Vector3(-.34f,-.18f,.012f),new Vector3(.34f,-.18f,.012f),new Vector3(.34f,.18f,.012f),new Vector3(-.34f,.18f,.012f) }, paper);
            Quad("Folded pocket", _envelope, new[] { new Vector3(-.34f,.18f,-.006f),new Vector3(0,-.04f,-.02f),new Vector3(.34f,.18f,-.006f),new Vector3(.34f,-.18f,-.02f),new Vector3(-.34f,-.18f,-.02f) }, fold);
            _flap = new GameObject("Hinged envelope flap").transform; _flap.SetParent(_envelope, false); _flap.localPosition = new Vector3(0,.18f,-.024f);
            Quad("Triangular flap", _flap, new[] { new Vector3(-.34f,0,0),new Vector3(0,-.23f,0),new Vector3(.34f,0,0) }, paper);
            var wax = new Material(c.PaletteShader); wax.SetFloat("_Unlit",1); wax.SetColor("_BaseColor",new Color(.48f,.14f,.10f)); _owned.Add(wax);
            _seal = FishingArt.Shape("Wax seal",PrimitiveType.Sphere,_envelope,new Vector3(0,-.055f,-.04f),new Vector3(.069f,.069f,.014f),wax).transform;
            var canvas = new GameObject("Letter paper", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            _paper = canvas.transform; _paper.SetParent(_root,false); _paper.localScale = Vector3.one * .00105f;
            var rect = canvas.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(560,640);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; canvas.GetComponent<Canvas>().worldCamera = c.viewCamera;
            _paperFade = canvas.GetComponent<CanvasGroup>();
            var image = canvas.AddComponent<Image>(); image.color = new Color(.96f,.91f,.79f); image.raycastTarget = false;
            _heading = Label("Title",new Vector2(34,-48),new Vector2(492,76),28,TextAnchor.UpperLeft);
            Label("Letterhead",new Vector2(34,-22),new Vector2(492,24),14,TextAnchor.UpperLeft).text = "回 潮 海  /  航 海 来 信";
            _body = Label("Letter body",new Vector2(34,-160),new Vector2(492,310),24,TextAnchor.UpperLeft);
            _sender = Label("Signature",new Vector2(34,-495),new Vector2(492,64),19,TextAnchor.UpperRight);
            _hint = Label("Reading hint",new Vector2(34,-590),new Vector2(492,32),15,TextAnchor.MiddleCenter);
            _root.gameObject.SetActive(false);
        }
        private Text Label(string name, Vector2 position, Vector2 size, int font, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(_paper,false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<Text>(); text.font = FishingUIFont.Font; text.fontSize = font;
            text.color = new Color(.18f,.23f,.22f); text.alignment = alignment; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        private void Quad(string name, Transform parent, Vector3[] vertices, Color color)
        {
            var mesh = new Mesh { name = name }; mesh.vertices = vertices;
            var triangles = new List<int>();
            for (int i=1;i<vertices.Length-1;i++) { triangles.Add(0);triangles.Add(i+1);triangles.Add(i); triangles.Add(0);triangles.Add(i);triangles.Add(i+1); }
            mesh.SetTriangles(triangles,0); var colors = new Color[vertices.Length]; for(int i=0;i<colors.Length;i++)colors[i]=color;
            mesh.colors=colors; mesh.RecalculateNormals(); mesh.RecalculateBounds(); _owned.Add(mesh);
            var go = new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var material = FishingArt.Palette(_owned,_c.PaletteShader,"Stationery linen",0,0); material.SetFloat("_Unlit",1);
            go.GetComponent<MeshRenderer>().sharedMaterial=material; go.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        public void ShowIsland(int island) => Show(FishingAdventure.Name(island)+"的来信",FishingLore.ResidentName(island),
            "致新来的领航员：\n\n"+FishingAdventure.Clue(island)+"\n\n愿这盏灯重新连起回家的航路。\n海风会替我们记住你的到来。",_c.Islands.WorldRoot.GetChild(island).Find("Voyage letter"));
        internal void Show(string title, string sender, string body, Transform source=null)
        {
            if (Open || _c.Paused || _c.DialogueOpen || _c.HasPendingCatch) return;
            _c.XRNotebook.Close(); _c.StoryModalChanged();
            _heading.text=title; _sender.text="—— "+sender; _body.text=body;
            _sourceRenderers=source!=null?source.GetComponentsInChildren<Renderer>():null;
            if(_sourceRenderers!=null){_sourceEnabled=new bool[_sourceRenderers.Length];for(int i=0;i<_sourceRenderers.Length;i++){_sourceEnabled[i]=_sourceRenderers[i].enabled;_sourceRenderers[i].enabled=false;}}
            var eye=_c.viewCamera.transform;
            _root.SetPositionAndRotation(eye.position+eye.forward*1.2f-eye.up*.10f,eye.rotation);
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
            _envelope.localPosition=new Vector3(0,Mathf.Lerp(-.22f,0,rise)-extract*.32f,0);
            _envelope.localRotation=Quaternion.Euler(12*(1-rise),0,-5*(1-rise));
            _seal.localPosition=new Vector3(seal*.12f,-.055f-seal*.10f,-.04f-seal*.09f);
            _seal.localScale=new Vector3(.069f,.069f,.014f)*(1-seal);
            _flap.localRotation=Quaternion.Euler(-165*flap,0,0);
            _paper.localPosition=new Vector3(0,Mathf.Lerp(-.10f,.11f,extract),-.06f);
            _paper.localScale=new Vector3(.00105f,.00105f*Mathf.Lerp(.12f,1,extract),.00105f);
            _paperFade.alpha=Mathf.Clamp01(extract*2);
            _hint.text=XRSettings.isDeviceActive?"A 收起来信  /  B 关闭":"E / Esc 收起来信 · J 日志可重读";
        }
        private void OnDestroy() { foreach(var item in _owned)if(item!=null)Destroy(item); }
    }
}
