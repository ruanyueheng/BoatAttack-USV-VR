using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private bool _ecologyPage;
        private int _ecologyMode;
        public void OpenHomeEcologyPage(int mode=0,bool keepHabitat=false){if(!keepHabitat)Controller.HomeEcology.ClearHabitatFilter();_ecologyPage=true;_craftPage=false;_ecologyMode=Mathf.Clamp(mode,0,4);Controller.HomeEcology.Page=0;}
        private void DrawHomeEcology()
        {
            var world=Controller.HomeEcology;
            MenuSheet(BackpackPanel);
            MenuTitle(320,146,700,"家园生态","");
            if(Button(new Rect(1060,160,210,38),"返回背包",true,false,16)){_ecologyPage=false;return;}
            Label(322,212,940,54,world.Status,15,Muted);
            string[] tabs={"设施建设","鱼箱 → 展示","水族箱养鱼","动物与繁殖","诱捕笼"};
            for(int i=0;i<tabs.Length;i++)if(Button(new Rect(320+i*191,277,183,38),tabs[i],true,_ecologyMode==i,16)){world.ClearHabitatFilter();_ecologyMode=i;}
            var choices=world.Choices(_ecologyMode);int pages=Mathf.Max(1,Mathf.CeilToInt(choices.Count/6f));world.Page=Mathf.Clamp(world.Page,0,pages-1);
            for(int i=0;i<6;i++)
            {
                int index=world.Page*6+i;if(index>=choices.Count)break;var choice=choices[index];
                if(RowCard(new Rect(320,328+i*56,951,50),new[]{5,7,1,3,8}[_ecologyMode],choice.Label,choice.Enabled))choice.Run();
            }
            if(choices.Count==0)Label(340,360,910,65,_ecologyMode==1?"鱼箱里尚无鱼获。钓鱼入箱后，可带回家展示。":_ecologyMode==3?"还没有动物朋友。使用活捉网，或给野外诱捕笼添加诱饵。":"当前没有记录。先从设施建设开始。",19,Muted);
            if(Button(new Rect(320,688,150,35),"上一页",world.Page>0,false,15))world.Page--;
            Label(490,694,150,25,(world.Page+1)+" / "+pages,16,Muted);
            if(Button(new Rect(650,688,150,35),"下一页",world.Page+1<pages,false,15))world.Page++;
            if(_ecologyMode==4 && Button(new Rect(820,688,450,35),"下次放笼目标："+FishingBackpack.AnimalName(world.TrapKind),true,false,15))world.TrapKind=(LandAnimalKind)(((int)world.TrapKind+1)%4);
            Label(320,738,950,26,world.NearHome?"设施与活体在家园操作；笼子须在2.5米内。I / Esc 关闭。":"带回家园后可建设、展示和饲养；F使用网/笼，笼可在野外管理。I / Esc 关闭。",14,Muted);
        }
    }
}
