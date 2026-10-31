using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        public const int HandAlignmentPage=35,QuitPage=36,CreativePage=37;
        /// <summary>Pulls a world panel in front of scenery, never closer than 0.8 m.</summary>
        private static float ClearDistance(Vector3 eye,Vector3 direction,float distance)
        {
            if(Physics.SphereCast(eye,.1f,direction,out var hit,distance+.1f,~((1<<4)|(1<<5)),QueryTriggerInteraction.Ignore))distance=Mathf.Clamp(hit.distance-.1f,.8f,distance);
            return distance;
        }
        private void RefreshComfortPage()
        {
            _status.fontSize=21;
            _status.text="VR 舒适 · 防晕设置（自动保存）\n头晕时：分段转向 + 瞬移 + 视野收窄 强\n船体摇晃不会带动头部；过场只淡入淡出\n"+_c.VisibleMessage;
            Set(0,"转向方式\n"+FishingXRComfort.TurnName+"\n点击切换",()=>{FishingXRComfort.TurnMode=(FishingXRComfort.Turn)(((int)FishingXRComfort.TurnMode+1)%3);Refresh();});
            Set(1,"移动方式\n"+(FishingXRComfort.Teleport?"瞬移（左摇杆前推）":"平滑移动")+"\n点击切换",()=>{FishingXRComfort.Teleport=!FishingXRComfort.Teleport;Refresh();});
            Set(2,"平滑移动速度\n"+FishingXRComfort.SpeedName+"\n点击切换",()=>{FishingXRComfort.SpeedLevel=(FishingXRComfort.SpeedLevel+1)%3;Refresh();});
            Set(3,"移动时视野收窄\n"+FishingXRComfort.VignetteName+"\n点击切换",()=>{FishingXRComfort.Vignette=(FishingXRComfort.Vignette+1)%4;Refresh();});
            Set(4,"舒适航行\n"+(_c.ComfortMode?"开启：船速 65% · 起伏衰减":"标准动力"),()=>{_c.ToggleComfort();Refresh();});
            Set(5,"秘境阵风\n"+(FishingXRComfort.GustComfort?"舒适：不推动身体":"慢速推动"),()=>{FishingXRComfort.GustComfort=!FishingXRComfort.GustComfort;Refresh();});
            Set(6,"校准眼高",()=>{_c.XRSeat.Recenter();Close();});
            Set(7,"操作说明\n各情境按键表",()=>Open(23));Set(8,"返回设置",()=>Open(14));
        }
        private void RefreshHandAlignment()
        {
            _status.text="手部倾角对齐（自动保存）\n左手："+FishingHands.ControllerRoll(true).ToString("0")+"°\n右手："+FishingHands.ControllerRoll(false).ToString("0")+"°\n按身体前后方向修正左右侧倾。\n加减角度可朝相反方向修正。\n放松手腕，调整到虚拟手与现实手一致。\n校准时可收起手册观察手和持握工具。";
            Set(0,"左手 +5°",()=>{FishingHands.SetControllerRoll(true,FishingHands.ControllerRoll(true)+5);Refresh();});
            Set(1,"左手 -5°",()=>{FishingHands.SetControllerRoll(true,FishingHands.ControllerRoll(true)-5);Refresh();});
            Set(2,"右手 +5°",()=>{FishingHands.SetControllerRoll(false,FishingHands.ControllerRoll(false)+5);Refresh();});
            Set(3,"右手 -5°",()=>{FishingHands.SetControllerRoll(false,FishingHands.ControllerRoll(false)-5);Refresh();});
            Set(4,"恢复默认 +30°",()=>{FishingHands.SetControllerRoll(true,30);FishingHands.SetControllerRoll(false,30);Refresh();});
            Set(5,"清零倾角补偿",()=>{FishingHands.SetControllerRoll(true,0);FishingHands.SetControllerRoll(false,0);Refresh();});
            Set(6,"校准眼高",()=>{_c.XRSeat.Recenter();Close();});
            Set(7,"退出游戏",()=>Open(QuitPage));Set(8,"返回设置",()=>Open(14));
        }
        private void RefreshControlsPage()
        {
            int count=FishingXRHints.ContextNames.Length;
            _status.fontSize=21;
            _status.text="操作说明 · "+(_offset/6+1)+" / "+((count+5)/6)+"\n右手 A / B · 左手 X / Y · 食指扳机 · 中指握把\n扳机指向卡片，左页显示完整按键\n抬起手柄看向它，也能看到当前按键\n完整练习：设置 → 完整手柄教学";
            for(int i=0;i<6;i++)
            {
                int index=_offset+i;if(index>=count)break;
                Set(i,FishingXRHints.Card((FishingXRHints.Context)index),()=>{});
            }
            Pager(count);
        }
    }
}
