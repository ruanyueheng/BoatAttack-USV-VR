using System;
using System.Collections.Generic;
using BoatAttack.Fishing;
internal static class CashierConversationTests
{
    public static void Run(Action<bool,string> check)
    {
        var career=new FishingCareer();var dialogue=new FishingCashierConversation();dialogue.Begin(true);
        check(dialogue.ChoiceCount==4 && dialogue.Speech(career,false).Contains("第一次"),"Cashier has four introductory branches and first-visit greeting");
        dialogue.Choose(-1);dialogue.Choose(4);
        check(dialogue.Topic==CashierTopic.Welcome && dialogue.Choice(-1)=="" && dialogue.Choice(4)=="","Cashier invalid choices do not change branch state");
        dialogue.Begin(false);check(dialogue.Speech(career,false).Contains("又见面"),"Cashier repeat visit greeting differs without new save schema");
        career.Calendar.AdvanceToHour(21);check(dialogue.Speech(career,false).Contains("天色暗"),"Cashier night greeting reminds player to carry a lamp");
        dialogue.Choose(1);var goods=dialogue.Speech(career,false);
        check(goods.Contains("钓竿") && goods.Contains("望远镜") && goods.Contains("制作材料"),"Cashier legacy equipment introduction remains compatible");
        check(dialogue.Choose(0)==CashierConversationAction.BrowseGoods,"Cashier shopping branch delegates a browsing action, not a purchase");
        dialogue.Begin(false);dialogue.Choose(2);dialogue.Choose(0);
        check(dialogue.Topic==CashierTopic.Stations && dialogue.Speech(career,false).Contains("铁镐头") && dialogue.Speech(career,false).Contains("枪械铁件"),"Cashier accurately explains current furnace/forge/workbench separation");
        dialogue.Choose(0);check(dialogue.Speech(career,false).Contains("铁镐头 ×1") && dialogue.Speech(career,false).Contains("木棍 ×2"),"Cashier tool costs derive from current production recipe data");
        dialogue.Begin(false);dialogue.Choose(2);dialogue.Choose(2);
        check(dialogue.Topic==CashierTopic.Lantern && dialogue.Speech(career,false).Contains("铁锭 ×2") && dialogue.Speech(career,false).Contains("玻璃 ×2") && dialogue.Speech(career,false).Contains("木炭 ×1"),"Cashier lamp recipe derives its real metal, glass and charcoal inputs");
        check(dialogue.Speech(career,false).Contains("F 点亮") && dialogue.Speech(career,true).Contains("右扳机"),"Cashier controls are specific to desktop or Pico");
        check(dialogue.Speech(career,true).Contains("腰间提灯") && dialogue.Speech(career,true).Contains("右握把") && dialogue.Speech(career,true).Contains("松手挂回"),"Cashier explains lifting and returning the actual waist lantern");
        dialogue.Begin(false);dialogue.Choose(3);dialogue.Choose(1);
        check(dialogue.Topic==CashierTopic.Home && dialogue.Choose(0)==CashierConversationAction.NavigateHome,"Cashier can request home's actual waypoint");
        dialogue.Choose(1);var sailing=dialogue.Speech(career,true);
        check(sailing.Contains("左 X") && sailing.Contains("左摇杆后拉") && !sailing.Contains("A 下船"),"Cashier sailing instructions match v0.25 controller bindings");
        var queue=new Queue<int[]>();var visited=new HashSet<CashierTopic>();queue.Enqueue(new int[0]);
        while(queue.Count>0)
        {
            var path=queue.Dequeue();var node=new FishingCashierConversation();node.Begin(false);
            foreach(int index in path)node.Choose(index);
            if(!visited.Add(node.Topic))continue;
            check(!string.IsNullOrWhiteSpace(node.Title) && !string.IsNullOrWhiteSpace(node.Speech(career,false)),"Reachable cashier branch has full text: "+node.Topic);
            check(node.ChoiceCount>=3 && node.ChoiceCount<=4,"Cashier branch fits shared desktop/XR choice count: "+node.Topic);
            for(int i=0;i<node.ChoiceCount;i++)
            {
                check(!string.IsNullOrWhiteSpace(node.Choice(i)),"Cashier option label exists: "+node.Topic+"/"+i);
                var next=new int[path.Length+1];Array.Copy(path,next,path.Length);next[next.Length-1]=i;
                var copy=new FishingCashierConversation();copy.Begin(false);foreach(int n in next)copy.Choose(n);
                if(!visited.Contains(copy.Topic))queue.Enqueue(next);
            }
        }
        check(visited.Count==Enum.GetValues(typeof(CashierTopic)).Length,"All cashier dialogue topics are reachable");
        check(career.Coins==0 && career.Backpack.UsedSlots==1 && career.Lore.ClearCount==0,"Reading every dialogue branch grants no currency, materials or quest completion");
    }
}
