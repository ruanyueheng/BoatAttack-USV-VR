using System;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.Fishing
{
    /// <summary>Shared paper, ink, typography and depth-tested world UI for the coastal game.</summary>
    public static class FishingUITheme
    {
        private static readonly Color[] Day={new Color32(244,228,195,255),new Color32(255,247,230,255),new Color32(49,79,72,255),new Color32(82,104,93,255),new Color32(210,167,110,255),new Color32(109,159,137,255),new Color32(239,145,107,255),new Color32(224,234,211,255),new Color32(226,223,207,255)};
        private static readonly Color[] Night={new Color32(30,42,58,255),new Color32(41,56,74,255),new Color32(246,235,209,255),new Color32(182,198,206,255),new Color32(224,184,119,255),new Color32(137,195,177,255),new Color32(247,166,132,255),new Color32(61,85,99,255),new Color32(51,62,76,255)};
        public static float NightAmount { get; private set; }
        private static bool _initialized;
        public static Color Paper=>Tone(0);public static Color Light=>Tone(1);public static Color Ink=>Tone(2);public static Color Muted=>Tone(3);
        public static Color Gold=>Tone(4);public static Color Sage=>Tone(5);public static Color Coral=>Tone(6);public static Color Selected=>Tone(7);public static Color Disabled=>Tone(8);
        public static Color Tone(int role)=>Color.Lerp(Day[role],Night[role],NightAmount);
        public static Color Blend(Color day,Color night)=>Color.Lerp(day,night,NightAmount);
        public static Color MapWater=>Blend(new Color(.76f,.85f,.82f),new Color(.12f,.22f,.30f));
        public static Color MapLow=>Blend(new Color(.91f,.82f,.63f),new Color(.51f,.49f,.38f));
        public static Color MapLand=>Blend(new Color(.66f,.74f,.58f),new Color(.26f,.39f,.39f));
        public static Color MapHigh=>Blend(new Color(.77f,.75f,.68f),new Color(.47f,.52f,.56f));
        private static void MapColor(Material material,string name,Color color)=>material.SetColor(name,color);
        public static void StyleMap(Material material)
        {
            MapColor(material,"_Water",MapWater);MapColor(material,"_Low",MapLow);MapColor(material,"_Land",MapLand);MapColor(material,"_High",MapHigh);
            MapColor(material,"_Unknown",Paper);MapColor(material,"_Hatch",Blend(new Color(.75f,.72f,.64f),new Color(.22f,.29f,.36f)));
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
        public static Font Heading=>_heading!=null?_heading:(_heading=Resources.Load<Font>("FishingUIPrototype/TideHandSC"));
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
