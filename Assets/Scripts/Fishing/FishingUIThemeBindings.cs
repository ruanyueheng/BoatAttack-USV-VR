using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.Fishing
{
    /// <summary>Static notebook decorations follow the same palette as the live button states.</summary>
    public sealed class FishingUIThemeBindings : MonoBehaviour
    {
        private readonly List<Graphic> _graphics=new List<Graphic>();
        private readonly List<int> _roles=new List<int>();
        private float _amount=-1;
        public void Add(Graphic graphic,int role)
        {
            int index=_graphics.IndexOf(graphic);
            if(index>=0)_roles[index]=role;else{_graphics.Add(graphic);_roles.Add(role);}
        }
        private void LateUpdate()
        {
            if(_amount==FishingUITheme.NightAmount)return;_amount=FishingUITheme.NightAmount;
            for(int i=0;i<_graphics.Count;i++)if(_graphics[i]!=null)_graphics[i].color=FishingUITheme.Tone(_roles[i]);
        }
    }
}
