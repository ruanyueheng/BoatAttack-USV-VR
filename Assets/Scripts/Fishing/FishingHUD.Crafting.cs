using System.Text;
using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private bool _craftPage,_craftBasics;
        private Vector2 _recipeScroll;
        private int _recipeIndex,_batch=1,_fuelIndex;
        private readonly FieldItem[] _fuels={FieldItem.Driftwood,FieldItem.Plank,FieldItem.Charcoal,FieldItem.Coal};
        public void OpenCraftingPage(){_craftPage=true;_craftBasics=false;_moveFrom=-1;_recipeIndex=0;_recipeScroll=Vector2.zero;}
        private void DrawCraftingContents()
        {
            var world=Controller.WorkshopWorld;var bag=Controller.Career.Backpack;var context=world.Context;
            bool basic=_craftBasics || context==CraftStation.Hand;
            var recipes=FishingRecipes.ForStation(context,basic);
            _recipeIndex=Mathf.Clamp(_recipeIndex,0,recipes.Length-1);
            Label(320,238,940,24,"当前设施："+FishingRecipes.StationName(context)+"  ·  "+FishingRecipes.Purpose(context),16,Mint,true);
            if(Button(new Rect(320,266,191,30),"设施配方",context!=CraftStation.Hand,!basic,14)){_craftBasics=false;_recipeIndex=0;_recipeScroll=Vector2.zero;}
            if(Button(new Rect(520,266,195,30),"随身加工",true,basic,14)){_craftBasics=true;_recipeIndex=0;_recipeScroll=Vector2.zero;}
            Rounded(new Rect(320,302,395,413),InkRaised,8);
            _recipeScroll=GUI.BeginScrollView(new Rect(326,308,384,401),_recipeScroll,new Rect(0,0,359,recipes.Length*45));
            for(int i=0;i<recipes.Length;i++)
            {
                var recipe=recipes[i];
                string ingredient=recipe.Output==FieldItem.CookedMeat || recipe.Output==FieldItem.Stick?"（"+FishingBackpack.Name(recipe.Costs[0].item)+"）":recipe.Id.StartsWith("coal-")?"（"+FishingBackpack.Name(recipe.Costs[1].item)+"）":"";
                if(Button(new Rect(3,i*45,350,39),"",true,_recipeIndex==i,15))_recipeIndex=i;
                FieldIcon(new Rect(8,i*45+3,32,32),recipe.Output);
                Label(48,i*45+2,298,20,FishingBackpack.Name(recipe.Output)+ingredient+" ×"+recipe.Count,15,_recipeIndex==i?Mint:Color.white);
                Label(48,i*45+22,298,16,FishingRecipes.StationName(recipe.Station),12,Faint);
            }
            GUI.EndScrollView();
            var r=recipes[_recipeIndex];bool smelt=r.Station==CraftStation.Furnace;
            FieldIcon(new Rect(743,291,38,40),r.Output);
            Label(797,286,425,38,FishingBackpack.Name(r.Output)+" ×"+r.Count,25,Color.white,true);
            Label(744,335,490,24,"需要："+FishingRecipes.StationName(r.Station)+(smelt?"  ·  每份 8 秒，关背包后继续":"  ·  材料齐全后立即制作"),15,Muted);
            int quantity=smelt?_batch:1;float y=375;bool enough=true;
            float costHeight=r.Costs.Length>4?25:33;
            foreach(var cost in r.Costs)
            {
                int needed=cost.count*quantity,have=bag.Count(cost.item);enough &= have>=needed;
                FieldIcon(new Rect(746,y,costHeight-3,costHeight-3),cost.item);
                Label(784,y,442,costHeight,FishingBackpack.Name(cost.item)+"   "+have+" / "+needed,r.Costs.Length>4?16:19,have>=needed?Mint:Danger);y+=costHeight;
            }
            if(smelt)
            {
                if(Button(new Rect(746,456,205,34),"数量："+_batch+"  [切换]",true,false,15))_batch=_batch==1?4:_batch==4?8:1;
                if(Button(new Rect(966,456,302,34),"燃料："+FishingBackpack.Name(_fuels[_fuelIndex])+"  [切换]",true,false,15))_fuelIndex=(_fuelIndex+1)%_fuels.Length;
                var fuel=_fuels[_fuelIndex];int fuelCount=(_batch+FishingRecipes.FuelUnits(fuel)-1)/FishingRecipes.FuelUnits(fuel);
                int needed=fuelCount;foreach(var cost in r.Costs)if(cost.item==fuel)needed+=cost.count*_batch;
                bool fuelReady=bag.Count(fuel)>=needed;
                Label(746,500,516,32,"燃料 "+fuelCount+" 个 · "+FishingBackpack.Name(fuel)+" 合计需 "+needed+" / 持有 "+bag.Count(fuel),15,fuelReady?Muted:Danger);
                var station=world.State.Station(world.SelectedStation);
                bool available=context==CraftStation.Furnace && station!=null && station.remaining==0 && station.ready==0;
                if(Button(new Rect(746,542,255,40),!available?"需空闲熔炉":!enough || !fuelReady?"材料 / 燃料不足":"加入熔炉",available && enough && fuelReady,false,17))world.Smelt(r.Id,_batch,fuel);
                if(Button(new Rect(1015,542,253,40),"领取炉内成品",context==CraftStation.Furnace && station!=null && station.ready>0,false,17))world.Collect();
            }
            else
            {
                bool station=context==r.Station || r.Station==CraftStation.Hand;
                if(Button(new Rect(746,Mathf.Max(500,y+8),522,45),station?(context==CraftStation.Forge?"锻造  ":"制作  ")+FishingBackpack.Name(r.Output):"靠近"+FishingRecipes.StationName(r.Station)+"按 E 后制作",station && enough,false,18))
                {if(r.Id=="voyage-compass")Controller.Voyage.CraftCompass();else world.Craft(r.Id);}
            }
            var current=world.State.Station(world.SelectedStation);
            if(context==CraftStation.Furnace && current!=null)
            {
                var active=FishingRecipes.Find(current.recipe);
                Label(746,600,522,27,active==null?"炉内空闲":FishingBackpack.Name(active.Output)+" · 待烧 "+current.remaining+" · 成品 "+current.ready+" · "+current.progress.ToString("0.0")+" / 8 秒",17,Gold,true);
                Bar(new Rect(746,627,522,3),current.remaining>0?current.progress/8f:current.ready>0?1:0,Gold);
                Label(746,632,522,34,"背包打开时世界暂停；退出后继续烧制。离开岸边也会加工。",14,Muted);
                if(current.ready>0 && !smelt && Button(new Rect(746,671,230,34),"领取炉内成品",true,false,15))world.Collect();
            }
            else Label(746,599,514,70,"漂流木加工木板与木柄，纤维绕成绳索。\n提灯用铁框、玻璃和木炭组装；皮毛用于家园织物。\n四境遗物可在工作台组装远航罗盘。",15,Muted);
            if(context!=CraftStation.Hand && current!=null && !current.permanent)
                if(Button(new Rect(1034,681,234,34),"收回此设施",current.remaining==0 && current.ready==0,false,15))world.Remove();
            Label(320,737,950,24,"设施与炉内物品独立保存 · 满包不吞材料 · 公共工坊位于西侧初始登岸点附近",14,Faint);
        }
    }
}
