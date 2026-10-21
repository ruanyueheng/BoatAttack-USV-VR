using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.Fishing
{
    public enum FishingPaperTheme { Letter, Celestial, Herbarium, Workshop, DreamSettings }
    /// <summary>Shared original stationery surfaces; decoration is cached and never intercepts input.</summary>
    public static class FishingPaperDesign
    {
        private static Sprite _vellum,_corner,_crest,_bookmark,_seal;
        private static readonly Sprite[] Themed=new Sprite[4];
        public static Sprite Surface(FishingPaperTheme theme)
        {
            if(theme==FishingPaperTheme.Letter)return Vellum;
            int index=(int)theme-1;
            if(Themed[index]!=null)return Themed[index];
            var texture=Resources.Load<Texture2D>("FishingV037/"+theme);
            if(texture==null)throw new System.InvalidOperationException("Missing themed paper: "+theme);
            float edge=Mathf.Min(texture.width,texture.height)*.2f;
            return Themed[index]=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,
                new Vector4(edge,edge,edge,edge));
        }
        public static FishingPaperTheme NotebookTheme(int page)
            =>page==3 || page==10 || page==15?FishingPaperTheme.Herbarium
            :page==0 || page==6 || page==9 || page==12?FishingPaperTheme.Celestial:FishingPaperTheme.Workshop;
        public static Sprite Vellum=>Load(ref _vellum,"Vellum",new Vector4(32,32,32,32));
        public static Sprite Corner=>Load(ref _corner,"SeaLaurelCorner");
        public static Sprite Crest=>Load(ref _crest,"TidalCrest");
        public static Sprite Bookmark=>Load(ref _bookmark,"SageBookmark");
        public static Sprite Seal=>Load(ref _seal,"TidalWaxSeal");
        public static Color PaperTint=>FishingUITheme.Blend(Color.white,new Color(.15f,.22f,.30f));
        public static Color SettingsTint=>FishingUITheme.Blend(Color.white,new Color(.25f,.22f,.28f));
        private static Sprite Load(ref Sprite sprite,string name,Vector4 border=default)
        {
            if(sprite!=null)return sprite;
            var texture=Resources.Load<Texture2D>("FishingV036/Stationery/"+name);
            if(texture==null)throw new System.InvalidOperationException("Missing V036 stationery: "+name);
            return sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,border);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            for(int i=0;i<Themed.Length;i++){if(Themed[i]!=null)Object.Destroy(Themed[i]);Themed[i]=null;}
            foreach(var sprite in new[]{_vellum,_corner,_crest,_bookmark,_seal})if(sprite!=null)Object.Destroy(sprite);
            _vellum=_corner=_crest=_bookmark=_seal=null;
        }
        public static Image Image(Transform parent,string name,Rect rect,Vector2 page,Sprite sprite,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.layer=5;go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);
            rt.anchoredPosition=new Vector2(rect.center.x-page.x*.5f,page.y*.5f-rect.center.y);rt.sizeDelta=rect.size;
            var image=go.GetComponent<Image>();image.sprite=sprite;image.color=color;image.raycastTarget=false;
            image.material=FishingUITheme.WorldMaterial;return image;
        }
        public static void Frame(Transform parent,Vector2 size,float corner=68,bool ribbon=false)
        {
            var gold=new Color(.66f,.54f,.28f,.66f);
            foreach(float inset in new[]{15f,20f})
            {
                Image(parent,"Fine tide gold top",new Rect(inset+corner*.62f,inset,size.x-2*inset-corner*1.24f,1),size,null,gold);
                Image(parent,"Fine tide gold bottom",new Rect(inset+corner*.62f,size.y-inset,size.x-2*inset-corner*1.24f,1),size,null,gold);
                Image(parent,"Fine tide gold left",new Rect(inset,inset+corner*.62f,1,size.y-2*inset-corner*1.24f),size,null,gold);
                Image(parent,"Fine tide gold right",new Rect(size.x-inset,inset+corner*.62f,1,size.y-2*inset-corner*1.24f),size,null,gold);
            }
            for(int i=0;i<4;i++)
            {
                bool right=i%2==1,bottom=i>1;
                var leaf=Image(parent,"Sea laurel corner "+i,new Rect(right?size.x-corner:0,bottom?size.y-corner:0,corner,corner),size,Corner,Color.white);
                leaf.rectTransform.localScale=new Vector3(right?-1:1,bottom?-1:1,1);
            }
            if(ribbon)Image(parent,"Sage page ribbon",new Rect(size.x-112,-4,58,116),size,Bookmark,Color.white);
        }
    }
}
