using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private Vector2 _barbecueScroll;
        private void DrawBarbecue()
        {
            var c=Controller;var coast=c.CoastWorld;var data=c.Career.Barbecue;
            Fill(-_offset/_scale,0,Screen.width/_scale,_height,new Color(.02f,.035f,.04f,.68f));
            var panel=new Rect(300,110,1000,Mathf.Min(650,_height-130));Card(panel,Mint);
            Label(330,134,650,44,"海风烧烤 · 渔屋鱼箱",30,InkText,true);
            Label(331,187,780,48,"从共用鱼箱挑选一尾海鱼，取出后放上烤架。\n炭烤18秒；收起面板继续，烤熟后领取到背包。",16,Muted);
            if(Button(new Rect(1130,138,138,36),"Esc 收起",true,false,15)){coast.CloseMenu();return;}
            var photo=Resources.Load<Texture2D>("FishingV028/Coast/GrilledFishThumbnail");
            if(photo!=null)GUI.DrawTexture(new Rect(1000,228,250,190),photo,ScaleMode.ScaleToFit,true);
            if(data.fish!=null)
            {
                string status=c.Career.BarbecueReady?"鱼皮微焦，柠檬清香 · 已烤熟":"正在烤制 "+FishingSpecies.Name(data.fish.kind)+" · 余下 "+Mathf.CeilToInt(data.remaining)+" 秒";
                Label(332,255,660,38,status,23,c.Career.BarbecueReady?Gold:InkText,true);
                Label(332,309,630,58,"烤架里的鱼与烤制进度会保存。\n领取后将香烤海鱼放入快捷栏，按 F 食用，恢复50%体力。",17,Muted);
                if(Button(new Rect(332,394,270,46),c.Career.BarbecueReady?"领取香烤海鱼":"炭火慢烤中…",c.Career.BarbecueReady,false,18))coast.Collect();
            }
            else
            {
                int eligible=0;foreach(var fish in c.Career.Inventory)if(FishingCoastRules.Edible(fish.kind))eligible++;
                var area=new Rect(332,248,664,Mathf.Max(80,panel.yMax-314));
                _barbecueScroll=GUI.BeginScrollView(area,_barbecueScroll,new Rect(0,0,640,Mathf.Max(area.height,eligible*43)));
                int row=0;bool chosen=false;
                foreach(var fish in c.Career.Inventory)
                {
                    if(!FishingCoastRules.Edible(fish.kind))continue;
                    float y=row*43;
                    FishGlyph(new Rect(0,y,42,32),fish.kind,fish.rare);
                    Label(56,y+5,440,30,FishingSpecies.Name(fish.kind)+(fish.rare?" · 珍珠":"")+"  "+FishingText.Weight(fish.grams)+"  ·  "+fish.value+" 金币",17,InkText);
                    if(Button(new Rect(496,y,140,34),"取鱼上架",true,false,15)){coast.StartRoasting(fish.id);chosen=true;break;}
                    row++;
                }
                GUI.EndScrollView();if(chosen)return;
                if(row==0)Label(333,265,620,70,"鱼箱还没有适合整鱼炭烤的海鱼。\n去浅湾钓一尾回来，烧烤架会在家等你。",20,Muted);
            }
            Label(332,panel.yMax-52,920,32,c.VisibleMessage,15,Muted);
        }
    }
}
