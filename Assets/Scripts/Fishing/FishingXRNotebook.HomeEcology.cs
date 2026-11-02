using System;
using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        private int _ecologyMode;
        public void OpenHomeEcology(int mode=0,bool keepHabitat=false)
        {if(!keepHabitat)_c.HomeEcology.ClearHabitatFilter();Open(21);_ecologyMode=Mathf.Clamp(mode,0,4);_c.HomeEcology.Page=0;if(IsOpen)Refresh();}
        private void RefreshHomeEcology()
        {
            var world=_c.HomeEcology;if(world==null)return;
            _status.fontSize=20;_status.text="家园生态\n"+world.Status+"\n"+_c.VisibleMessage;
            if(_ecologyMode<0)
            {
                string[] tabs={"设施建设","鱼箱 → 展示","水族箱养鱼","动物与繁殖","诱捕笼"};
                for(int i=0;i<5;i++){int mode=i;Set(i,tabs[i],()=>{world.ClearHabitatFilter();_ecologyMode=mode;Refresh();});}
                Set(5,"放笼物种："+FishingBackpack.AnimalName(world.TrapKind),()=>{world.TrapKind=(LandAnimalKind)(((int)world.TrapKind+1)%4);Refresh();});
                Set(7,"返回首页",()=>Open());Set(8,"关闭",Close);return;
            }
            var choices=world.Choices(_ecologyMode);int pages=Mathf.Max(1,Mathf.CeilToInt(choices.Count/6f));world.Page=Mathf.Clamp(world.Page,0,pages-1);
            for(int i=0;i<6;i++)
            {int index=world.Page*6+i;if(index>=choices.Count)break;var choice=choices[index];Set(i,choice.Label,choice.Enabled?(Action)(()=>{choice.Run();Refresh();}):null);}
            Set(6,(world.Page+1)+"/"+pages+" · 下一页",pages>1?(Action)(()=>{world.Page=(world.Page+1)%pages;Refresh();}):null);
            Set(7,"养鱼 / 动物 / 诱捕目录",()=>{world.ClearHabitatFilter();_ecologyMode=-1;Refresh();});Set(8,"关闭",Close);
        }
    }
}
