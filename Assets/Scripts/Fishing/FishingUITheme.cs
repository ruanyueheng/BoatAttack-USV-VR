using System;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.Fishing
{
    /// <summary>Shared paper, ink, typography and depth-tested world UI for the coastal game.</summary>
    public static class FishingUITheme
    {
        public static readonly Color Paper=new Color32(244,228,195,255),Light=new Color32(255,247,230,255);
        public static readonly Color Ink=new Color32(49,79,72,255),Muted=new Color32(82,104,93,255);
        public static readonly Color Gold=new Color32(210,167,110,255),Sage=new Color32(109,159,137,255),Coral=new Color32(239,145,107,255);
        public static readonly Color Selected=new Color32(224,234,211,255),Disabled=new Color32(226,223,207,255);
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
