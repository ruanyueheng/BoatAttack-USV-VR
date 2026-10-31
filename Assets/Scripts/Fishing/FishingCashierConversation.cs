using System;
using System.Text;
namespace BoatAttack.Fishing
{
    public enum CashierTopic { Welcome, Story, History, Personal, Goods, Upgrades, Crafting, Stations, Tools, Lantern, Living, Gathering, Hunting, Home, Sailing }
    public enum CashierConversationAction { None, BrowseGoods, NavigateHome }
    /// <summary>Text-only branch state; opening a topic never spends coins or grants items.</summary>
    public sealed class FishingCashierConversation
    {
        public CashierTopic Topic { get; private set; }=CashierTopic.Welcome;
        private bool _firstVisit;
        public void Begin(bool firstVisit){Topic=CashierTopic.Welcome;_firstVisit=firstVisit;}
        public string Title=>Topic==CashierTopic.Welcome?"欢迎来到潮汐钓具店":Topic==CashierTopic.Goods?"商品与装备":Topic==CashierTopic.Crafting?"制作与工具":Topic==CashierTopic.Living?"海岛生活":Topic==CashierTopic.Story?"汐禾的小店":Topic==CashierTopic.Stations?"工坊分工":Topic==CashierTopic.Tools?"从石镐到铁镐":Topic==CashierTopic.Lantern?"把灯带上岸":Topic==CashierTopic.Gathering?"材料从哪里来":Topic==CashierTopic.Hunting?"狩猎与回收":Topic==CashierTopic.Home?"回潮渔屋":Topic==CashierTopic.Sailing?"靠岸与返航":Topic==CashierTopic.Upgrades?"钓具与船舶升级":"聊聊汐禾";
        public int ChoiceCount=>Topic==CashierTopic.Welcome || Topic==CashierTopic.Goods || Topic==CashierTopic.Crafting || Topic==CashierTopic.Living?4:3;
        public string Choice(int index)
        {
            if(index<0 || index>=ChoiceCount)return "";
            switch(Topic)
            {
                case CashierTopic.Welcome:return new[]{"聊聊汐禾与小店","介绍商品与装备","制作、采集与狩猎指南","回潮渔屋与海岛生活"}[index];
                case CashierTopic.Goods:return new[]{"查看商品与价格","钓具和船舶如何升级","猎枪与狩猎装备","回到开场话题"}[index];
                case CashierTopic.Crafting:return new[]{"三种工坊怎么分工","如何制作石镐和铁镐","如何做会发光的灯笼","回到开场话题"}[index];
                case CashierTopic.Living:return new[]{"材料和动物在哪找","怎么回潮渔屋","靠岸与返航建议","回到开场话题"}[index];
                case CashierTopic.Story:return new[]{"这间店的故事","你喜欢这片海什么","回到开场话题"}[index];
                case CashierTopic.Home:return new[]{"给我标出家的泊位","靠岸要注意什么","回到海岛生活"}[index];
                case CashierTopic.Stations:return new[]{"石镐和铁镐的配方","怎么制作灯笼","回到制作话题"}[index];
                case CashierTopic.Tools:return new[]{"查看商品","三种工坊分工","回到制作话题"}[index];
                case CashierTopic.Lantern:return new[]{"材料从哪里来","查看商品","回到制作话题"}[index];
                case CashierTopic.Gathering:return new[]{"狩猎与掉落怎么处理","怎么制作镐子","回到海岛生活"}[index];
                case CashierTopic.Hunting:return new[]{"查看商品","自己组装猎枪","回到开场话题"}[index];
                case CashierTopic.Upgrades:return new[]{"查看商品","先准备哪些工具","回到商品话题"}[index];
                default:return new[]{"查看商品","回潮渔屋在哪里","回到开场话题"}[index];
            }
        }
        public CashierConversationAction Choose(int index)
        {
            if(index<0 || index>=ChoiceCount)return CashierConversationAction.None;
            switch(Topic)
            {
                case CashierTopic.Welcome:Topic=new[]{CashierTopic.Story,CashierTopic.Goods,CashierTopic.Crafting,CashierTopic.Living}[index];break;
                case CashierTopic.Goods:if(index==0)return CashierConversationAction.BrowseGoods;Topic=new[]{CashierTopic.Welcome,CashierTopic.Upgrades,CashierTopic.Hunting,CashierTopic.Welcome}[index];break;
                case CashierTopic.Crafting:Topic=new[]{CashierTopic.Stations,CashierTopic.Tools,CashierTopic.Lantern,CashierTopic.Welcome}[index];break;
                case CashierTopic.Living:Topic=new[]{CashierTopic.Gathering,CashierTopic.Home,CashierTopic.Sailing,CashierTopic.Welcome}[index];break;
                case CashierTopic.Story:Topic=new[]{CashierTopic.History,CashierTopic.Personal,CashierTopic.Welcome}[index];break;
                case CashierTopic.Home:if(index==0)return CashierConversationAction.NavigateHome;Topic=index==1?CashierTopic.Sailing:CashierTopic.Living;break;
                case CashierTopic.Stations:Topic=new[]{CashierTopic.Tools,CashierTopic.Lantern,CashierTopic.Crafting}[index];break;
                case CashierTopic.Tools:if(index==0)return CashierConversationAction.BrowseGoods;Topic=index==1?CashierTopic.Stations:CashierTopic.Crafting;break;
                case CashierTopic.Lantern:if(index==1)return CashierConversationAction.BrowseGoods;Topic=index==0?CashierTopic.Gathering:CashierTopic.Crafting;break;
                case CashierTopic.Gathering:Topic=new[]{CashierTopic.Hunting,CashierTopic.Tools,CashierTopic.Living}[index];break;
                case CashierTopic.Hunting:if(index==0)return CashierConversationAction.BrowseGoods;Topic=index==1?CashierTopic.Stations:CashierTopic.Welcome;break;
                case CashierTopic.Upgrades:if(index==0)return CashierConversationAction.BrowseGoods;Topic=index==1?CashierTopic.Tools:CashierTopic.Goods;break;
                default:if(index==0)return CashierConversationAction.BrowseGoods;Topic=index==1?CashierTopic.Home:CashierTopic.Welcome;break;
            }
            return CashierConversationAction.None;
        }
        public string Speech(FishingCareer career,bool xr)
        {
            string use=xr?"B 打开背包，点选换入快捷位；Y 换装备，右扳机使用。":"I 打开背包，数字键换装备，F 使用工具。";
            switch(Topic)
            {
                case CashierTopic.Welcome:return (_firstVisit?"你好，我是汐禾。第一次靠岸吧？欢迎来潮汐钓具店。":"又见面啦，领航员。海风有没有把你想找的东西送来？")+"\n\n"+(career.Calendar.Night?"天色暗了，记得带灯笼上岸。":"今天适合把工具备齐，再慢慢逛海湾。")+"\n\n想看看商品，还是听我讲讲采集和制作？";
                case CashierTopic.Goods:return "钓竿、鱼线和卷线器可以升级，船舶也有载重、动力与操控升级。店里还有镐、猎矛、望远镜、提灯、制作材料和远航口粮。\n\n你现在有 "+career.Coins+" 金币。看商品不会扣款，只有确认购买才付款；满背包或余额不足会拒绝交易。\n\n"+use;
                case CashierTopic.Upgrades:return "钓具升级帮助你控制鱼线；船舶升级让航行和装载更顺手。二级载重、动力、操控专精三选一，先想好自己的玩法。\n\n刚上岸可以先做石镐，不必把金币全花掉。价格和已有等级都能在商品页看到。";
                case CashierTopic.Crafting:return "工坊有三种设施，别把它们混用：熔炉炼材料，锻造台打金属零件，工作台组装装备。\n\n木棍不用去地上找。漂流木直接能做，选“随身加工”就行。你想先准备镐子，还是给夜里做一盏灯？";
                case CashierTopic.Stations:return "熔炉：铁矿石配合燃料，计时炼成铁锭；也能烧木炭、玻璃、砖和烤肉。\n\n锻造台：铁锭塑形成铁镐头和枪械铁件。\n\n工作台：装木柄、绑绳索，组装铁镐、猎枪、灯笼等装备。每个设施只显示自己的配方，基础材料在“随身加工”。";
                case CashierTopic.Tools:return "先用漂流木做木棍，纤维做绳索，再到工作台：\n"+Recipe("stone-pick")+"\n\n持石镐开采矿石，熔炉炼铁，锻造台打铁镐头，最后回工作台：\n"+Recipe("iron-pick")+"\n\n"+use;
                case CashierTopic.Lantern:return "夜里带一盏灯，礁石和地上的材料会好找一些。铁矿炼成铁锭做灯框，砂烧成玻璃做灯罩，木炭供灯芯使用；皮毛留作家园织物。\n\n回工作台组装：\n"+Recipe("lantern")+"。\n\n"+(xr?"提灯放在背包里便会挂到右侧腰间，左扳机随时开关。右手靠近提环，握住右握把提起，松手挂回。":"L 随时开关腰间提灯；选中提灯也可按 F。")+"换工具、采集时仍会继续照路。";
                case CashierTopic.Gathering:return "岸边可以捡漂流木、石料和纤维；低处材料要靠近，必要时蹲下触及。煤点在山脚，煤和铁矿需要持镐采集。\n\n"+(xr?"右手贴近材料后按右 A；镐尖接触矿石后挥动或按右扳机。":"E 互动，Ctrl 蹲下。")+"\n\n漂流木做木棍，别为了找现成木棍在海滩上绕圈啦。";
                case CashierTopic.Hunting:return "猎枪在店里售价 "+FishingCareer.HuntingRiflePrice+" 金币，也能自己制作：熔炉炼铁，锻造台做枪械铁件，再到工作台组装。\n\n兔子、狐狸、龟类和海鸥可被命中。回收后得到对应肉类、兔毛、狐皮、龟壳或羽毛；满背包时遗体保留，拾取后清理，约五分钟活动时间后刷新活体。\n\n"+(xr?"Y 选猎枪，握住右扳机连射，抬枪瞄准。":"选择猎枪，左键长按连射，右键瞄准，F 单发。")+"别隔着墙开枪。";
                case CashierTopic.Home:return "回潮渔屋已经移到港口西侧的独立水面，不在旧店旁了。\n\n"+(xr?"B 打开手册，在航海图选择“家”的泊位。":"M 取出全域航海图，在右侧目的地选择“回潮渔屋”。")+"低速靠近泊位，下船后沿栈道进屋，床边可休息到清晨。\n\n要不要我现在给你标出家的航点？";
                case CashierTopic.Sailing:return "先减速，再靠岸；别急着把船往地基下面挤。\n\n"+(xr?"左扳机刹车，左摇杆后拉倒航；低速按左 X 下船。手册航海图可选择泊位。":"Shift 刹车，S 倒航；航速降到 5.4 公里每小时以下再 G 下船。")+"\n\n回潮渔屋有自己的泊位。需要救援时，"+(xr?"B → 设置 → 序章 / 环境特效 / 救援可返回港口。":"R 返回港口。")+"鱼获和进度会保留。";
                case CashierTopic.Story:return "这家店不算大，却见过好多次出航和归来。\n\n我喜欢听客人说今天在哪片海湾找到新东西，也喜欢把旧工具整理得还能再用一次。想听店里的故事，还是聊聊这片海？";
                case CashierTopic.History:return "这里最早只修渔网。风暴过后，岛上的人带来绳索、木料和灯，才慢慢变成现在的钓具店。\n\n我留下来，是想让准备出发的人都能找到需要的东西，也让晚归的人知道这里还有人等着开门。";
                default:return "我喜欢傍晚的海面，也喜欢柜台上刚整理好的那一排灯。\n\n冒险当然重要，不过累的时候，回屋歇一歇也很好。你下次来，可以告诉我今天又发现了什么。";
            }
        }
        private static string Recipe(string id)
        {
            var recipe=FishingRecipes.Find(id);if(recipe==null)return "配方暂不可用";
            var text=new StringBuilder();for(int i=0;i<recipe.Costs.Length;i++){if(i>0)text.Append(" + ");text.Append(FishingBackpack.Name(recipe.Costs[i].item)).Append(" ×").Append(recipe.Costs[i].count);}
            return text.Append(" → ").Append(FishingBackpack.Name(recipe.Output)).Append(" ×").Append(recipe.Count).ToString();
        }
    }
}
