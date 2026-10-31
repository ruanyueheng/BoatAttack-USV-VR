using System;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        private enum HomeState { Normal, Hover, Pressed, Selected, Disabled }
        private static Color HomePaper=>FishingUITheme.Paper;private static Color HomeInk=>FishingUITheme.Ink;private static Color HomeMuted=>FishingUITheme.Muted;
        private static Color HomeGold=>FishingUITheme.Gold;private static Color HomeSage=>FishingUITheme.Sage;private static Color HomeLight=>FishingUITheme.Light;
        private static readonly string[] HomeTitles={"随身行囊","海岸工坊","航海图","航海手札","港口委托","钓具百货","群岛航记","设置","快速回家"};
        private static readonly string[] HomeCaptions={"装备与物资","制作与锻造","海湾与航点","图鉴与日历","委托与鱼货","补给与升级"};
        private GameObject _homeDecor;
        private Sprite _homePaperSprite,_homeEdgeSprite;
        private Material _homeMaterial;
        private readonly Sprite[] _homeGlyphs=new Sprite[9];
        private readonly Image[] _homeEdges=new Image[9],_homeIcons=new Image[9];
        private readonly Text[] _homeCaptions=new Text[9],_homeArrows=new Text[9];
        private Text _homeTitle,_homeDate,_homeWeather,_homeWallet,_homeCapacity,_homeEquipment,_homeMessage;
        private CanvasScaler _homeScaler;
        private int _homeHover=-1,_homeSelected=-1;
        private bool _homeHeld,_homeVisible;
        private Image _spreadPaper;

        private static void HomeRect(RectTransform rt,Rect rect)
        {
            rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);
            rt.anchoredPosition=new Vector2(rect.x,-rect.y);rt.sizeDelta=rect.size;
        }

        private Image HomeImage(Transform parent,string name,Rect rect,Color color,Sprite sprite=null,bool themed=true)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.layer=5;go.transform.SetParent(parent,false);
            HomeRect(go.GetComponent<RectTransform>(),rect);
            var image=go.GetComponent<Image>();image.color=color;image.sprite=sprite;image.material=FishingUITheme.WorldMaterial;image.raycastTarget=false;
            if(themed)FishingUITheme.Bind(image,color);
            if(sprite!=null && sprite.border!=Vector4.zero)image.type=Image.Type.Sliced;
            return image;
        }

        private static Text HomeText(Transform parent,Rect rect,string value,int size,Color color,Font font=null,bool themed=true)
        {
            var text=Label(parent,new Vector2(rect.x,-rect.y),rect.size,size);
            text.text=value;text.color=color;text.font=font!=null?font:FishingUIFont.Font;
            if(font==FishingUITheme.Heading)text.lineSpacing=1;
            if(themed)FishingUITheme.Bind(text,color);
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
            return text;
        }

        private void BuildNotebookHome()
        {
            // The sea route uses queue 3100; retain UI/Default's world depth test and draw the notebook afterwards.
            _homeMaterial=FishingUITheme.WorldMaterial;
            // V044：手账纸与金线框换成绘本水彩纸 / 细金线四芒星框。
            _homePaperSprite=FishingUITheme.StorybookPaperSprite;
            _homeEdgeSprite=FishingUITheme.GiltFrameSprite;
            var atlas=Resources.Load<Texture2D>("FishingUIPrototype/NavigationAtlas");
            var titleFont=FishingUITheme.Heading;
            var art=Resources.Load<Texture2D>("FishingUIPrototype/IslandPostcard");
            if(atlas==null || titleFont==null || art==null)throw new InvalidOperationException("Notebook prototype art/font is incomplete.");
            if(atlas.width!=768 || atlas.height!=768)throw new InvalidOperationException("Notebook atlas requires its original 768px dimensions; disable power-of-two import scaling.");
            for(int i=0;i<9;i++)_homeGlyphs[i]=FishingUITheme.NavigationIcon(i);
            _homeScaler=_canvas.gameObject.AddComponent<CanvasScaler>();
            _homeScaler.dynamicPixelsPerUnit=4;
            _homeDecor=new GameObject("Handpainted notebook home",typeof(RectTransform));_homeDecor.layer=5;_homeDecor.transform.SetParent(_canvas.transform,false);
            var decor=_homeDecor.GetComponent<RectTransform>();decor.anchorMin=Vector2.zero;decor.anchorMax=Vector2.one;decor.offsetMin=decor.offsetMax=Vector2.zero;
            var p=_homeDecor.transform;
            HomeImage(p,"Cloth binding",new Rect(13,25,9,730),HomeSage,_homePaperSprite);
            HomeImage(p,"Quiet paper edge",new Rect(29,18,1051,758),HomeGold,_homeEdgeSprite);
            HomeImage(p,"Compass insignia",new Rect(43,39,67,67),Color.white,_homeGlyphs[2]);
            _homeTitle=HomeText(p,new Rect(127,28,410,74),"潮汐手册",48,HomeInk,titleFont,false);FishingUITheme.StyleHeading(_homeTitle);
            HomeText(p,new Rect(129,107,442,36),"回潮海  ·  航行与生活",22,HomeMuted);
            _homeDate=HomeText(p,new Rect(568,45,492,37),"",25,HomeInk);_homeDate.alignment=TextAnchor.MiddleRight;
            HomeImage(p,"Weather stamp",new Rect(779,97,283,38),FishingUITheme.Selected,_homePaperSprite);
            _homeWeather=HomeText(p,new Rect(791,100,259,32),"",21,HomeInk);_homeWeather.alignment=TextAnchor.MiddleCenter;
            HomeImage(p,"Header rule",new Rect(42,149,1020,1.5f),HomeGold);

            var postcard=HomeImage(p,"Island postcard",new Rect(42,171,367,452),HomeLight,_homePaperSprite);
            HomeImage(postcard.transform,"Postcard edge",new Rect(0,0,367,452),HomeGold,_homeEdgeSprite);
            var painting=new GameObject("Gouache bay illustration",typeof(RectTransform),typeof(RawImage));painting.layer=5;painting.transform.SetParent(postcard.transform,false);
            HomeRect(painting.GetComponent<RectTransform>(),new Rect(13,13,341,319));
            var photo=painting.GetComponent<RawImage>();photo.texture=art;photo.raycastTarget=false;photo.uvRect=new Rect(0,.03f,1,.935f);
            var tape=HomeImage(postcard.transform,"Small paper tape",new Rect(124,-6,114,20),new Color32(210,167,110,170),_homePaperSprite);tape.rectTransform.localRotation=Quaternion.Euler(0,0,-4);
            HomeText(postcard.transform,new Rect(20,346,331,56),"去海边，慢一点。",34,HomeInk,titleFont);
            HomeText(postcard.transform,new Rect(23,407,323,31),"装好行囊，再去遇见一片海。",21,HomeMuted);
            var pocket=HomeImage(p,"Travel pocket",new Rect(42,651,367,79),FishingUITheme.Selected,_homePaperSprite);
            HomeImage(pocket.transform,"Coin pictogram",new Rect(15,6,29,29),HomeGold,FishingUITheme.Coins);
            HomeImage(pocket.transform,"Fish box pictogram",new Rect(15,44,25,25),HomeSage,FishingUITheme.FishBox);
            _homeWallet=HomeText(pocket.transform,new Rect(55,4,294,38),"",25,HomeInk);
            _homeCapacity=HomeText(pocket.transform,new Rect(48,43,301,32),"",21,HomeMuted);
            _homeMessage=HomeText(p,new Rect(444,609,610,48),"",20,HomeMuted);
            HomeImage(p,"Footer reading strip",new Rect(35,733,1030,43),HomePaper,_homePaperSprite);
            _homeEquipment=HomeText(p,new Rect(44,741,457,32),"",20,HomeMuted);
            var hint=HomeText(p,new Rect(513,741,547,32),"右手指向  ·  扳机选择  ·  B 收起",20,HomeMuted);hint.alignment=TextAnchor.MiddleRight;
            HomeImage(p,"Footer rule",new Rect(42,733,1020,1),new Color32(210,167,110,170));

            for(int i=0;i<9;i++)
            {
                _homeEdges[i]=HomeImage(_plates[i].transform,"Handdrawn button edge",new Rect(0,0,1,1),HomeGold,_homeEdgeSprite,false);
                _homeIcons[i]=HomeImage(_plates[i].transform,"Navigation illustration",new Rect(0,0,1,1),Color.white,_homeGlyphs[i]);
                _homeArrows[i]=HomeText(_plates[i].transform,new Rect(0,0,1,1),"›",30,HomeGold,null,false);_homeArrows[i].alignment=TextAnchor.MiddleCenter;
                _homeCaptions[i]=HomeText(_plates[i].transform,new Rect(0,0,1,1),i<6?HomeCaptions[i]:"",21,HomeMuted,null,false);
            }
            _homeDecor.SetActive(false);
            BuildNotebookPages();
            var paper=HomeImage(_canvas.transform,"Windows torn parchment spread",new Rect(0,0,1100,780),FishingPaperDesign.PaperTint,FishingUITheme.TornParchmentSprite,false);
            _spreadPaper=paper;paper.transform.SetAsFirstSibling();
            var sea=HomeImage(_canvas.transform,"Windows starry sea surround",new Rect(-22,-22,1144,824),Color.white,FishingUITheme.StarrySeaSprite,false);
            sea.transform.SetAsFirstSibling();
            BuildDesktopSurfaces();
        }

        private static Rect HomeButtonRect(int i)
            =>i<6?new Rect(442+i%2*316,171+i/2*149,304,128)
            :i==6?new Rect(442,669,223,57):i==7?new Rect(684,669,155,57):new Rect(858,669,204,57);

        private void PresentNotebookHome()
        {
            bool home=_page==0;
            _spreadPaper.sprite=FishingPaperDesign.NotebookTheme(_page)==FishingPaperTheme.DreamSettings?FishingPaperDesign.Surface(FishingPaperTheme.DreamSettings):FishingUITheme.TornParchmentSprite;
            _spreadPaper.color=FishingPaperDesign.PaperTint;
            if(home!=_homeVisible)
            {
                _homeVisible=home;_homeHover=-1;_homeHeld=false;
                _homeDecor.SetActive(home);_status.gameObject.SetActive(!home);
                var rt=(RectTransform)_canvas.transform;
                rt.sizeDelta=home?new Vector2(1100,780):new Vector2(580,660);
                rt.localScale=Vector3.one*(home?.00105f:.0016f);
                _homeScaler.dynamicPixelsPerUnit=4;
                var panel=_canvas.GetComponent<Image>();panel.sprite=home?FishingPaperDesign.Surface(FishingPaperTheme.Celestial):null;
                panel.type=home?Image.Type.Sliced:Image.Type.Simple;panel.color=home?FishingPaperDesign.PaperTint:new Color(.025f,.048f,.082f,.97f);
                foreach(var graphic in _canvas.GetComponentsInChildren<Graphic>(true))graphic.material=graphic==_map && _mapPalette!=null?_mapPalette:home?_homeMaterial:null;
                for(int i=0;i<9;i++)
                {
                    _homeEdges[i].gameObject.SetActive(home);_homeIcons[i].gameObject.SetActive(home);_homeArrows[i].gameObject.SetActive(home);
                    _homeCaptions[i].gameObject.SetActive(home && i<6);
                    _plates[i].sprite=home?_homePaperSprite:null;_plates[i].type=home?Image.Type.Sliced:Image.Type.Simple;
                    if(!home)
                    {
                        HomeRect(_plates[i].rectTransform,new Rect(24+i%3*181,258+i/3*128,170,114));
                        var box=_plates[i].GetComponent<BoxCollider>();box.size=new Vector3(170,114,3);box.center=new Vector3(85,-57,0);
                        _labels[i].color=new Color(.90f,.95f,.92f);_labels[i].alignment=TextAnchor.UpperLeft;
                        _plates[i].color=_actions[i]==null?new Color(.09f,.13f,.14f):new Color(.12f,.27f,.25f);
                    }
                }
            }
            if(!home){PresentNotebookPage();return;}
            _pageDecor.SetActive(false);if(_albumPreview!=null)_albumPreview.gameObject.SetActive(false);
            var day=_c.Career.Calendar;var bag=_c.Career.Backpack;
            _homeDate.text=day.DateLabel+"  /  "+day.TimeLabel;
            FishingUITheme.StyleHeading(_homeTitle);
            _homeWeather.text=BayClimate.Name(_c.Weather.Climate.Current)+"  ·  "+day.TideName;
            _canvas.GetComponent<Image>().color=FishingPaperDesign.PaperTint;
            _homeWallet.text="金币    "+_c.Career.CoinDisplay;
            _homeCapacity.text="鱼箱 "+_c.Career.Count+"/"+_c.Career.Capacity+" · 行囊 "+bag.UsedSlots+"/36";
            _homeEquipment.text="当前装备  ·  "+FishingBackpack.Name(bag.Equipped);
            _homeMessage.text=_c.VisibleMessage;
            for(int i=0;i<9;i++)
            {
                _plates[i].gameObject.SetActive(true);
                var border=_plates[i].GetComponent<Outline>();if(border!=null)border.enabled=false;
                var r=HomeButtonRect(i);HomeRect(_plates[i].rectTransform,r);
                HomeRect(_homeEdges[i].rectTransform,new Rect(0,0,r.width,r.height));
                HomeRect(_homeIcons[i].rectTransform,i<6?new Rect(12,28,75,75):new Rect(10,13,32,32));
                _homeIcons[i].sprite=_homeGlyphs[i];_homeIcons[i].rectTransform.localScale=Vector3.one;
                var box=_plates[i].GetComponent<BoxCollider>();box.size=new Vector3(r.width,r.height,3);box.center=new Vector3(r.width*.5f,-r.height*.5f,0);
                var label=_labels[i];label.text=HomeTitles[i];label.font=i<6?FishingUITheme.Heading:FishingUIFont.Font;label.lineSpacing=i<6?1:1.12f;label.fontSize=i<6?29:23;label.alignment=TextAnchor.MiddleLeft;
                HomeRect(label.rectTransform,i<6?new Rect(96,23,176,44):new Rect(49,9,r.width-70,39));
                HomeRect(_homeArrows[i].rectTransform,new Rect(r.width-37,i<6?35:3,30,50));
                if(i<6){_homeCaptions[i].text=HomeCaptions[i];HomeRect(_homeCaptions[i].rectTransform,new Rect(97,76,179,32));}
                ApplyHomeState(i,HomeButtonState(i),true);
            }
        }

        private HomeState HomeButtonState(int i)
            =>_actions[i]==null?HomeState.Disabled:i==_homeHover?(_homeHeld?HomeState.Pressed:HomeState.Hover):i==_homeSelected?HomeState.Selected:HomeState.Normal;

        private void ApplyHomeState(int i,HomeState state,bool instant)
        {
            bool disabled=state==HomeState.Disabled,selected=state==HomeState.Selected,pressed=state==HomeState.Pressed,hover=state==HomeState.Hover;
            Color fill=disabled?FishingUITheme.Disabled:i==8?HomeInk:pressed||selected?FishingUITheme.Selected:hover?FishingUITheme.Blend(new Color32(255,253,242,255),new Color32(54,75,93,255)):HomeLight;
            Color edge=disabled?HomeMuted:pressed||selected?HomeInk:hover?HomeSage:HomeGold;
            float mix=instant?1:1-Mathf.Exp(-Time.unscaledDeltaTime/ .08f);
            _plates[i].color=Color.Lerp(_plates[i].color,fill,mix);_homeEdges[i].color=Color.Lerp(_homeEdges[i].color,edge,mix);
            _labels[i].color=i==8&&!disabled?HomeLight:HomeInk;
            if(i<6)_homeCaptions[i].color=disabled?HomeInk:HomeMuted;
            _homeIcons[i].color=disabled?new Color(1,1,1,.45f):i==8?HomeLight:Color.white;
            _homeArrows[i].text=disabled?"–":selected?"✓":"›";
            _homeArrows[i].color=i==8&&!disabled?HomeLight:pressed||selected||hover||disabled?HomeInk:HomeGold;
            _homeEdges[i].rectTransform.localScale=Vector3.one*(pressed?.989f:1);
        }

        private void UpdateHomePointer(int index,bool held)
        {
            if(UpdateDesktopPointer(index))return;
            if(_page!=0){UpdatePagePointer(index,held);return;}
            _homeHover=index;_homeHeld=held;
            for(int i=0;i<9;i++)ApplyHomeState(i,HomeButtonState(i),false);
        }

    }
}
