using System;
using BoatAttack.Fishing;
internal static class CombatTests
{
    public static void Run(Action<bool,string> check)
    {
        var c=new FishingCareer();int coins=c.Coins;
        check(!c.ClaimGuardianReward(),"Guardian reward requires a pending defeat");
        c.MarkGuardianDefeated();c.MarkGuardianDefeated();
        check(c.GuardianRewardPending && c.ClaimGuardianReward() && c.GuardianWins==1 && c.Coins==coins+120,"Guardian first reward commits once");
        check(c.Backpack.Count(FieldItem.GuardianPearl)==1 && c.Backpack.Count(FieldItem.ReefChitin)==6 && !c.ClaimGuardianReward(),"Guardian first bundle is unique");
        c.TickCombat(10000,false);check(!c.CanChallengeGuardian,"Combat frame delta is capped");
        for(int i=0;i<130;i++)c.TickCombat(1,true);check(!c.CanChallengeGuardian,"Combat cooldown pauses");
        for(int i=0;i<119;i++)c.TickCombat(1,false);check(c.CanChallengeGuardian,"Guardian cooldown expires with active time");
        c.MarkGuardianDefeated();check(c.ClaimGuardianReward() && c.GuardianWins==2 && c.Backpack.Count(FieldItem.GuardianPearl)==1,"Repeat guardian win does not duplicate pearl");
        c.MarkNightCreatureDefeated();check(c.ClaimNightCreatureReward() && !c.ClaimNightCreatureReward(),"Night creature cooldown gates duplicate loot");
        var data=c.Snapshot();check(FishingCareer.TryRestore(data,out var restored) && restored.GuardianWins==2,"Combat receipts round trip");
        data.combat.guardianWins=99;check(restored.GuardianWins==2,"Combat restore clones visitor DTO");
        var old=new FishingCareer().Snapshot();Program.Legacy(old,17);check(FishingCareer.TryRestore(old,out var migrated) && migrated.GuardianWins==0,"Version17 migrates pristine combat");
        old.combat.guardianWins=1;check(!FishingCareer.TryRestore(old,out _),"Version17 rejects future combat receipts");
        old=new FishingCareer().Snapshot();old.combat.guardianCooldown=double.NaN;check(!FishingCareer.TryRestore(old,out _),"Combat save rejects NaN time");
        var full=new FishingCareer();while(full.Backpack.TryAdd(FieldItem.Stone,64)){}full.MarkGuardianDefeated();coins=full.Coins;
        check(!full.ClaimGuardianReward() && full.GuardianRewardPending && full.GuardianWins==0 && full.Coins==coins,"Full backpack preserves pending guardian reward without coins");
        full.MarkNightCreatureDefeated();check(!full.ClaimNightCreatureReward() && full.NightRewardPending,"Full backpack retains pending night loot");
        var round=full.Snapshot();check(FishingCareer.TryRestore(round,out restored) && restored.GuardianRewardPending && restored.NightRewardPending,"Pending guardian reward survives reload");
    }
}
