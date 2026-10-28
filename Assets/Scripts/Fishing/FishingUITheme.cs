using System;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.Fishing
{
    /// <summary>Shared paper, ink, typography and depth-tested world UI for the coastal game.</summary>
    public static class FishingUITheme
    {
        private static readonly Color[] Day={new Color32(246,236,213,255),new Color32(255,250,238,255),new Color32(38,58,72,255),new Color32(84,100,104,255),new Color32(196,150,82,255),new Color32(109,159,137,255),new Color32(239,145,107,255),new Color32(224,234,211,255),new Color32(226,223,207,255)};
        private static readonly Color[] Night={new Color32(22,34,54,255),new Color32(32,47,70,255),new Color32(247,238,214,255),new Color32(184,198,214,255),new Color32(236,200,130,255),new Color32(137,195,177,255),new Color32(247,166,132,255),new Color32(61,85,99,255),new Color32(51,62,76,255)};
        private static readonly Color[] DayTitles={new Color32(30,75,79,255),new Color32(43,60,105,255),new Color32(31,76,49,255),new Color32(104,43,54,255),new Color32(70,42,99,255)};
        private static readonly Color[] NightTitles={new Color32(178,229,218,255),new Color32(188,213,250,255),new Color32(174,227,185,255),new Color32(246,186,176,255),new Color32(220,199,247,255)};
        public static float NightAmount { get; private set; }
        private static bool _initialized;
        public static Color Paper=>Tone(0);public static Color Light=>Tone(1);public static Color Ink=>Tone(2);public static Color Muted=>Tone(3);
        public static Color Gold=>Tone(4);public static Color Sage=>Tone(5);public static Color Coral=>Tone(6);public static Color Selected=>Tone(7);public static Color Disabled=>Tone(8);
        public static Color Tone(int role)=>Color.Lerp(Day[role],Night[role],NightAmount);
        public static Color Blend(Color day,Color night)=>Color.Lerp(day,night,NightAmount);
        public static Color TitleFill(int style){style=Mathf.Clamp(style,0,DayTitles.Length-1);return Blend(DayTitles[style],NightTitles[style]);}
        public static Color MapWater=>Blend(new Color(.76f,.85f,.82f),new Color(.12f,.22f,.30f));
        public static Color MapLow=>Blend(new Color(.91f,.82f,.63f),new Color(.51f,.49f,.38f));
        public static Color MapLand=>Blend(new Color(.66f,.74f,.58f),new Color(.26f,.39f,.39f));
        public static Color MapHigh=>Blend(new Color(.77f,.75f,.68f),new Color(.47f,.52f,.56f));
        private static void MapColor(Material material,string name,Color color)=>material.SetColor(name,color);
        public static void StyleMap(Material material)
        {
            MapColor(material,"_Water",MapWater);MapColor(material,"_Low",MapLow);MapColor(material,"_Land",MapLand);MapColor(material,"_High",MapHigh);
            // V044：未测绘处透明，露出下层海雾剪影（大图）或纸面（小图）。
            var unknown=Paper;unknown.a=0;MapColor(material,"_Unknown",unknown);MapColor(material,"_Hatch",Blend(new Color(.75f,.72f,.64f,0),new Color(.22f,.29f,.36f,0)));
            MapColor(material,"_SourceWater",new Color(.10f,.24f,.28f));
            // The existing bake stores linear terrain swatches while its water clear is encoded as sRGB.
            MapColor(material,"_SourceLow",new Color(.73f,.69f,.54f).linear);
            MapColor(material,"_SourceLand",new Color(.35f,.46f,.36f).linear);MapColor(material,"_SourceHigh",new Color(.66f,.68f,.66f).linear);
        }
        public static float AdvanceNight(float current,float daylight,float seconds)=>Mathf.Lerp(current,daylight<.5f?1:0,1-Mathf.Exp(-Mathf.Max(0,seconds)/.38f));
        public static void Tick(float daylight,float seconds)
        {
            NightAmount=_initialized?AdvanceNight(NightAmount,daylight,seconds):daylight<.5f?1:0;_initialized=true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){NightAmount=0;_initialized=false;}
        public static void Bind(Graphic graphic,int role)
        {
            var canvas=graphic.GetComponentInParent<Canvas>(true);
            var binding=canvas.GetComponent<FishingUIThemeBindings>() ?? canvas.gameObject.AddComponent<FishingUIThemeBindings>();
            binding.Add(graphic,role);graphic.color=Tone(role);
        }
        public static void Bind(Graphic graphic,Color color)
        {
            for(int i=0;i<Day.Length;i++)if(color==Tone(i)){Bind(graphic,i);return;}
            graphic.color=color;
        }
        private static Sprite _coins,_fishBox;
        public static Sprite Coins=>_coins!=null?_coins:(_coins=Icon("Coins"));
        public static Sprite FishBox=>_fishBox!=null?_fishBox:(_fishBox=Icon("FishBox"));
        private static Sprite Icon(string name)
        {
            var texture=Resources.Load<Texture2D>("FishingUIPrototype/"+name);
            if(texture==null)throw new InvalidOperationException("Missing coastal HUD icon: "+name);
            return Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f));
        }
        private static Font _heading;
        private static Material _material;
        private static Sprite _paper,_edge;
        private static readonly Sprite[] Glyphs=new Sprite[9];
        // V044 storybook display face: Noto Serif SC SemiBold subset (OFL, see FishingV044/Fonts/NotoSerifSC-OFL.txt).
        public static Font Heading=>_heading!=null?_heading:(_heading=Resources.Load<Font>("FishingV044/Fonts/NotoSerifSC-SemiBold-Subset"));
        private static Texture2D _panelPaper,_panelVeil,_star,_rule,_slot,_titleGlow;
        /// <summary>V044 9-slice surfaces (40px border): cream watercolour paper for reading, night-sky veil for in-world HUD.</summary>
        public static Texture2D PanelPaper=>Storybook(ref _panelPaper,"PanelPaper");
        public static Texture2D PanelVeil=>Storybook(ref _panelVeil,"PanelVeil");
        public static Texture2D Star=>Storybook(ref _star,"Star");
        public static Texture2D GiltRule=>Storybook(ref _rule,"GiltRule");
        public static Texture2D Slot=>Storybook(ref _slot,"Slot");
        public static Texture2D TitleGlow=>Storybook(ref _titleGlow,"TitleGlow");
        private static Texture2D _starrySea,_parchment,_seaFog,_compass;
        /// <summary>Map/journal spread: starry night-sea backdrop, torn parchment (80px 9-slice), sea-fog veil for the unexplored, gold compass ring for selection.</summary>
        public static Texture2D StarrySea=>Storybook(ref _starrySea,"StarrySea");
        public static Texture2D TornParchment=>Storybook(ref _parchment,"TornParchment");
        public static Texture2D SeaFog=>Storybook(ref _seaFog,"SeaFog");
        public static Texture2D CompassRing=>Storybook(ref _compass,"CompassRing");
        private static Sprite _plainSprite,_giltFrameSprite;
        /// <summary>XR/world uGUI surfaces in the V044 language: tintable watercolour paper and a gilt hairline frame with corner stars (40px 9-slice).</summary>
        public static Sprite StorybookPaperSprite=>_plainSprite!=null?_plainSprite:(_plainSprite=Sliced("PanelPlain"));
        public static Sprite GiltFrameSprite=>_giltFrameSprite!=null?_giltFrameSprite:(_giltFrameSprite=Sliced("GiltFrame"));
        private static Sprite Sliced(string name)
        {
            Texture2D texture=null;texture=Storybook(ref texture,name);
            return Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(PanelBorder,PanelBorder,PanelBorder,PanelBorder));
        }
        private static readonly Sprite[] ControllerGlyphs=new Sprite[6];
        private static Texture2D _controllerAtlas;
        /// <summary>PICO controller line glyph: A, B, X, Y, Trigger, Grip (white, tint via Image.color).</summary>
        public static Sprite ControllerGlyph(string key)
        {
            int index=Array.IndexOf(new[]{"A","B","X","Y","Trigger","Grip"},key);
            if(index<0)throw new ArgumentOutOfRangeException(nameof(key));
            if(ControllerGlyphs[index]!=null)return ControllerGlyphs[index];
            var atlas=Storybook(ref _controllerAtlas,"ControllerGlyphs");
            return ControllerGlyphs[index]=Sprite.Create(atlas,new Rect(index*128,0,128,128),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
        }
        /// <summary>World-space hint row: controller glyph + short label pairs laid out left to right.</summary>
        public static void HintRow(Transform parent,Vector2 position,float height,int fontSize,params (string key,string text)[] hints)
        {
            float x=position.x;
            foreach(var (key,text) in hints)
            {
                var icon=new GameObject("Controller glyph "+key,typeof(RectTransform),typeof(Image));icon.layer=5;icon.transform.SetParent(parent,false);
                var irt=icon.GetComponent<RectTransform>();irt.anchorMin=irt.anchorMax=irt.pivot=new Vector2(0,1);irt.anchoredPosition=new Vector2(x,position.y);irt.sizeDelta=new Vector2(height,height);
                var image=icon.GetComponent<Image>();image.sprite=ControllerGlyph(key);image.material=WorldMaterial;image.raycastTarget=false;Bind(image,4);
                x+=height+6;
                var label=new GameObject("Controller hint "+key,typeof(RectTransform),typeof(Text));label.layer=5;label.transform.SetParent(parent,false);
                var t=label.GetComponent<Text>();t.font=FishingUIFont.Font;t.fontSize=fontSize;t.text=text;t.alignment=TextAnchor.MiddleLeft;t.material=WorldMaterial;t.raycastTarget=false;
                t.horizontalOverflow=HorizontalWrapMode.Overflow;Bind(t,2);
                var lrt=label.GetComponent<RectTransform>();lrt.anchorMin=lrt.anchorMax=lrt.pivot=new Vector2(0,1);lrt.anchoredPosition=new Vector2(x,position.y);lrt.sizeDelta=new Vector2(t.preferredWidth+2,height);
                x+=t.preferredWidth+28;
            }
        }
        public const int PanelBorder=40;
        private static Texture2D Storybook(ref Texture2D texture,string name)
        {
            if(texture!=null)return texture;
            texture=Resources.Load<Texture2D>("FishingV044/UI/"+name);
            if(texture==null)throw new InvalidOperationException("Missing V044 storybook UI texture: "+name);
            return texture;
        }
        public static Material WorldMaterial=>_material!=null?_material:(_material=new Material(Graphic.defaultGraphicMaterial){name="Coastal paper after water guidance",renderQueue=3200});
        public static Sprite PaperSprite=>_paper!=null?_paper:(_paper=Surface("Paper"));
        public static Sprite EdgeSprite=>_edge!=null?_edge:(_edge=Surface("InkEdge"));
        private static Sprite Surface(string name)
        {
            var texture=Resources.Load<Texture2D>("FishingUIPrototype/"+name);
            if(texture==null)throw new InvalidOperationException("Missing coastal UI surface: "+name);
            return Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(24,24,24,24));
        }
        public static Sprite NavigationIcon(int index)
        {
            if(index<0 || index>=Glyphs.Length)throw new ArgumentOutOfRangeException(nameof(index));
            if(Glyphs[index]!=null)return Glyphs[index];
            var atlas=Resources.Load<Texture2D>("FishingUIPrototype/NavigationAtlas");
            if(atlas==null || atlas.width!=768 || atlas.height!=768)throw new InvalidOperationException("Coastal navigation atlas must retain its 768px import size.");
            return Glyphs[index]=Sprite.Create(atlas,new Rect(index%3*256,(2-index/3)*256,256,256),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
        }
    }
}
