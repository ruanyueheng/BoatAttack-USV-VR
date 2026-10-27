using System;
namespace BoatAttack.Fishing
{
    [Serializable]
    public sealed class CombatData
    {
        public int guardianWins,nightDefeats;
        public bool guardianRewardPending,nightRewardPending;
        public double guardianCooldown,nightCooldown;
        public CombatData Copy()=>(CombatData)MemberwiseClone();
    }
    public sealed partial class FishingCareer
    {
        private CombatData _combat=new CombatData();
        public int GuardianWins=>_combat.guardianWins;
        public bool GuardianRewardPending=>_combat.guardianRewardPending;
        public bool NightRewardPending=>_combat.nightRewardPending;
        public bool CanChallengeNight=>!NightRewardPending && _combat.nightCooldown<=0 && _combat.nightDefeats<1000000;
        public void MarkNightCreatureDefeated(){if(_combat.nightCooldown<=0 && !_combat.nightRewardPending && _combat.nightDefeats<1000000)_combat.nightRewardPending=true;}
        public bool CanChallengeGuardian=>!GuardianRewardPending && _combat.guardianCooldown<=0 && GuardianWins<1000000;
        public void MarkGuardianDefeated(){if(CanChallengeGuardian)_combat.guardianRewardPending=true;}
        public bool ClaimGuardianReward()
        {
            if(!GuardianRewardPending || GuardianWins>=1000000)return false;
            bool first=GuardianWins==0;int coins=first?120:20;
            if(Coins>100000000-coins)return false;
            var rewards=first?new[]{FishingRecipes.Stack(FieldItem.GuardianPearl,1),FishingRecipes.Stack(FieldItem.ReefChitin,6)}:new[]{FishingRecipes.Stack(FieldItem.ReefChitin,2)};
            if(!Backpack.TryAddBundle(rewards))return false;
            Coins+=coins;_combat.guardianWins++;_combat.guardianRewardPending=false;_combat.guardianCooldown=120;return true;
        }
        public bool ClaimNightCreatureReward()
        {
            if(!_combat.nightRewardPending || _combat.nightCooldown>0 || _combat.nightDefeats>=1000000 || !Backpack.TryAdd(FieldItem.ReefChitin,1))return false;
            _combat.nightDefeats++;_combat.nightRewardPending=false;_combat.nightCooldown=45;return true;
        }
        public void TickCombat(double seconds,bool paused)
        {
            if(paused || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds<=0)return;
            seconds=Math.Min(1,seconds);_combat.guardianCooldown=Math.Max(0,_combat.guardianCooldown-seconds);_combat.nightCooldown=Math.Max(0,_combat.nightCooldown-seconds);
        }
        private bool RestoreCombat(FishingSaveData save)
        {
            var data=save.combat;
            if(save.version<18)
            {
                if(data!=null && (data.guardianWins!=0 || data.nightDefeats!=0 || data.guardianRewardPending || data.nightRewardPending || data.guardianCooldown!=0 || data.nightCooldown!=0))return false;
                foreach(FieldItem item in Enum.GetValues(typeof(FieldItem)))if(item>=FieldItem.ReefChitin &&(Backpack.Count(item)>0 || HomeStorage.Count(item)>0))return false;
                return true;
            }
            if(data==null || data.guardianWins<0 || data.guardianWins>1000000 || data.nightDefeats<0 || data.nightDefeats>1000000
                || double.IsNaN(data.guardianCooldown) || double.IsInfinity(data.guardianCooldown) || data.guardianCooldown<0 || data.guardianCooldown>120
                || double.IsNaN(data.nightCooldown) || double.IsInfinity(data.nightCooldown) || data.nightCooldown<0 || data.nightCooldown>45
                || (data.guardianWins==0 && data.guardianCooldown!=0) || (data.nightDefeats==0 && data.nightCooldown!=0)
                || (data.nightRewardPending && data.nightCooldown!=0) || (data.guardianRewardPending && data.guardianCooldown!=0))return false;
            _combat=data.Copy();return true;
        }
    }
}
