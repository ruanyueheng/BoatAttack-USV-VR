using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        private readonly string[] _pageContent=new string[9];
        private GameObject _pageDecor;
        private Text _pageTitle,_pageDate,_pageDetail;
        private Image _pageLeaf,_pageLeafEdge;
        private int _pageHover=-1;
        private static readonly string[] PageTitles={"潮汐手册","随身行囊","海岸工坊","自然日历","钓具百货","探索补给","群岛来信","港口事务","海岛居民","群岛航记","海湾图鉴","手柄教学","航海图","鱼箱与委托","海湾设置","自然观察","航行帮助","海边烧烤","储物箱 · 存入","储物箱 · 取出","声音混音","家园生态","VR 舒适","操作说明","信件"};

        private void BuildNotebookPages()
        {
            _pageDecor=new GameObject("Coastal paper feature pages",typeof(RectTransform));_pageDecor.layer=5;_pageDecor.transform.SetParent(_canvas.transform,false);
            var rect=_pageDecor.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            _pageDecor.transform.SetAsFirstSibling();
            var p=_pageDecor.transform;
            HomeImage(p,"Cloth spine",new Rect(13,25,9,730),HomeSage,_homePaperSprite);
            HomeImage(p,"Paper perimeter",new Rect(29,18,1051,758),HomeGold,_homeEdgeSprite);
            _pageTitle=HomeText(p,new Rect(52,33,600,70),"",42,HomeInk,FishingUITheme.Heading);
            HomeText(p,new Rect(55,106,620,33),"潮汐手册  /  回潮海",21,HomeMuted);
            _pageDate=HomeText(p,new Rect(651,49,407,40),"",23,HomeInk);_pageDate.alignment=TextAnchor.MiddleRight;
            HomeImage(p,"Header rule",new Rect(42,149,1020,1.5f),HomeGold);
            _pageLeaf=HomeImage(p,"Left journal leaf",new Rect(42,171,375,555),HomeLight,_homePaperSprite);
            _pageLeafEdge=HomeImage(p,"Left leaf edge",new Rect(42,171,375,555),HomeGold,_homeEdgeSprite);
            _pageDetail=HomeText(p,new Rect(62,191,335,471),"",22,HomeInk);
            HomeText(p,new Rect(62,680,335,34),"指向卡片，查看完整信息",20,HomeMuted);
            HomeImage(p,"Footer reading strip",new Rect(35,733,1030,43),HomePaper,_homePaperSprite);
            HomeImage(p,"Footer rule",new Rect(42,733,1020,1),HomeGold);
            var hint=HomeText(p,new Rect(44,741,1018,32),"右手指向  ·  扳机选择  ·  B 返回海湾",20,HomeMuted);hint.alignment=TextAnchor.MiddleRight;
            _pageDecor.SetActive(false);
        }

        private void PresentNotebookPage()
        {
            _pageDecor.SetActive(true);
            var root=(RectTransform)_canvas.transform;root.sizeDelta=new Vector2(1100,780);root.localScale=Vector3.one*.00105f;
            _homeScaler.dynamicPixelsPerUnit=3;
            var theme=FishingPaperDesign.NotebookTheme(_page);
            var panel=_canvas.GetComponent<Image>();panel.sprite=FishingPaperDesign.Surface(theme);panel.type=Image.Type.Sliced;panel.color=FishingPaperDesign.PaperTint;
            _pageTitle.font=theme==FishingPaperTheme.Workshop?FishingUIFont.Font:FishingUITheme.Heading;
            _pageTitle.fontSize=theme==FishingPaperTheme.Workshop?38:45;
            _pageTitle.text=_page>=0 && _page<PageTitles.Length?PageTitles[_page]:"潮汐手册";
            _pageDate.text=_c.Career.Calendar.DateLabel+"  /  "+_c.Career.Calendar.TimeLabel;
            bool chart=_page==12;
            HomeRect(_pageLeaf.rectTransform,new Rect(42,171,chart?620:375,555));HomeRect(_pageLeafEdge.rectTransform,new Rect(42,171,chart?620:375,555));
            HomeRect(_status.rectTransform,new Rect(62,191,chart?580:335,chart?120:471));_status.fontSize=22;_status.color=HomeInk;
            if(_page==8){while(_status.fontSize>20 && _status.preferredHeight>_status.rectTransform.rect.height)_status.fontSize--;}
            if(_page==12 && _map.texture!=null)
            {
                float ratio=_map.texture.width/(float)_map.texture.height,width=Mathf.Min(580,305*ratio);
                HomeRect(_map.rectTransform,new Rect(62+(580-width)*.5f,328,width,width/ratio));
                foreach(var marker in new[]{_mapPlayer,_mapTarget}){marker.sizeDelta=new Vector2(40,40);marker.GetComponent<Text>().fontSize=21;}
                PlaceMapMarker(_mapPlayer,_c.ExplorerPosition);PlaceMapMarker(_mapTarget,_c.NavigationPosition);
            }
            for(int i=0;i<9;i++)
            {
                var r=i<6?new Rect(442+i%2*316,171+i/2*157,304,144)
                    :i==6?new Rect(442,669,194,57):i==7?new Rect(652,669,220,57):new Rect(888,669,174,57);
                if(chart && i<6)r=new Rect(686,171+i*79,376,68);
                var plate=_plates[i];HomeRect(plate.rectTransform,r);plate.sprite=_homePaperSprite;plate.type=Image.Type.Sliced;plate.material=_homeMaterial;
                plate.gameObject.SetActive(_pageContent[i]!="—");
                var box=plate.GetComponent<BoxCollider>();box.size=new Vector3(r.width,r.height,3);box.center=new Vector3(r.width*.5f,-r.height*.5f,0);
                _homeEdges[i].gameObject.SetActive(true);HomeRect(_homeEdges[i].rectTransform,new Rect(0,0,r.width,r.height));
                _homeArrows[i].gameObject.SetActive(i<6);HomeRect(_homeArrows[i].rectTransform,new Rect(r.width-34,8,28,48));
                bool photo=i<6 && _icons[i].gameObject.activeSelf;
                _homeIcons[i].gameObject.SetActive(i>=6);
                if(i>=6)
                {
                    bool previous=_pageContent[i]!=null && _pageContent[i].StartsWith("上一");
                    bool home=_pageContent[i]!=null && _pageContent[i].Contains("首页");
                    HomeRect(_homeIcons[i].rectTransform,new Rect(previous?40:9,12,31,31));
                    _homeIcons[i].rectTransform.localScale=new Vector3(previous?-1:1,1,1);_homeIcons[i].sprite=_homeGlyphs[home?3:8];
                }
                if(photo)HomeRect(_icons[i].rectTransform,new Rect(13,14,63,63));
                var content=(_pageContent[i]??"").Split(new[]{'\n'},2);
                int limit=i>=6?Mathf.FloorToInt((r.width-61)/20):chart?13:photo?7:10;
                string title=content[0];if(title.Length>limit)title=title.Substring(0,limit-1)+"…";
                _labels[i].text=title;_labels[i].fontSize=i<6?24:20;_labels[i].color=HomeInk;_labels[i].alignment=TextAnchor.MiddleLeft;
                HomeRect(_labels[i].rectTransform,i<6?new Rect(photo?87:16,chart?12:8,photo?181:chart?316:253,41):new Rect(46,7,r.width-59,43));
                _homeCaptions[i].gameObject.SetActive(i<6 && !chart);
                if(i<6)
                {
                    string body=content.Length>1?content[1]:"";
                    int max=photo?24:38;if(body.Length>max)body=body.Substring(0,max-1)+"…";
                    _homeCaptions[i].text=body;_homeCaptions[i].fontSize=21;_homeCaptions[i].color=HomeMuted;
                    HomeRect(_homeCaptions[i].rectTransform,new Rect(16,photo?82:48,272,photo?61:92));
                }
                ApplyPageState(i,false,true);
            }
            foreach(var graphic in _canvas.GetComponentsInChildren<Graphic>(true))graphic.material=graphic==_map && _mapPalette!=null?_mapPalette:_homeMaterial;
            UpdatePagePointer(_pageHover,false);
        }

        private void ApplyPageState(int i,bool held,bool instant)
        {
            bool disabled=_actions[i]==null,hover=i==_pageHover;
            Color surface=disabled?FishingUITheme.Disabled:hover?FishingUITheme.Selected:HomeLight;
            if(_page==12 && _pageContent[i]!=null && _pageContent[i].StartsWith("★"))surface=FishingUITheme.Selected;
            float blend=instant?1:1-Mathf.Exp(-Time.unscaledDeltaTime/.08f);
            _plates[i].color=Color.Lerp(_plates[i].color,surface,blend);
            _homeEdges[i].color=disabled?HomeMuted:hover?HomeSage:HomeGold;
            _labels[i].color=HomeInk;_homeCaptions[i].color=HomeMuted;_homeIcons[i].color=HomeInk;
            _homeArrows[i].text=disabled?"–":"›";_homeArrows[i].color=disabled?HomeMuted:HomeInk;
        }

        private void UpdatePagePointer(int index,bool held)
        {
            _pageHover=index;
            bool detail=index>=0 && index<9 && !string.IsNullOrEmpty(_pageContent[index]) && _pageContent[index]!="—";
            _pageDetail.gameObject.SetActive(detail);_status.gameObject.SetActive(!detail);
            if(detail){_pageDetail.text=_pageContent[index];HomeRect(_pageDetail.rectTransform,new Rect(62,191,_page==12?580:335,_page==12?120:471));}
            for(int i=0;i<9;i++)ApplyPageState(i,held,false);
        }
    }
}
