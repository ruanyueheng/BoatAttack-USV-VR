using System;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        private void RefreshHomeStorage()
        {
            bool deposit=_page==18;var bag=deposit?_c.Career.Backpack:_c.Career.HomeStorage;
            _status.text="家园储物箱 · "+(deposit?"存入":"取出")+"\n随身行囊 "+_c.Career.Backpack.UsedSlots+" / 36 格\n家园收藏 "+_c.Career.HomeStorage.UsedSlots+" / 36 格\n\n点右侧物品，整组"+(deposit?"存入箱中":"取回行囊")+"。\n空间不足时保留原物品。\n\n"+_c.VisibleMessage;
            for(int i=0;i<6;i++)
            {
                int slot=_offset+i;if(slot>=FishingBackpack.SlotCount)break;var item=bag.At(slot);
                Set(i,(slot+1)+" "+FishingBackpack.Name(item.item)+"\n×"+item.count,item.count>0?(Action)(()=>{_c.Home.TransferStorage(deposit,slot);Refresh();}):null);
                if(item.count>0)Photo(i,FishingV023Art.Icon(item.item)??FishingItemIcons.Get(item.item));
            }
            Pager(FishingBackpack.SlotCount);Set(7,deposit?"切换取出物品":"切换存入物品",()=>Open(deposit?19:18));
        }
    }
}
