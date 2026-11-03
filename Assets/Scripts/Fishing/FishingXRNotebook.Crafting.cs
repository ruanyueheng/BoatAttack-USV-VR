using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        private GameObject _desktopCraft;
        private readonly Image[] _recipeRows=new Image[8];
        private readonly RawImage[] _recipePictures=new RawImage[8];
        private readonly Text[] _recipeNames=new Text[8];
        private Text _craftFacility,_craftName,_craftCosts,_craftRequirement,_craftState,_craftNote,_craftPageNumber;
        private RawImage _craftPicture;
        private Image _craftFacilityTab,_craftHandTab,_craftMake,_craftClaim,_craftRemove,_craftQuantityButton,_craftFuelButton;
        private Text _craftMakeLabel,_craftQuantityLabel,_craftFuelLabel;
        private int _craftSelection,_craftListOffset,_craftQuantity=1,_craftFuel;
        private CraftRecipe _selectedRecipe;
        private readonly FieldItem[] _craftFuels={FieldItem.Driftwood,FieldItem.Plank,FieldItem.Charcoal,FieldItem.Coal};
        private CraftStation _craftContext=(CraftStation)(-1);
        private bool _craftWasBasic;

        private void BuildDesktopCrafting()
        {
            _desktopCraft=new GameObject("Windows crafting layout",typeof(RectTransform));_desktopCraft.layer=5;_desktopCraft.transform.SetParent(_canvas.transform,false);_desktopCraft.transform.SetAsFirstSibling();
            HomeRect((RectTransform)_desktopCraft.transform,new Rect(0,0,1024,646));
            HomeImage(_desktopCraft.transform,"Windows crafting parchment",new Rect(-24,-20,1072,686),Color.white,FishingUITheme.TornParchmentSprite,false);
            DesktopText(_desktopCraft.transform,"Windows workshop heading",new Rect(32,20,450,52),"海 岸 工 坊",34,FishingUITheme.Ink,true);
            DesktopButton(_desktopCraft.transform,"Workshop backpack tab",320,new Rect(519,33,145,38),"背包 / 手册",()=>Open(1),20).color=Color.clear;
            DesktopText(_desktopCraft.transform,"Workshop active tab",new Rect(677,39,160,32),"制作 / 熔炼",20,FishingUITheme.Ink);
            DesktopButton(_desktopCraft.transform,"Workshop close",321,new Rect(866,30,124,42),"B 关闭",Close,20).color=Color.clear;
            _craftFacility=DesktopText(_desktopCraft.transform,"Windows current facility",new Rect(32,98,950,34),"",20,FishingUITheme.Sage);
            _craftFacilityTab=DesktopButton(_desktopCraft.transform,"Facility recipes",308,new Rect(32,136,191,36),"设施配方",()=>{_craftBasics=false;_craftSelection=_craftListOffset=0;Refresh();},20);
            _craftHandTab=DesktopButton(_desktopCraft.transform,"Hand recipes",309,new Rect(232,136,195,36),"随身加工",()=>{_craftBasics=true;_craftSelection=_craftListOffset=0;Refresh();},20);
            HomeImage(_desktopCraft.transform,"Windows recipe list field",new Rect(32,178,395,405),FishingUITheme.Light,FishingUITheme.RoundedSurface,false);
            for(int i=0;i<8;i++)
            {
                int row=i;
                _recipeRows[i]=DesktopButton(_desktopCraft.transform,"Windows recipe row "+i,300+i,new Rect(40,184+i*48,379,44),"",()=>{_craftSelection=_craftListOffset+row;Refresh();},20);
                _recipeNames[i]=_recipeRows[i].GetComponentInChildren<Text>();HomeRect(_recipeNames[i].rectTransform,new Rect(48,5,323,36));
                _recipePictures[i]=DesktopPicture(_recipeRows[i].transform,"Actual recipe output",new Rect(6,5,34,34));
            }
            DesktopButton(_desktopCraft.transform,"Earlier recipes",310,new Rect(32,590,105,38),"‹ 上一页",()=>{_craftListOffset=Mathf.Max(0,_craftListOffset-8);Refresh();},18);
            _craftPageNumber=DesktopText(_desktopCraft.transform,"Recipe list pagination",new Rect(148,594,150,32),"",19,FishingUITheme.Muted);_craftPageNumber.alignment=TextAnchor.MiddleCenter;
            DesktopButton(_desktopCraft.transform,"Later recipes",311,new Rect(310,590,117,38),"下一页 ›",()=>{_craftListOffset+=8;Refresh();},18);
            _craftPicture=DesktopPicture(_desktopCraft.transform,"Windows selected output",new Rect(455,161,46,46));
            _craftName=DesktopText(_desktopCraft.transform,"Windows selected recipe",new Rect(509,156,470,48),"",28,FishingUITheme.Ink,true);
            _craftRequirement=DesktopText(_desktopCraft.transform,"Windows station requirement",new Rect(456,211,522,44),"",20,FishingUITheme.Muted);
            _craftCosts=DesktopText(_desktopCraft.transform,"Windows ingredient amounts",new Rect(456,263,522,145),"",21,FishingUITheme.Ink);
            _craftQuantityButton=DesktopButton(_desktopCraft.transform,"Windows furnace batch",312,new Rect(456,408,205,40),"",()=>{_craftQuantity=_craftQuantity==1?4:_craftQuantity==4?8:1;Refresh();},20);_craftQuantityLabel=_craftQuantityButton.GetComponentInChildren<Text>();
            _craftFuelButton=DesktopButton(_desktopCraft.transform,"Windows furnace fuel",313,new Rect(676,408,302,40),"",()=>{_craftFuel=(_craftFuel+1)%_craftFuels.Length;Refresh();},20);_craftFuelLabel=_craftFuelButton.GetComponentInChildren<Text>();
            _craftMake=DesktopButton(_desktopCraft.transform,"Windows explicit craft action",314,new Rect(456,454,310,44),"",MakeSelectedRecipe,21);_craftMakeLabel=_craftMake.GetComponentInChildren<Text>();
            _craftClaim=DesktopButton(_desktopCraft.transform,"Windows claim furnace output",315,new Rect(778,454,200,44),"领取成品",()=>{_c.WorkshopWorld.Collect();Refresh();},20);
            _craftState=DesktopText(_desktopCraft.transform,"Windows furnace progress",new Rect(456,507,522,77),"",20,FishingUITheme.Gold);
            _craftNote=DesktopText(_desktopCraft.transform,"Windows crafting explanation",new Rect(456,507,522,88),"漂流木加工木板与木柄，纤维绕成绳索。\n提灯用铁框、玻璃和木炭组装。\n四境遗物可在工作台组装远航罗盘。",19,FishingUITheme.Muted);
            _craftRemove=DesktopButton(_desktopCraft.transform,"Windows recover station",316,new Rect(746,588,232,38),"收回此设施",()=>{_c.WorkshopWorld.Remove();Refresh();},20);
            _desktopCraft.SetActive(false);
        }
        private CraftRecipe[] CraftingRecipes()
        {
            var context=_c.WorkshopWorld.Context;bool basic=_craftBasics||context==CraftStation.Hand;
            if(_craftContext!=context || _craftWasBasic!=basic){_craftSelection=_craftListOffset=0;_craftContext=context;_craftWasBasic=basic;}
            return FishingRecipes.ForStation(context,basic);
        }
        private void PresentDesktopCrafting()
        {
            var world=_c.WorkshopWorld;var bag=_c.Career.Backpack;var recipes=CraftingRecipes();
            _craftSelection=Mathf.Clamp(_craftSelection,0,recipes.Length-1);_craftListOffset=Mathf.Clamp(_craftListOffset,0,Mathf.Max(0,(recipes.Length-1)/8*8));
            _selectedRecipe=recipes[_craftSelection];var recipe=_selectedRecipe;bool smelt=recipe.Station==CraftStation.Furnace;
            _craftFacility.text="当前设施："+FishingRecipes.StationName(world.Context)+" · "+FishingRecipes.Purpose(world.Context);
            _craftFacilityTab.color=_craftWasBasic?Color.clear:FishingUITheme.Selected;_craftHandTab.color=_craftWasBasic?FishingUITheme.Selected:Color.clear;
            for(int i=0;i<8;i++)
            {
                int index=_craftListOffset+i;_recipeRows[i].gameObject.SetActive(index<recipes.Length);if(index>=recipes.Length)continue;
                var candidate=recipes[index];_recipeNames[i].text=FishingBackpack.Name(candidate.Output)+" ×"+candidate.Count;
                if(candidate.Output==FieldItem.CookedMeat || candidate.Output==FieldItem.Stick)_recipeNames[i].text+="（"+FishingBackpack.Name(candidate.Costs[0].item)+"）";
                _recipePictures[i].texture=FishingV023Art.Icon(candidate.Output)??FishingItemIcons.Get(candidate.Output);
                _recipeRows[i].color=index==_craftSelection?FishingUITheme.Selected:Color.clear;
            }
            _craftPageNumber.text=(_craftListOffset/8+1)+" / "+Mathf.Max(1,(recipes.Length+7)/8);
            _craftPicture.texture=FishingV023Art.Icon(recipe.Output)??FishingItemIcons.Get(recipe.Output);_craftName.text=FishingBackpack.Name(recipe.Output)+" ×"+recipe.Count;
            _craftRequirement.text="需要："+FishingRecipes.StationName(recipe.Station)+(bag.FreeMaterials?" · 创造模式免普通材料 / 燃料":smelt?" · 每份 8 秒，关手册后继续":" · 材料齐全后立即制作");
            _craftCosts.text="";bool enough=true;int quantity=smelt?_craftQuantity:1;
            foreach(var cost in recipe.Costs){int needed=cost.count*quantity;int have=bag.Count(cost.item);bool free=bag.FreeMaterials && !FishingBackpack.IsQuestItem(cost.item);enough&=free || have>=needed;_craftCosts.text+=FishingBackpack.Name(cost.item)+"   "+(free?"免材料":have+" / "+needed)+"\n";}
            _craftCosts.text=_craftCosts.text.TrimEnd();_craftCosts.fontSize=recipe.Costs.Length>4?19:21;
            _craftQuantityButton.gameObject.SetActive(smelt);_craftFuelButton.gameObject.SetActive(smelt);
            _craftQuantityLabel.text="数量："+_craftQuantity+"  [切换]";var fuel=_craftFuels[_craftFuel];_craftFuelLabel.text="燃料："+FishingBackpack.Name(fuel)+"  [切换]";
            var station=world.State.Station(world.SelectedStation);bool available=world.Context==recipe.Station || recipe.Station==CraftStation.Hand;
            if(smelt)
            {
                int fuelNeeded=(_craftQuantity+FishingRecipes.FuelUnits(fuel)-1)/FishingRecipes.FuelUnits(fuel);
                foreach(var cost in recipe.Costs)if(cost.item==fuel)fuelNeeded+=cost.count*_craftQuantity;
                enough&=bag.FreeMaterials || bag.Count(fuel)>=fuelNeeded;available&=station!=null && station.remaining==0 && station.ready==0;
                _craftMakeLabel.text=!available?"需空闲熔炉":!enough?"材料 / 燃料不足":"加入熔炉";
            }
            else _craftMakeLabel.text=!available?"靠近"+FishingRecipes.StationName(recipe.Station):!enough?"材料不足":"制作  "+FishingBackpack.Name(recipe.Output);
            _craftMake.color=available&&enough?FishingUITheme.Selected:FishingUITheme.Disabled;
            bool furnace=world.Context==CraftStation.Furnace && station!=null;_craftClaim.gameObject.SetActive(furnace);_craftClaim.color=furnace&&station.ready>0?FishingUITheme.Selected:FishingUITheme.Disabled;
            _craftState.gameObject.SetActive(furnace);_craftNote.gameObject.SetActive(!furnace);
            if(furnace){var active=FishingRecipes.Find(station.recipe);_craftState.text=(active==null?"炉内空闲":FishingBackpack.Name(active.Output)+" · 待烧 "+station.remaining+" · 成品 "+station.ready+"\n"+station.progress.ToString("0.0")+" / 8 秒")+"\n退出手册后继续烧制。离开岸边也会加工。";}
            _craftRemove.gameObject.SetActive(station!=null&&!station.permanent);FitDesktopLabels(_desktopCraft);
        }
        private void MakeSelectedRecipe()
        {
            if(_selectedRecipe==null)return;
            if(_selectedRecipe.Station==CraftStation.Furnace)_c.WorkshopWorld.Smelt(_selectedRecipe.Id,_craftQuantity,_craftFuels[_craftFuel]);
            else if(_selectedRecipe.Id=="voyage-compass")_c.Voyage.CraftCompass();
            else _c.WorkshopWorld.Craft(_selectedRecipe.Id);
            Refresh();
        }
    }
}
