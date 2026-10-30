using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        private GameObject _desktopJournal,_desktopBag,_desktopDialogue;
        private Text _dialogueName,_dialogueRole,_dialogueSpeech;
        private RawImage _dialoguePortrait;
        private Image _journalPaper,_specimenPanel,_specimenBookmark;
        private Text _journalHeading,_journalChapter,_journalNumber,_catalogCount,_specimenName,_specimenHabitat,_specimenRange,_specimenRecord,_specimenDiscovery,_specimenRare,_specimenQuestion;
        private RawImage _specimenBanner,_specimenPhoto;
        private readonly Image[] _journalNav=new Image[9];
        private readonly Text[] _journalNavLabels=new Text[9],_specimenNumbers=new Text[6];
        private readonly Image[] _bagSlots=new Image[36];
        private readonly Image[] _bagEdges=new Image[36],_desktopCardEdges=new Image[9];
        private readonly RawImage[] _bagPictures=new RawImage[36];
        private readonly Text[] _bagNumbers=new Text[36],_bagCounts=new Text[36],_bagNotes=new Text[3];
        private Text _bagSummary,_bagHint,_bagDiscardLabel;
        private Image _legendPaper;
        private RawImage _legendArt;
        private Text _legendHeading,_legendStory,_legendRecord;
        private Image _bagDiscard,_bagInspect,_bagEquip;
        private Image _allFilter,_knownFilter;
        private int _desktopHover=-1,_bagSelection=-1,_desktopDetailPage=-1,_desktopSelection;
        private static readonly string[] DesktopTabs={"鱼获图鉴","传说线索","自然观察","海岸调查","海湾日历","群岛航记","世界与秘境","相册图鉴","信件"};
        private static readonly int[] DesktopPages={10,29,15,33,3,6,32,25,24};
        private static readonly string[] DesktopChapters={"海的收藏","夕照中的金帆","与万物相遇","沿着潮线走","听潮与候风","寄自远方的信","找回四段回响","镜头里的回潮海","漂流瓶与回信"};
        private bool DesktopJournalPage=>_page==10 || _page==29 || _page==15 || _page==33 || _page==3 || _page==6 || _page==9 || _page==32 || _page==25 || _page==26 || _page==24 || _page==31;
        private static Color DesktopRule=>FishingUITheme.Blend(new Color(.65f,.48f,.29f,.38f),new Color(.62f,.71f,.77f,.38f));

        private Text DesktopText(Transform parent,string name,Rect rect,string text,int size,Color color,bool heading=false)
        {
            var label=HomeText(parent,rect,text,size,color,heading?FishingUITheme.Heading:null,false);
            label.name=name;label.lineSpacing=1.05f;
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(rect.height,Mathf.Ceil(label.preferredHeight)+2));return label;
        }
        private Image DesktopButton(Transform parent,string name,int index,Rect rect,string text,Action command,int size=22)
        {
            var image=HomeImage(parent,name,rect,FishingUITheme.Light,FishingUITheme.RoundedSurface,false);
            var label=DesktopText(image.transform,name+" label",new Rect(12,5,rect.width-24,rect.height-10),text,size,FishingUITheme.Ink);label.alignment=TextAnchor.MiddleLeft;
            var collider=image.gameObject.AddComponent<BoxCollider>();collider.isTrigger=true;collider.size=new Vector3(rect.width,rect.height,3);collider.center=new Vector3(rect.width*.5f,-rect.height*.5f,0);
            var button=image.gameObject.AddComponent<FishingNotebookButton>();button.Owner=this;button.Index=index;button.Command=command;
            return image;
        }
        private RawImage DesktopPicture(Transform parent,string name,Rect rect)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(RawImage));go.layer=5;go.transform.SetParent(parent,false);
            var image=go.GetComponent<RawImage>();image.material=FishingUITheme.WorldMaterial;image.raycastTarget=false;HomeRect(image.rectTransform,rect);return image;
        }
        private void BuildDesktopSurfaces()
        {
            _desktopJournal=new GameObject("Windows journal layout",typeof(RectTransform));_desktopJournal.layer=5;_desktopJournal.transform.SetParent(_canvas.transform,false);_desktopJournal.transform.SetAsFirstSibling();
            HomeRect((RectTransform)_desktopJournal.transform,new Rect(0,0,1456,756));
            HomeImage(_desktopJournal.transform,"Original starry surround",new Rect(-12,-12,1480,780),Color.white,FishingUITheme.StarrySeaSprite,false);
            HomeImage(_desktopJournal.transform,"Windows dark directory",new Rect(0,0,254,756),FishingHUD.SeaInk,null,false);
            _journalPaper=HomeImage(_desktopJournal.transform,"Windows journal paper",new Rect(254,0,1202,756),FishingPaperDesign.PaperTint,FishingPaperDesign.Surface(FishingPaperTheme.Herbarium),false);
            DesktopText(_desktopJournal.transform,"Journal imprint",new Rect(27,29,205,26),"T I D E B O U N D",16,FishingHUD.AmberInk);
            DesktopText(_desktopJournal.transform,"Journal book title",new Rect(26,64,220,54),"航海手札",34,FishingHUD.CreamInk,true);
            HomeImage(_desktopJournal.transform,"Directory rule",new Rect(28,126,192,1),new Color(.86f,.72f,.48f,.4f),null,false);
            for(int i=0;i<9;i++)
            {
                int page=DesktopPages[i];var nav=_journalNav[i]=DesktopButton(_desktopJournal.transform,"Windows journal tab "+i,200+i,new Rect(14,146+i*52,226,48),DesktopTabs[i],()=>Open(page),22);
                _journalNavLabels[i]=nav.GetComponentInChildren<Text>();HomeRect(_journalNavLabels[i].rectTransform,new Rect(58,7,164,36));
                if(i==0){var fish=DesktopPicture(nav.transform,"Fish directory glyph",new Rect(12,9,34,30));fish.texture=Resources.Load<Texture2D>("FishingFish/CoralThumbnail");}
                else if(i==4)DesktopText(nav.transform,"Calendar sun",new Rect(12,2,36,44),"●",28,FishingHUD.AmberInk);
                else if(i==6){var prism=DesktopPicture(nav.transform,"Windows world journal glyph",new Rect(12,7,34,34));prism.texture=Resources.Load<Texture2D>("FishingV019/Thumbnails/StarPrism");}
                else HomeImage(nav.transform,"Windows directory glyph",new Rect(12,7,34,34),Color.white,FishingUITheme.NavigationIcon(new[]{0,5,3,2,6,6,8,3,7}[i]),false);
            }
            DesktopText(_desktopJournal.transform,"Journal motto",new Rect(28,656,200,52),"每次发现，都有回响。\n记录随航程保存",20,FishingHUD.CreamInk);
            HomeImage(_desktopJournal.transform,"Windows journal crest",new Rect(96,604,62,50),Color.white,FishingPaperDesign.Crest,false);
            var close=DesktopButton(_desktopJournal.transform,"Journal close",209,new Rect(24,707,206,32),"B  合上手札",Close,20);close.color=Color.clear;close.GetComponentInChildren<Text>().color=FishingHUD.CreamInk;
            _journalHeading=DesktopText(_desktopJournal.transform,"Windows journal heading",new Rect(290,27,820,53),"",36,FishingUITheme.Ink,true);
            _journalChapter=DesktopText(_desktopJournal.transform,"Windows journal chapter",new Rect(292,84,820,26),"",21,FishingUITheme.Muted);
            _journalNumber=DesktopText(_desktopJournal.transform,"Windows chapter number",new Rect(1271,43,146,28),"",20,FishingUITheme.Muted);_journalNumber.alignment=TextAnchor.MiddleRight;
            HomeImage(_desktopJournal.transform,"Journal header hairline",new Rect(290,124,1130,1),DesktopRule,null,false);
            HomeImage(_desktopJournal.transform,"Journal footer hairline",new Rect(290,708,1130,1),DesktopRule,null,false);
            HomeImage(_desktopJournal.transform,"Quiet reading footer paper",new Rect(280,716,1145,37),FishingUITheme.Paper,null,false);
            DesktopText(_desktopJournal.transform,"Journal reading footer",new Rect(290,722,1110,27),"扳机选择  ·  B 合上  ·  观察与收集进度随航程保存",20,FishingUITheme.Muted);
            _catalogCount=DesktopText(_desktopJournal.transform,"Windows catalog count",new Rect(290,145,310,30),"",22,FishingUITheme.Ink,true);
            _allFilter=DesktopButton(_desktopJournal.transform,"Catalog all filter",220,new Rect(620,141,110,34),"全部",()=>{_knownCatalog=false;_offset=0;Refresh();},20);
            _knownFilter=DesktopButton(_desktopJournal.transform,"Catalog discovered filter",221,new Rect(744,141,134,34),"已发现",()=>{_knownCatalog=true;_offset=0;Refresh();},20);
            _specimenPanel=HomeImage(_desktopJournal.transform,"Windows specimen detail",new Rect(900,145,520,527),FishingUITheme.Light,null,false);
            _specimenBanner=DesktopPicture(_desktopJournal.transform,"Windows chapter illustration",new Rect(900,145,520,130));_specimenBanner.texture=Resources.Load<Texture2D>("FishingV031/Chapters");_specimenBanner.uvRect=new Rect(0,.70f,.25f,.17f);
            HomeImage(_specimenPanel.transform,"Specimen portrait field",new Rect(57,87,406,182),FishingUITheme.Paper,null,false);
            _specimenPhoto=DesktopPicture(_desktopJournal.transform,"Windows large specimen",new Rect(990,250,340,149));
            _specimenBookmark=HomeImage(_desktopJournal.transform,"Windows specimen bookmark",new Rect(1353,143,36,72),Color.white,FishingPaperDesign.Bookmark,false);
            _specimenQuestion=DesktopText(_desktopJournal.transform,"Undiscovered legend silhouette",new Rect(975,257,370,128),"？",70,FishingUITheme.Gold,true);_specimenQuestion.alignment=TextAnchor.MiddleCenter;
            _specimenName=DesktopText(_desktopJournal.transform,"Windows specimen name",new Rect(928,430,464,48),"",32,FishingUITheme.Ink,true);_specimenName.alignment=TextAnchor.UpperCenter;
            _specimenHabitat=DesktopText(_desktopJournal.transform,"Windows specimen habitat",new Rect(934,483,452,56),"",22,FishingUITheme.Muted);_specimenHabitat.alignment=TextAnchor.UpperCenter;
            _specimenRange=DesktopText(_desktopJournal.transform,"Windows specimen weight range",new Rect(930,534,460,25),"",20,FishingUITheme.Muted);_specimenRange.alignment=TextAnchor.MiddleCenter;
            HomeImage(_specimenPanel.transform,"Specimen record rule",new Rect(30,416,460,1),DesktopRule,null,false);
            DesktopText(_specimenPanel.transform,"Maximum record heading",new Rect(30,428,230,25),"最大纪录",20,FishingUITheme.Muted);
            _specimenRecord=DesktopText(_desktopJournal.transform,"Windows specimen record",new Rect(930,603,250,36),"",28,FishingUITheme.Gold,true);
            _specimenDiscovery=DesktopText(_desktopJournal.transform,"Windows specimen discovery",new Rect(1200,573,194,26),"",22,FishingUITheme.Sage);
            _specimenRare=DesktopText(_desktopJournal.transform,"Windows specimen rare form",new Rect(1200,606,194,42),"",20,FishingUITheme.Muted);
            for(int i=0;i<9;i++)
            {
                _desktopCardEdges[i]=HomeImage(_plates[i].transform,"Windows card hairline",new Rect(0,0,278,126),DesktopRule,FishingUITheme.RoundedStroke,false);
                var edgeRect=_desktopCardEdges[i].rectTransform;edgeRect.anchorMin=Vector2.zero;edgeRect.anchorMax=Vector2.one;edgeRect.offsetMin=edgeRect.offsetMax=Vector2.zero;
                if(i<6)_specimenNumbers[i]=DesktopText(_plates[i].transform,"Windows specimen number",new Rect(12,8,66,22),"",16,FishingUITheme.Muted);
            }
            _legendArt=DesktopPicture(_desktopJournal.transform,"Windows legend chapter art",new Rect(290,145,640,300));_legendArt.texture=Resources.Load<Texture2D>("FishingV031/Chapters");_legendArt.uvRect=new Rect(.25f,.5f,.25f,.5f);
            _legendPaper=HomeImage(_desktopJournal.transform,"Windows legend story field",new Rect(290,458,640,213),FishingUITheme.Light,null,false);
            _legendHeading=DesktopText(_desktopJournal.transform,"Windows legend story title",new Rect(313,474,594,48),"海面上，传来金色的传闻",29,FishingUITheme.Gold,true);
            _legendStory=DesktopText(_desktopJournal.transform,"Windows legend story",new Rect(313,530,594,72),"夕照染红水道，它追逐闪亮的饵，跃出浪尖。\n找到它，也请记得让它回到海里。",22,FishingUITheme.Ink);
            _legendRecord=DesktopText(_desktopJournal.transform,"Windows legend record",new Rect(313,618,594,40),"",21,FishingUITheme.Muted);
            BuildDesktopBag();BuildDesktopDialogue();BuildDesktopCrafting();_desktopJournal.SetActive(false);
        }
        private void BuildDesktopBag()
        {
            _desktopBag=new GameObject("Windows 36-slot backpack layout",typeof(RectTransform));_desktopBag.layer=5;_desktopBag.transform.SetParent(_canvas.transform,false);_desktopBag.transform.SetAsFirstSibling();
            HomeRect((RectTransform)_desktopBag.transform,new Rect(0,0,1024,646));
            HomeImage(_desktopBag.transform,"Original backpack parchment",new Rect(-24,-20,1072,686),Color.white,FishingUITheme.TornParchmentSprite,false);
            DesktopText(_desktopBag.transform,"Windows bag heading",new Rect(32,25,450,48),"随 身 行 囊",34,FishingUITheme.Ink,true);
            _bagSummary=DesktopText(_desktopBag.transform,"Windows bag summary",new Rect(32,80,590,30),"",21,FishingUITheme.Muted);
            DesktopText(_desktopBag.transform,"Storage heading",new Rect(32,110,420,30),"储物区 · 27 格",20,FishingUITheme.Ink,true);
            DesktopText(_desktopBag.transform,"Storage limits",new Rect(350,110,270,30),"材料最多 64 / 格",18,FishingUITheme.Muted);
            DesktopText(_desktopBag.transform,"Hotbar heading",new Rect(32,378,420,30),"快捷栏 · 9 格",22,FishingUITheme.Ink,true);
            for(int i=0;i<36;i++)
            {
                int slot=i,cell=i<9?i:i-9;var r=new Rect(32+cell%9*66,i<9?416:142+cell/9*78,60,66);
                _bagSlots[i]=DesktopButton(_desktopBag.transform,"Windows inventory slot "+i,100+i,r,"",()=>SelectDesktopBagSlot(slot));
                _bagEdges[i]=HomeImage(_bagSlots[i].transform,"Windows slot border",new Rect(0,0,60,66),DesktopRule,FishingUITheme.RoundedStroke,false);
                _bagPictures[i]=DesktopPicture(_bagSlots[i].transform,"Actual item photograph",new Rect(7,8,46,44));
                _bagNumbers[i]=DesktopText(_bagSlots[i].transform,"Quick slot number",new Rect(3,1,29,22),(i+1).ToString(),15,FishingUITheme.Muted);
                _bagCounts[i]=DesktopText(_bagSlots[i].transform,"Stack quantity",new Rect(10,42,46,23),"",20,FishingUITheme.Ink);_bagCounts[i].alignment=TextAnchor.MiddleRight;
            }
            _bagHint=DesktopText(_desktopBag.transform,"Windows move instruction",new Rect(32,500,594,65),"",21,FishingUITheme.Muted);
            _bagDiscard=DesktopButton(_desktopBag.transform,"Windows confirmed discard",140,new Rect(32,567,210,35),"销毁选中物品…",DiscardDesktopBagSelection,20);_bagDiscardLabel=_bagDiscard.GetComponentInChildren<Text>();
            _bagInspect=DesktopButton(_desktopBag.transform,"Windows inspect item",141,new Rect(257,567,190,35),"拿起检视",()=>{if(_bagSelection>=0)_c.InspectFieldItem(_bagSelection);},20);
            _bagEquip=DesktopButton(_desktopBag.transform,"VR number-key equivalent",142,new Rect(465,567,155,35),"装备选中物品",()=>
            {if(_bagSelection<0)return;int slot=_bagSelection;if(slot>=FishingBackpack.HotbarCount){if(!_c.MoveFieldItem(slot,_c.Career.Backpack.Selected))return;slot=_c.Career.Backpack.Selected;}_c.SelectFieldSlot(slot);Close();},20);
            HomeImage(_desktopBag.transform,"Windows manual divider",new Rect(658,105,1,495),DesktopRule,null,false);
            DesktopText(_desktopBag.transform,"Windows field manual heading",new Rect(696,109,290,30),"野外手册",26,FishingUITheme.Sage,true);
            for(int i=0;i<3;i++)
            {HomeImage(_desktopBag.transform,"Wildlife note paper "+i,new Rect(696,158+i*93,290,i==2?102:81),FishingUITheme.Light,null,false);_bagNotes[i]=DesktopText(_desktopBag.transform,"Windows wildlife note "+i,new Rect(710,166+i*93,264,i==2?88:67),"",19,FishingUITheme.Ink);}
            DesktopText(_desktopBag.transform,"Windows tool heading",new Rect(696,462,290,30),"工具使用",24,FishingUITheme.Gold,true);
            DesktopText(_desktopBag.transform,"VR tool mapping",new Rect(696,498,290,101),"Y 换装备 · A 互动 · 扳机使用\n握把拿取 · 提灯夜间照路\n生兽肉 + 燃料 → 熔炉炭烤",20,FishingUITheme.Muted);
            var ecology=DesktopButton(_desktopBag.transform,"Windows home ecology entry",143,new Rect(696,605,290,31),"家园生态 · 鱼缸 / 活捉 / 繁殖",()=>{_ecologyMode=-1;Open(21);},18);ecology.color=Color.clear;
            var craft=DesktopButton(_desktopBag.transform,"Windows crafting tab",144,new Rect(677,30,145,34),"制作 / 熔炼",()=>Open(2),20);craft.color=Color.clear;
            var close=DesktopButton(_desktopBag.transform,"Windows bag close",145,new Rect(866,30,124,38),"B 关闭",Close,20);close.color=Color.clear;
            _desktopBag.SetActive(false);
        }
        private void SelectDesktopBagSlot(int slot)
        {
            _discardQuote="";var bag=_c.Career.Backpack;
            if(FishingXRInput.Grip(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand))){_c.MoveFieldItem(slot,0,false,true);_bagSelection=-1;}
            else if(_bagSelection<0){if(bag.At(slot).count>0)_bagSelection=slot;}
            else if(_bagSelection==slot)_bagSelection=-1;
            else{_c.MoveFieldItem(_bagSelection,slot,FishingXRInput.Grip(InputDevices.GetDeviceAtXRNode(XRNode.RightHand)));_bagSelection=-1;}
            Refresh();
        }
        private void BuildDesktopDialogue()
        {
            _desktopDialogue=new GameObject("Windows dialogue layout",typeof(RectTransform));_desktopDialogue.layer=5;_desktopDialogue.transform.SetParent(_canvas.transform,false);_desktopDialogue.transform.SetAsFirstSibling();
            HomeRect((RectTransform)_desktopDialogue.transform,new Rect(0,0,1120,352));
            HomeImage(_desktopDialogue.transform,"Windows dialogue veil",new Rect(-14,-6,1148,364),Color.white,FishingUITheme.PanelVeilSprite,false);
            _dialoguePortrait=DesktopPicture(_desktopDialogue.transform,"Windows resident illustration",new Rect(28,26,74,88));
            _dialogueName=DesktopText(_desktopDialogue.transform,"Windows speaker name",new Rect(120,20,510,50),"",30,FishingHUD.AmberInk,true);
            HomeImage(_desktopDialogue.transform,"Windows speaker gilt rule",new Rect(120,70,300,1),FishingHUD.AmberInk,null,false);
            _dialogueRole=DesktopText(_desktopDialogue.transform,"Windows speaker role",new Rect(122,80,590,60),"",20,FishingHUD.CreamInk);
            HomeImage(_desktopDialogue.transform,"Windows dialogue choice divider",new Rect(760,24,1,304),new Color(.91f,.72f,.35f,.35f),null,false);
            _dialogueSpeech=DesktopText(_desktopDialogue.transform,"Windows spoken text",new Rect(34,145,712,146),"",22,FishingHUD.CreamInk);
            DesktopText(_desktopDialogue.transform,"Windows reveal dialogue hint",new Rect(34,309,700,35),"A 显示完整对话  ·  扳机选择话题  ·  B 结束交谈",20,FishingHUD.CreamInk);
            _desktopDialogue.SetActive(false);
        }
        private void PresentDesktopDialogue()
        {
            var resident=_c.Residents;int id=resident.ActiveId;
            _dialogueName.text=FishingLore.ResidentName(id);_dialogueRole.text=FishingLore.ResidentRole(id)+" / "+resident.TopicTitle;
            _dialogueSpeech.text=resident.VisibleSpeech;_dialogueSpeech.fontSize=22;
            while(_dialogueSpeech.fontSize>18 && _dialogueSpeech.preferredHeight>146)_dialogueSpeech.fontSize--;
            _dialoguePortrait.texture=Resources.Load<Texture2D>((id==4?"FishingV019/Thumbnails/Lantern":"FishingV022/Thumbnails/Resident"+id));
            _status.gameObject.SetActive(false);
            for(int i=0;i<9;i++)
            {
                bool visible=i<resident.ChoiceCount || (i==6 && id==4) || i==8;_plates[i].gameObject.SetActive(visible);if(!visible)continue;
                var r=i<6?new Rect(778,24+i*53,310,47):new Rect(778,i==6?245:294,310,47);
                HomeRect(_plates[i].rectTransform,r);var box=_plates[i].GetComponent<BoxCollider>();box.size=new Vector3(r.width,r.height,3);box.center=new Vector3(r.width*.5f,-r.height*.5f,0);
                _plates[i].sprite=null;_plates[i].color=Color.clear;var border=_plates[i].GetComponent<Outline>();if(border!=null)border.enabled=false;
                _homeEdges[i].gameObject.SetActive(false);_homeIcons[i].gameObject.SetActive(false);_homeArrows[i].gameObject.SetActive(false);_homeCaptions[i].gameObject.SetActive(false);_icons[i].gameObject.SetActive(false);
                HomeRect(_labels[i].rectTransform,new Rect(12,6,286,38));_labels[i].font=FishingUIFont.Font;_labels[i].fontSize=22;_labels[i].color=FishingHUD.CreamInk;_labels[i].text=(i<resident.ChoiceCount?(i+1)+"  ":"")+_pageContent[i];
            }
        }
        private void DiscardDesktopBagSelection()
        {
            var item=_c.Career.Backpack.At(_bagSelection);if(item.count<=0 || item.item==FieldItem.FieldGuide || FishingBackpack.IsQuestItem(item.item))return;
            string quote=_bagSelection+":"+item.item+":"+item.count;
            if(_discardQuote!=quote){_discardQuote=quote;Refresh();return;}
            if(_c.DiscardFieldItem(_bagSelection,item.item,item.count)){_bagSelection=-1;_discardQuote="";Refresh();}
        }
        private void PresentDesktopSurfaces()
        {
            if(_desktopJournal==null)return;
            bool journal=DesktopJournalPage,bag=_page==1,dialogue=_page==8&&_c.DialogueOpen,craft=_page==2;
            for(int i=0;i<9;i++){_desktopCardEdges[i].gameObject.SetActive(journal);if(journal){_plates[i].sprite=FishingUITheme.RoundedSurface;_plates[i].type=Image.Type.Sliced;var outline=_plates[i].GetComponent<Outline>();if(outline!=null)outline.enabled=false;}}
            _desktopJournal.SetActive(journal);_desktopBag.SetActive(bag);_desktopDialogue.SetActive(dialogue);_desktopCraft.SetActive(craft);
            _canvas.GetComponent<Image>().enabled=!journal&&!bag&&!dialogue&&!craft;
            _spreadPaper.gameObject.SetActive(!journal&&!bag&&!dialogue&&!craft);
            _canvas.transform.Find("Windows starry sea surround").gameObject.SetActive(!journal&&!bag&&!dialogue&&!craft);
            foreach(var number in _specimenNumbers)number.gameObject.SetActive(journal && _page==10);
            if(!journal && !bag && !dialogue && !craft)return;
            _homeDecor.SetActive(false);_pageDecor.SetActive(false);_map.gameObject.SetActive(false);if(_albumPreview!=null)_albumPreview.gameObject.SetActive(false);
            _pageDetail.gameObject.SetActive(false);
            ((RectTransform)_canvas.transform).sizeDelta=journal?new Vector2(1456,756):dialogue?new Vector2(1120,352):new Vector2(1024,646);
            if(craft){_status.gameObject.SetActive(false);for(int i=0;i<9;i++)_plates[i].gameObject.SetActive(false);PresentDesktopCrafting();return;}
            if(dialogue){PresentDesktopDialogue();return;}
            if(bag){_status.gameObject.SetActive(false);for(int i=0;i<9;i++)_plates[i].gameObject.SetActive(false);PresentDesktopBag();return;}
            int tab=Array.IndexOf(DesktopPages,_page);if(_page==9)tab=5;if(_page==26)tab=7;if(tab<0)tab=0;
            _journalHeading.text=_page==31?"航海手札":DesktopTabs[tab];_journalChapter.text=DesktopChapters[tab]+"  /  回潮海自然档案";_journalNumber.text=(tab+1).ToString("00")+" / 09";
            _journalPaper.sprite=FishingPaperDesign.Surface(_page==10 || _page==15?FishingPaperTheme.Herbarium:_page==3?FishingPaperTheme.Workshop:FishingPaperTheme.Celestial);_journalPaper.color=FishingPaperDesign.PaperTint;
            bool catalog=_page==10;
            bool legend=_page==29;
            foreach(var graphic in new Graphic[]{_legendArt,_legendPaper,_legendHeading,_legendStory,_legendRecord})graphic.gameObject.SetActive(legend);
            foreach(var graphic in new Graphic[]{_catalogCount,_allFilter,_knownFilter,_specimenPanel,_specimenBanner,_specimenPhoto,_specimenBookmark,_specimenQuestion,_specimenName,_specimenHabitat,_specimenRange,_specimenRecord,_specimenDiscovery,_specimenRare})graphic.gameObject.SetActive(catalog);
            if(catalog){PresentDesktopCatalog();FitDesktopLabels(_desktopJournal);return;}
            if(legend){PresentDesktopLegend();FitDesktopLabels(_desktopJournal);return;}
            _status.gameObject.SetActive(true);HomeRect(_status.rectTransform,new Rect(920,172,470,470));_status.fontSize=22;_status.color=FishingUITheme.Ink;
            for(int i=0;i<9;i++)
            {
                var r=i<6?new Rect(290+i%2*284,145+i/2*157,270,144):new Rect(290+(i-6)*196,644,184,46);
                HomeRect(_plates[i].rectTransform,r);var box=_plates[i].GetComponent<BoxCollider>();box.size=new Vector3(r.width,r.height,3);box.center=new Vector3(r.width*.5f,-r.height*.5f,0);
                _homeEdges[i].gameObject.SetActive(false);_homeArrows[i].gameObject.SetActive(false);_homeIcons[i].gameObject.SetActive(false);
                if(i<6 && _icons[i].gameObject.activeSelf){HomeRect(_icons[i].rectTransform,new Rect(12,12,64,54));HomeRect(_labels[i].rectTransform,new Rect(88,10,166,43));}
                else HomeRect(_labels[i].rectTransform,new Rect(14,8,r.width-28,43));
                if(i<6)
                {
                    var caption=_homeCaptions[i];HomeRect(caption.rectTransform,new Rect(14,70,r.width-28,70));
                    caption.text=caption.text.Replace("\n"," · ");
                    while(caption.text.Length>1 && caption.preferredHeight>caption.rectTransform.rect.height)
                        caption.text=caption.text.TrimEnd('…').Substring(0,caption.text.TrimEnd('…').Length-1)+"…";
                }
            }
            if(_page==AlbumPage)PresentAlbumPage(true);
            UpdateDesktopPointer(_desktopHover);
            FitDesktopLabels(_desktopJournal);
        }
        private static void FitDesktopLabels(GameObject root)
        {
            foreach(var text in root.GetComponentsInChildren<Text>())
                if(text.preferredHeight>text.rectTransform.rect.height)text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Ceil(text.preferredHeight)+2);
        }
        private void PresentDesktopLegend()
        {
            _status.gameObject.SetActive(false);_legendHeading.text=_c.Career.LegendCatches>0?"金帆旗鱼":"海面上，传来金色的传闻";
            _legendRecord.text=_c.Career.LegendCatches>0?"已放生 "+_c.Career.LegendCatches+" 次  ·  纪录 "+FishingText.Weight(_c.Career.BestGrams(FishKind.Sailfish)):"首次测量放生：150–210 金币";
            for(int i=0;i<9;i++)
            {
                bool visible=i<4;_plates[i].gameObject.SetActive(visible);_homeEdges[i].gameObject.SetActive(false);_homeArrows[i].gameObject.SetActive(false);_homeIcons[i].gameObject.SetActive(false);_icons[i].gameObject.SetActive(false);if(!visible)continue;
                var r=i<3?new Rect(962,145+i*128,458,112):new Rect(962,625,458,46);
                HomeRect(_plates[i].rectTransform,r);var box=_plates[i].GetComponent<BoxCollider>();box.size=new Vector3(r.width,r.height,3);box.center=new Vector3(r.width*.5f,-r.height*.5f,0);
                var parts=_pageContent[i].Split(new[]{'\n'},2);_labels[i].text=parts[0];_labels[i].fontSize=24;HomeRect(_labels[i].rectTransform,new Rect(20,10,r.width-40,38));
                _homeCaptions[i].gameObject.SetActive(i<3);if(i<3){_homeCaptions[i].text=parts.Length>1?parts[1]:"";_homeCaptions[i].fontSize=20;HomeRect(_homeCaptions[i].rectTransform,new Rect(20,44,r.width-40,66));}
            }
            UpdateDesktopPointer(_desktopHover);
        }
        private void PresentDesktopCatalog()
        {
            if(_catalogSelection<0 && _catalogEntries.Count>0)_catalogSelection=(int)_catalogEntries[0];
            int known=0;for(int i=0;i<FishingSpecies.Count;i++)if(_c.Career.Discovered((FishKind)i)||_c.Career.Discovered((FishKind)i,true))known++;
            _catalogCount.text="海湾藏品  "+known.ToString("00")+" / "+FishingSpecies.Count;
            _allFilter.color=!_knownCatalog?FishingUITheme.Selected:Color.clear;_knownFilter.color=_knownCatalog?FishingUITheme.Selected:Color.clear;
            _status.gameObject.SetActive(false);
            for(int i=0;i<9;i++)
            {
                bool used=i<6?_offset+i<_catalogEntries.Count:i!=7 && _pageContent[i]!="—";_plates[i].gameObject.SetActive(used);if(!used)continue;
                var r=i<6?new Rect(290+i%2*284,192+i/2*138,278,126):new Rect(i==6?290:763,625,115,36);
                HomeRect(_plates[i].rectTransform,r);var box=_plates[i].GetComponent<BoxCollider>();box.size=new Vector3(r.width,r.height,3);box.center=new Vector3(r.width*.5f,-r.height*.5f,0);
                _homeEdges[i].gameObject.SetActive(false);_homeArrows[i].gameObject.SetActive(false);_homeIcons[i].gameObject.SetActive(false);_homeCaptions[i].gameObject.SetActive(false);
                HomeRect(_labels[i].rectTransform,i<6?new Rect(12,82,254,40):new Rect(3,3,r.width-6,32));_labels[i].fontSize=i<6?24:20;_labels[i].alignment=TextAnchor.MiddleCenter;
                if(i>=6)continue;
                var kind=_catalogEntries[_offset+i];bool knownFish=_c.Career.Discovered(kind)||_c.Career.Discovered(kind,true),secret=FishingSpecies.Of(kind).Legendary&&!knownFish;
                _specimenNumbers[i].text=((int)kind+1).ToString("00");_labels[i].text=secret?"未解的金帆":FishingSpecies.Name(kind);
                _icons[i].gameObject.SetActive(!secret);HomeRect(_icons[i].rectTransform,new Rect(62,7,162,73));_icons[i].color=knownFish?Color.white:new Color(.42f,.50f,.55f,.35f);
            }
            bool selected=_catalogSelection>=0;_specimenPanel.gameObject.SetActive(selected);
            foreach(var graphic in new Graphic[]{_specimenBanner,_specimenPhoto,_specimenBookmark,_specimenQuestion,_specimenName,_specimenHabitat,_specimenRange,_specimenRecord,_specimenDiscovery,_specimenRare})graphic.gameObject.SetActive(selected);
            if(selected)
            {
                var kind=(FishKind)_catalogSelection;var species=FishingSpecies.Of(kind);bool knownFish=_c.Career.Discovered(kind)||_c.Career.Discovered(kind,true),secret=species.Legendary&&!knownFish;
                _specimenPhoto.texture=Resources.Load<Texture2D>("FishingFish/"+kind+"Thumbnail");_specimenPhoto.gameObject.SetActive(!secret);_specimenQuestion.gameObject.SetActive(secret);
                _specimenPhoto.color=knownFish?Color.white:new Color(.42f,.50f,.55f,.35f);_specimenName.text=secret?"传说仍在海风里":species.Name;_specimenHabitat.text=secret?"收集传说线索，寻找海面上的金色帆影。":species.Habitat;
                _specimenRange.text=secret?"传说条件见线索章":FishingText.Weight(species.MinGrams)+" – "+FishingText.Weight(species.MaxGrams);
                _specimenRecord.text=_c.Career.BestGrams(kind)>0?FishingText.Weight(_c.Career.BestGrams(kind)):"等待第一次相遇";
                _specimenDiscovery.text=knownFish?"已收录":"尚未收录";_specimenRare.text=species.Legendary?"测量后放生":_c.Career.Discovered(kind,true)?"珍珠变体已发现":"珍珠变体待发现";
            }
            UpdateDesktopPointer(_desktopHover);
        }
        private void PresentDesktopBag()
        {
            var bag=_c.Career.Backpack;var selected=bag.At(_bagSelection);
            _bagSummary.text=bag.UsedSlots+" / 36 格  ·  物资与鱼箱独立保存";
            _bagSummary.rectTransform.anchoredPosition=new Vector2(32,-76);
            _bagSummary.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,34);
            for(int i=0;i<36;i++)
            {
                var item=bag.At(i);_bagNumbers[i].gameObject.SetActive(i<9);_bagCounts[i].gameObject.SetActive(item.count>1);_bagCounts[i].text=item.count.ToString();
                _bagCounts[i].rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,31);
                _bagPictures[i].gameObject.SetActive(item.count>0);if(item.count>0)_bagPictures[i].texture=FishingV023Art.Icon(item.item)??FishingItemIcons.Get(item.item);
                _bagSlots[i].color=i==_bagSelection || _desktopHover==100+i?FishingUITheme.Selected:item.count>0?FishingUITheme.Light:FishingUITheme.Blend(new Color(.55f,.45f,.30f,.12f),new Color(0,0,0,.25f));
                _bagEdges[i].color=i==_bagSelection?FishingUITheme.Sage:bag.Selected==i?FishingUITheme.Gold:DesktopRule;
            }
            _bagHint.text=selected.count>0?"已选中 "+FishingBackpack.Name(selected.item)+" ×"+selected.count+"：扳机选择目标格。\n右握把 + 扳机：分一半；左握把 + 扳机：快速转移。":"扳机选择物品，再选择目标格：移动 / 合并 / 交换。\n握把组合操作可拆分或在储物区与快捷栏间快速转移。";
            string quote=_bagSelection+":"+selected.item+":"+selected.count;bool discard=selected.count>0 && selected.item!=FieldItem.FieldGuide && !FishingBackpack.IsQuestItem(selected.item);
            _bagDiscardLabel.text=_discardQuote==quote?"确认永久销毁 ×"+selected.count:"销毁选中物品…";_bagDiscard.color=discard?FishingUITheme.Light:FishingUITheme.Disabled;
            _bagInspect.color=_bagEquip.color=selected.count>0?FishingUITheme.Light:FishingUITheme.Disabled;
            string[] notes={"野兔掉落兔肉和兔毛。","赤狐掉落狐狸肉和狐皮。","龟类掉落海龟肉和龟壳；海鸥掉落肉和羽毛。"};
            for(int i=0;i<3;i++)_bagNotes[i].text=(bag.Seen((LandAnimalKind)i)?"已记录 · ":"待观察 · ")+FishingBackpack.AnimalName((LandAnimalKind)i)+"\n"+notes[i];
            FitDesktopLabels(_desktopBag);
        }
        private bool UpdateDesktopPointer(int index)
        {
            if(_desktopJournal==null || (!_desktopJournal.activeSelf && !_desktopBag.activeSelf && !_desktopDialogue.activeSelf && !_desktopCraft.activeSelf))return false;
            _desktopHover=index;
            if(_desktopCraft.activeSelf)return true;
            if(_desktopDialogue.activeSelf){for(int i=0;i<9;i++)if(_plates[i].gameObject.activeSelf)_plates[i].color=i==index?new Color(.18f,.29f,.33f,.6f):Color.clear;return true;}
            if(_desktopBag.activeSelf){PresentDesktopBag();return true;}
            int tab=Array.IndexOf(DesktopPages,_page);if(_page==9)tab=5;if(_page==26)tab=7;if(tab<0)tab=0;
            for(int i=0;i<9;i++){_journalNav[i].color=i==tab?new Color(.18f,.29f,.33f):index==200+i?new Color(.1f,.19f,.25f):Color.clear;_journalNavLabels[i].color=i==tab?FishingHUD.AmberInk:FishingHUD.CreamInk;}
            for(int i=0;i<9;i++)
            {
                bool selected=_page==10 && i<6 && _offset+i<_catalogEntries.Count && (int)_catalogEntries[_offset+i]==_catalogSelection;
                _plates[i].color=selected || i==index?FishingUITheme.Selected:FishingUITheme.Light;
            }
            if(_page==15 || _page==32 || _page==33 || _page==6)
            {
                if(_desktopDetailPage!=_page){_desktopSelection=0;_desktopDetailPage=_page;}
                if(index>=0 && index<6 && _pageContent[index]!="—")_desktopSelection=index;
                if(_pageContent[_desktopSelection]!="—")_status.text=_pageContent[_desktopSelection];
            }
            return true;
        }
    }
}
