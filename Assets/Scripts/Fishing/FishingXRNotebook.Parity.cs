using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        private const int ItemPage=27,MovePage=28,LegendPage=29,JournalPage=31,LorePage=32,SurveyPage=33;
        private int _itemSlot=-1,_catalogSelection=-1,_natureSelection=-1,_loreSelection,_surveySelection;
        private bool _moveHalf,_knownCatalog;
        private readonly List<FishKind> _catalogEntries=new List<FishKind>();
        private string _discardQuote="";
        public bool InventoryPage=>IsOpen && (_page==1 || _page==ItemPage || _page==MovePage);
        private void SelectInventoryItem(int slot)
        {_itemSlot=slot;_discardQuote="";Open(ItemPage);}
        private void RefreshInventoryItem()
        {
            var item=_c.Career.Backpack.At(_itemSlot);
            if(item.count<=0){_status.text="这个格子已空。返回行囊选择物品。";Set(7,"返回行囊",()=>Open(1));return;}
            string quote=_itemSlot+":"+item.item+":"+item.count;
            bool discard=item.item!=FieldItem.FieldGuide && !FishingBackpack.IsQuestItem(item.item);
            _status.text=FishingBackpack.Name(item.item)+" ×"+item.count+"\n第 "+(_itemSlot+1)+" 格\n\n整组移动、合并或交换；也可拆出一半。\n销毁须再次确认，航海遗物与自然手册不能销毁。";
            Set(0,item.item==FieldItem.Lantern?"装备并挂到腰侧\n右扳机开关 · Y 收回背包":"装备并拿起检视",()=>EquipSlot(_itemSlot));Photo(0,FishingV023Art.Icon(item.item));
            if(item.item==FieldItem.Lantern)Set(5,"熄灯并收回背包\n取下腰挂，腾出双手",()=>{_c.StowLantern();Close();});
            Set(1,"快速转移\n快捷栏 ↔ 储物区",()=>{if(_c.MoveFieldItem(_itemSlot,0,false,true))Open(1);else _c.Notice("另一侧没有可用空间，原物品保留。",4);});
            Set(2,"整组移动 / 合并 / 交换",()=>{_moveHalf=false;Open(MovePage);});
            Set(3,"分出一半\n选择目标格",item.count>1?(Action)(()=>{_moveHalf=true;Open(MovePage);}):null);
            Set(4,_discardQuote==quote?"确认销毁 ×"+item.count:"销毁这组物品…",discard?(Action)(()=>
            {
                if(_discardQuote!=quote){_discardQuote=quote;Refresh();return;}
                if(_c.DiscardFieldItem(_itemSlot,item.item,item.count)){_discardQuote="";Open(1);}
            }):null);
            Set(7,"返回行囊",()=>{_discardQuote="";Open(1);});Set(8,"返回海湾",Close);
        }
        private void RefreshInventoryMove()
        {
            var bag=_c.Career.Backpack;var source=bag.At(_itemSlot);
            _status.text=(_moveHalf?"拆分一半":"整组移动 / 合并 / 交换")+"\n"+FishingBackpack.Name(source.item)+" ×"+source.count+"\n从第 "+(_itemSlot+1)+" 格移到右侧所选格。\n空格也可选；空间不足时保留原物品。";
            for(int i=0;i<6;i++)
            {
                int slot=_offset+i;if(slot>=FishingBackpack.SlotCount)break;var target=bag.At(slot);
                Set(i,(slot+1)+" · "+(target.count>0?FishingBackpack.Name(target.item)+"\n×"+target.count:"空格"),source.count>0 && slot!=_itemSlot?(Action)(()=>
                {if(_c.MoveFieldItem(_itemSlot,slot,_moveHalf))Open(1);else _c.Notice("目标格无法容纳这组物品，原物品保留。",4);}):null);
                if(target.count>0)Photo(i,FishingV023Art.Icon(target.item));
            }
            Pager(FishingBackpack.SlotCount);Set(7,"取消 / 返回物品",()=>Open(ItemPage));
        }
        private void RefreshJournalIndex()
        {
            _status.text="航海手札\n每次发现，都有回响。\n鱼获、自然观察、调查、剧情与照片随航程保存。";
            Set(0,"鱼获图鉴 · 全部",()=>{_knownCatalog=false;Open(10);});
            Set(1,"鱼获图鉴 · 已发现",()=>{_knownCatalog=true;Open(10);});
            Set(2,"传说线索 / 调查航点",()=>Open(LegendPage));
            Set(3,"自然观察 / 海岸调查",()=>Open(15));Set(4,"海湾日历",()=>Open(3));
            Set(5,"群岛航记 / 主线",()=>Open(9));Set(6,"世界与秘境",()=>Open(LorePage));
            Set(7,"相册 / 照片图鉴",()=>Open(AlbumPage));Set(8,"信件 / 漂流瓶",()=>Open(LettersPage));
        }
        private string CatalogDetail(FishKind kind)
        {
            var species=FishingSpecies.Of(kind);var career=_c.Career;
            bool known=career.Discovered(kind)||career.Discovered(kind,true);
            if(species.Legendary && !known)return "传说仍在海风里\n收集线索，寻找海面上的金色帆影。\n传说条件见「航海手札 → 传说线索」。";
            return species.Name+"\n"+species.Habitat+"\n体重 "+FishingText.Weight(species.MinGrams)+" – "+FishingText.Weight(species.MaxGrams)
                +"\n最大纪录："+(career.BestGrams(kind)>0?FishingText.Weight(career.BestGrams(kind)):"等待第一次相遇")
                +"\n"+(career.Discovered(kind)?"普通已发现":"普通待发现")+" · "+(species.Legendary?"测量后放生":career.Discovered(kind,true)?"珍珠变体已发现":"珍珠变体待发现");
        }
        private void RefreshCatalog()
        {
            var entries=_catalogEntries;entries.Clear();int known=0;
            for(int i=0;i<FishingSpecies.Count;i++)
            {var kind=(FishKind)i;bool found=_c.Career.Discovered(kind)||_c.Career.Discovered(kind,true);if(found)known++;if(!_knownCatalog||found)entries.Add(kind);}
            if(_knownCatalog && !entries.Contains((FishKind)_catalogSelection))_catalogSelection=-1;
            _offset=Mathf.Min(_offset,Mathf.Max(0,(entries.Count-1)/6*6));
            _status.text=_catalogSelection>=0?CatalogDetail((FishKind)_catalogSelection):"海湾藏品 · "+known+" / "+FishingSpecies.Count+"\n"+(_knownCatalog?"只看已发现":"全部物种")+"\n指向或选择卡片查看栖息条件、普通与珍珠发现、体重纪录。";
            for(int i=0;i<6;i++)
            {
                int index=_offset+i;if(index>=entries.Count)break;var kind=entries[index];
                Set(i,CatalogDetail(kind),()=>{_catalogSelection=(int)kind;Refresh();});
                bool hidden=FishingSpecies.Of(kind).Legendary && !_c.Career.Discovered(kind) && !_c.Career.Discovered(kind,true);
                if(!hidden)Photo(i,Resources.Load<Texture2D>("FishingFish/"+kind+"Thumbnail"));
            }
            Pager(entries.Count);Set(7,"航海手札 / 筛选",()=>Open(JournalPage));
        }
        private string NatureDetail(int index)
        {
            if(index<5){var kind=(MarineKind)index;return BayClimate.MarineName(kind)+"\n"+BayClimate.MarineHint(kind)+"\n"+(_c.Career.Seen(kind)?"观察已记录":"尚待观察");}
            var animal=(LandAnimalKind)(index-5);
            string hint=index==5?"草地与灌木间的轻巧访客。放慢脚步，留一点距离。":index==6?"林缘与沙洲上的赤色身影。蹲下靠近，更容易观察。":index==7?"岩石和草地上的慢行者。请让它按自己的节奏生活。":BayClimate.MarineHint((MarineKind)(index==8?4:0));
            return FishingBackpack.AnimalName(animal)+"\n"+hint+"\n"+(_c.Career.Backpack.Seen(animal)?"观察已记录":"尚待观察");
        }
        private void RefreshNature()
        {
            _status.text=_natureSelection>=0?NatureDetail(_natureSelection):"自然观察 / 海岸调查\n自然手册或望远镜：看向动物按 A\n海洋生物保持距离，陆地动物缓慢接近\n各区域两项调查回港提交 +25 金币\n"+_c.VisibleMessage;
            string[] portraits={"FishingV029/TurtleThumbnail","FishingFish/StingrayThumbnail","FishingV031/DolphinPortrait","FishingFish/SardineThumbnail","FishingV029/SeagullThumbnail","FishingV029/RabbitThumbnail","FishingV029/FoxThumbnail","FishingV031/TortoisePortrait","FishingV029/SeagullThumbnail","FishingV029/TurtleThumbnail"};
            for(int i=0;i<6;i++)
            {
                int index=_offset+i;
                if(index<10){Set(i,NatureDetail(index),()=>{_natureSelection=index;Refresh();});Photo(i,Resources.Load<Texture2D>(portraits[index]));}
                else if(index<14){int site=index-10;Set(i,FishingExploration.SiteName(site)+"\n"+(FishingExploration.SiteDone(_c.Career.ShoreTaskMask,site)?"调查完成":"待调查"),()=>{_surveySelection=site;Open(SurveyPage);});}
            }
            Pager(14);Set(7,"返回航海手札",()=>Open(JournalPage));
        }
        private void RefreshLegend()
        {
            var career=_c.Career;
            _status.text="夕照中的金帆\n夕照染红水道，它追逐闪亮的饵，跃出浪尖。\n找到它，也请记得让它回到海里。\n\n已放生 "+career.LegendCatches+" 次\n最大纪录 "+FishingText.Weight(career.BestGrams(FishKind.Sailfish));
            for(int i=0;i<LegendRules.ClueCount;i++)
            {bool found=LegendRules.ClueUnlocked(i,career);Set(i,"线索 0"+(i+1)+(found?" · 已发现":" · 待发现")+"\n"+(found?LegendRules.Clue(i):LegendRules.ClueHint(i)),()=>{});}
            Set(3,"设定图鉴调查航点",()=>{_c.NavigateSurvey();Close();});
            Set(7,"返回航海手札",()=>Open(JournalPage));Set(8,"返回海湾",Close);
        }
        private void RefreshLore()
        {
            _status.text=_c.Career.MainObjective+"\n四境图页 "+_c.Career.Lore.ClearCount+" / 4\n"+FishingLore.ResidentName(_loreSelection)+" · "+FishingLore.ResidentRole(_loreSelection);
            for(int i=0;i<4;i++)
            {
                int island=i;bool cleared=_c.Career.Lore.Cleared(i);
                var reward=FishingCareer.ShrineReward(i);
                Set(i,FishingLore.ShrineName(i)+"\n"+FishingLore.Subtitle(i)+"\n"+(cleared?FishingLore.Echo(i):FishingLore.Inscription(i))+"\n"+FishingLore.TrialObjective(i)
                    +"\n秘境收获："+FishingBackpack.Name(reward[0].item)+"、"+FishingBackpack.Name(reward[1].item)+" ×"+reward[1].count
                    +"\n"+(_c.Career.Lore.RewardClaimed(i)?"已领取 · ":"尚待领取 · ")+FishingLore.BlueprintName(i)+" · "+FishingLore.BlueprintEffect(i),()=>{_loreSelection=island;Refresh();});
            }
            Set(4,"前往所选岛屿",()=>{_c.SelectNavigation(_c.IslandNavigationIndex(_loreSelection));Close();});
            Set(5,"阅读故事",_c.Letter.CanRead?(Action)(()=>{int id=_loreSelection;Close();_c.Letter.Show(FishingLore.ShrineName(id)+"航记",FishingLore.ResidentName(id),(_c.Career.Lore.Cleared(id)?FishingLore.Echo(id):FishingLore.TrialHint(id))+"\n\n"+_c.Career.MainObjective);}):null);
            Set(7,"返回航海手札",()=>Open(JournalPage));Set(8,"返回海湾",Close);
        }
        private void RefreshSurvey()
        {
            _status.text="沿岸调查\n每区两项调查，走近勘测台按 A 记录。\n已记录 "+FishingExploration.CompletedTasks(_c.Career.ShoreTaskMask)+" / 8\n每区回港提交 +25 金币；四区完成额外 +60 金币。\n"+_c.Career.Expedition.NextObjective;
            for(int i=0;i<4;i++)
            {
                int site=i;string text=FishingExploration.SiteName(i);
                for(int t=0;t<2;t++)text+="\n"+(FishingExploration.Done(_c.Career.ShoreTaskMask,i*2+t)?"已记录 · ":"待调查 · ")+FishingExploration.TaskName(i*2+t);
                Set(i,text,()=>{_surveySelection=site;Refresh();});
            }
            Set(4,"前往所选调查点",()=>{_c.SelectNavigation(_c.fishingSpots.Length+1+_surveySelection);Close();});
            Set(5,_c.Expedition.PrecisionGuide?"关闭信标指引":"开启信标指引",()=>{_c.Expedition.ToggleGuide();Refresh();});
            Set(7,"返回航海手札",()=>Open(JournalPage));Set(8,"返回海湾",Close);
        }
    }
}
