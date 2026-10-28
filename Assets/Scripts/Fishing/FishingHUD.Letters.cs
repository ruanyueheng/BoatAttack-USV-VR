using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private int _letterPage;
        /// <summary>Journal "信件": drift-bottle letters, replies and market keepsake cards; click to reread.</summary>
        private void DrawLettersPage(Rect r)
        {
            var c=Controller;var letters=FishingSeaMarket.Archive(c.Career.Lore);
            const int perPage=7;int pages=Mathf.Max(1,Mathf.CeilToInt(letters.Count/(float)perPage));_letterPage=Mathf.Clamp(_letterPage,0,pages-1);
            if(letters.Count==0){Label(new Rect(r.x,r.y+120,r.width,60),"还没有收到信件。\n在海上拾起漂流瓶，或放流一封信等待回音。",19,Muted,false,TextAnchor.UpperCenter);return;}
            for(int i=0;i<perPage;i++)
            {
                int index=_letterPage*perPage+i;if(index>=letters.Count)break;var letter=letters[index];
                if(RowCard(new Rect(r.x,r.y+i*64,r.width,56),7,letter.title+" · "+letter.sender,c.Letter.CanRead))
                {c.ToggleJournal();c.Letter.Show(letter.title,letter.sender,letter.body);return;}
            }
            float y=r.yMax-40;
            if(Button(new Rect(r.x,y,130,34),"上一页",_letterPage>0,false,15))_letterPage--;
            Label(new Rect(r.x+140,y+6,120,24),Number(_letterPage+1)+" / "+Number(pages),16,Muted,false,TextAnchor.UpperCenter);
            if(Button(new Rect(r.x+270,y,130,34),"下一页",_letterPage+1<pages,false,15))_letterPage++;
            Label(new Rect(r.x+420,y+8,r.width-420,22),"点击信件重读 · 共 "+Number(letters.Count)+" 封",14,Muted,false,TextAnchor.UpperRight);
        }
    }
}
