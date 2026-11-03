using System;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        public const int RealmDemoPage=39,RealmResetPage=40;
        private int _realmResetSelection=-2;
        private void RefreshRealmDemo()
        {
            if(_page==RealmResetPage)
            {
                _status.text="秘境演示重置\n机关、星座和回响贝壳回到初始状态。\n背包、金币、已领遗物和蓝图保留。\n重演不会重复发放已领取奖励。\n在秘境内操作会先回到安全的岛屿入口。\n请选择秘境，再确认重置。\n"+_c.VisibleMessage;
                if(_realmResetSelection>=-1)
                {
                    int selected=_realmResetSelection;
                    Set(0,"确认重置\n"+(selected<0?"四个秘境":FishingLore.ShrineName(selected)),_c.CreativeMode?(Action)(()=>{_c.ResetRealmsForDemo(selected);_realmResetSelection=-2;Open(RealmDemoPage);}):null);
                    Set(1,"取消，保留当前进度",()=>{_realmResetSelection=-2;Refresh();});
                }
                else
                {
                    for(int i=0;i<4;i++){int island=i;Set(i,FishingLore.ShrineName(i)+"\n选择重置",_c.CreativeMode?(Action)(()=>{_realmResetSelection=island;Refresh();}):null);}
                    Set(4,"重置四个秘境",_c.CreativeMode?(Action)(()=>{_realmResetSelection=-1;Refresh();}):null);
                }
                Set(7,"返回秘境展示",()=>{_realmResetSelection=-2;Open(RealmDemoPage);});Set(8,"返回首页",()=>Open());return;
            }
            _status.text="四岛秘境 · 课堂展示\n传送到岛外入口，按 A / E 正常进入。\n传送开关仅对本次游玩生效，需要创造模式。\n重置可重演第一次的机关、星座和回响。\n已领取的金币、遗物和蓝图不会重复发放。\n"+_c.VisibleMessage;
            for(int i=0;i<4;i++)
            {
                int island=i;
                Set(i,FishingAdventure.Name(i)+"\n传送至秘境入口",_c.CreativeRealmTravel?(Action)(()=>_c.TeleportToRealmEntrance(island)):null);
            }
            Set(4,"入口传送："+(_c.CreativeRealmTravel?"开\n点击关闭":"关\n点击开启"),_c.CreativeMode?(Action)(()=>{_c.SetCreativeRealmTravel(!_c.CreativeRealmTravel);Refresh();}):null);
            Set(5,"重置秘境\n选择与确认",()=>{_realmResetSelection=-2;Open(RealmResetPage);});
            Set(6,"补齐养殖动物",_c.CreativeMode?(Action)(()=>{_c.PrepareAnimalShowcase();Refresh();}):null);
            Set(7,"返回创造模式",()=>Open(CreativePage));Set(8,"返回首页",()=>Open());
        }
    }
}
