using System;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        private void RefreshBarbecuePage()
        {
            var data=_c.Career.Barbecue;bool near=_c.CoastWorld!=null && _c.CoastWorld.NearGrill;
            _status.text="海风烧烤 · 渔屋鱼箱\n选一尾海鱼取出上架，炭烤18秒\n收起手册后继续烤制；进度自动保存\n香烤海鱼使用后恢复50%体力\n"+(near?_c.VisibleMessage:"请先走到渔屋旁的烧烤架前");
            if(data.fish!=null)
            {
                _status.text+="\n"+FishingSpecies.Name(data.fish.kind)+(_c.Career.BarbecueReady?" · 已烤熟":" · 余下 "+Mathf.CeilToInt(data.remaining)+"秒");
                Set(0,_c.Career.BarbecueReady?"领取香烤海鱼":"海鱼烤制中",near && _c.Career.BarbecueReady?(Action)(()=>{_c.CoastWorld.Collect();Refresh();}):null);
                Photo(0,Resources.Load<Texture2D>("FishingV028/Coast/GrilledFishThumbnail"));
            }
            else
            {
                int index=0,shown=0;
                foreach(var fish in _c.Career.Inventory)
                {
                    if(!FishingCoastRules.Edible(fish.kind))continue;
                    if(index++<_offset)continue;if(shown>=6)break;
                    string id=fish.id;int slot=shown++;
                    Set(slot,"取出并烤制\n"+FishingSpecies.Name(fish.kind)+" "+FishingText.Weight(fish.grams),near?(Action)(()=>{_c.CoastWorld.StartRoasting(id);Refresh();}):null);
                    Photo(slot,Resources.Load<Texture2D>("FishingFish/"+fish.kind+"Thumbnail"));
                }
                Set(6,_offset==0?"更多鱼箱鱼获":"返回前页",()=>{_offset=_offset==0?6:0;Refresh();});
                if(index==0)_status.text+="\n鱼箱暂无适合整鱼炭烤的海鱼";
            }
            Set(7,"导航回潮渔屋",()=>{_c.SelectNavigation(_c.HomeNavigationIndex);Close();});Set(8,"收起手册",Close);
        }
    }
}
