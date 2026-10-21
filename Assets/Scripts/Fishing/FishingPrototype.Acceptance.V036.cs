using System;
using System.Collections;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        private Coroutine _v036Fixture;
        private bool _v036FixtureReady;
        public bool AcceptanceFixturePreparing=>_v036Fixture!=null;

        private void CancelV036AcceptanceFixture()
        {
            if(_v036Fixture!=null)StopCoroutine(_v036Fixture);
            _v036Fixture=null;_v036FixtureReady=false;
            if(Home!=null && Home.TelevisionOn)Home.ToggleTelevision();
        }
        private void BeginV036AcceptanceFixture(FishingAcceptanceCase selected)
        {
            if(AcceptanceOptions==null || Acceptance==null || !FishingAcceptanceCases.IsV036Case(selected))
                throw new InvalidOperationException("V036 fixtures require the explicit isolated acceptance profile.");
            _v036Fixture=StartCoroutine(PrepareV036AcceptanceFixture(selected));
        }
        private IEnumerator PrepareV036AcceptanceFixture(FishingAcceptanceCase selected)
        {
            try
            {
                // Let profile-bound ground fruit and the one persistent can reset before touching either.
                yield return null;yield return null;
                if(selected==FishingAcceptanceCase.PortalEntrance || selected==FishingAcceptanceCase.PortalExit)
                {
                    Teleport(FishingIslandWorld.Berth(0),Quaternion.identity);
                    if(!Shore.TryDisembark() || !Shore.Relocate(Realms.PortalApproach(0),Quaternion.identity))
                        throw new InvalidOperationException("Portal fixture cannot reach the island entrance.");
                    if(selected==FishingAcceptanceCase.PortalExit)
                    {
                        if(!Realms.Enter(0))throw new InvalidOperationException("Portal fixture cannot use the production entrance.");
                        while(Realms.Transitioning)yield return null;
                        var exit=Realms.ExitPortal(0);
                        // Isolated camera fixture: terminal walkability and the complete route have their own runtime suite.
                        if(!Shore.Relocate(exit.position+exit.forward*1.4f,Quaternion.LookRotation(-exit.forward)))
                            throw new InvalidOperationException("Portal fixture terminal footing is obstructed.");
                    }
                    var portal=selected==FishingAcceptanceCase.PortalEntrance?Realms.Portal(0):Realms.ExitPortal(0);
                    yield return null;AimV036AcceptanceCamera(portal.position+Vector3.up*1.55f);
                }
                else
                {
                    PrepareAcceptanceHome(FishingAcceptanceCase.HomeInterior);yield return null;yield return null;
                    if(selected==FishingAcceptanceCase.HomeStorage)
                    {
                        if(!Shore.Relocate(Home.Origin+new Vector3(3.1f,.2f,-1.35f),Quaternion.Euler(0,90,0)))
                            throw new InvalidOperationException("Storage fixture footing is obstructed.");
                        yield return null;AimV036AcceptanceCamera(Home.InteractionPoint(6));
                        if(!Home.InteractAt(6) || !InventoryOpen || !Home.TransferStorage(true,1) || !Home.TransferStorage(false,2))
                            throw new InvalidOperationException("Storage fixture failed the real open, deposit and withdraw interactions.");
                    }
                    else if(selected==FishingAcceptanceCase.SofaTelevision)
                    {
                        if(!Shore.Relocate(Home.LoungeStance,Quaternion.Euler(0,180,0)))throw new InvalidOperationException("Sofa fixture footing is obstructed.");
                        yield return null;AimV036AcceptanceCamera(Home.InteractionPoint(4));
                        if(!Home.InteractAt(4))throw new InvalidOperationException("Sofa fixture cannot begin the production sitting transition.");
                        if(!Home.TelevisionOn)Home.ToggleTelevision();
                        for(float age=0;age<1.35f;age+=Mathf.Min(Time.unscaledDeltaTime,.05f))yield return null;
                    }
                    else if(selected==FishingAcceptanceCase.OrchardFruit)
                    {
                        var tree=Home.OrchardTreeRoot(0);
                        if(!Shore.Relocate(tree.position+Vector3.back*1.55f,Quaternion.identity))throw new InvalidOperationException("Orchard fixture footing is obstructed.");
                        yield return null;yield return null;AimV036AcceptanceCamera(tree.position+Vector3.up*1.1f);
                        if(!Home.InteractAt(7))throw new InvalidOperationException("Orchard fixture cannot use the production shake interaction.");
                        float deadline=Time.realtimeSinceStartup+12;
                        while(!Home.OrchardFruitLanded(0) && Time.realtimeSinceStartup<deadline)yield return null;
                        if(!Home.OrchardFruitLanded(0))throw new InvalidOperationException("Orchard fruit never reached the ground.");
                        AimV036AcceptanceCamera(Home.OrchardFruit(0).position+Vector3.up*.10f);
                    }
                    else
                    {
                        var approach=ApproachV036Farm(15);while(approach.MoveNext())yield return approach.Current;
                        if(!RequestWorldInteraction())throw new InvalidOperationException("Watering fixture cannot pick up the actual well-side can.");
                        float deadline=Time.realtimeSinceStartup+12;
                        while(Farm.Busy && Time.realtimeSinceStartup<deadline)yield return null;
                        if(Farm.Busy || !Farm.CarryingCan)throw new InvalidOperationException("Watering fixture pickup never completed.");
                        int target=selected==FishingAcceptanceCase.WellRefill?12:0;
                        approach=ApproachV036Farm(target);while(approach.MoveNext())yield return approach.Current;
                        if(selected!=FishingAcceptanceCase.WateringCarry)
                        {
                            if(!RequestWorldInteraction())throw new InvalidOperationException("Watering fixture cannot start its actual well or soil action.");
                            float phase=selected==FishingAcceptanceCase.WellRefill?.62f:.48f;deadline=Time.realtimeSinceStartup+12;
                            while(Farm.Busy && Farm.WorkProgress<phase && Time.realtimeSinceStartup<deadline)yield return null;
                            if(!Farm.Busy || Farm.WorkProgress<phase)throw new InvalidOperationException("Watering fixture missed its visible pouring phase.");
                        }
                    }
                }
                Notice(string.Empty,0);PresentCharacterFrame();Save();
                if(SaveWarning.Length!=0)throw new InvalidOperationException("V036 fixture could not save its isolated profile.");
                _v036FixtureReady=true;
            }
            finally{_v036Fixture=null;}
        }
        private IEnumerator ApproachV036Farm(int id)
        {
            var p=Farm.Target(id).position+(id==15?Vector3.left*.65f:Vector3.back*(id<12?1.1f:1.3f));p.y=Home.Origin.y;if(id<12)p.x-=.25f;
            if(!Shore.Relocate(p,Quaternion.identity))throw new InvalidOperationException("Watering fixture has no supported stance at target "+id);
            for(int frame=0;frame<4;frame++){Shore.Move(Vector2.zero,false,false,false,.05f);yield return null;}
            AimV036AcceptanceCamera(Farm.Aim(id));yield return null;
            if(!Farm.TryTarget(out int found,out _) || found!=id)throw new InvalidOperationException("Watering fixture production targeting selected the wrong object: "+id);
        }
        private void AimV036AcceptanceCamera(Vector3 target)
        {
            View.ResetLook();PresentCharacterFrame();
            var direction=(target-viewCamera.transform.position).normalized;
            float yaw=Mathf.DeltaAngle(Shore.Actor.eulerAngles.y,Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg);
            float pitch=-Mathf.Asin(Mathf.Clamp(direction.y,-1,1))*Mathf.Rad2Deg;
            float scale=.12f*Mathf.Clamp(MouseSensitivity,.25f,2.5f);
            View.ApplyShoreLookDelta(new Vector2(yaw/scale,-pitch/(scale*(InvertLook?-1:1))));View.ApplyView();
        }
        internal void ValidateV036AcceptanceFixture(FishingAcceptanceCase selected)
        {
            if(!FishingAcceptanceCases.IsV036Case(selected))return;
            if(!_v036FixtureReady || AcceptanceFixturePreparing || !Ashore || !FishingCareer.TryRestore(Career.Snapshot(),out _))
                throw new InvalidOperationException("V036 fixture is incomplete or has invalid saved progress: "+selected);
            if(selected==FishingAcceptanceCase.HomeStorage && (!InventoryOpen || Career.Backpack.Count(FieldItem.IslandFruit)!=3 || Career.HomeStorage.Count(FieldItem.Stone)!=18 || Career.HomeStorage.Count(FieldItem.Rope)!=6 || Career.HomeStorage.Count(FieldItem.IslandFruit)!=0))
                throw new InvalidOperationException("Storage capture lacks the conserved two-way transfer and real dual-inventory UI.");
            if(selected==FishingAcceptanceCase.SofaTelevision && (!Home.Lounging || !Home.TelevisionOn || !StoryBlocksGameplay || Home.LoungeEyeOffset.sqrMagnitude<.01f))
                throw new InvalidOperationException("Sofa capture lacks its original rig seat, camera or actual film.");
            if(selected>=FishingAcceptanceCase.WateringCarry && selected<=FishingAcceptanceCase.WateringPour)
            {
                if(!Farm.CarryingCan || !Farm.WateringCan.gameObject.activeInHierarchy || Career.Farm.Water!=11 || Career.Farm.Plot(0).wateredDay>=0)
                    throw new InvalidOperationException("Watering capture lost its real held can or prematurely committed water.");
                if(selected==FishingAcceptanceCase.WateringCarry?Farm.Busy:!Farm.Busy || !Farm.WaterStreamVisible)
                    throw new InvalidOperationException("Watering capture lacks the expected carried / bucket / stream presentation.");
            }
            if(selected==FishingAcceptanceCase.OrchardFruit && (!Home.OrchardFruitLanded(0) || !Home.OrchardFruit(0).gameObject.activeInHierarchy || !Career.CanHarvestHomeFruit(0) || Career.Backpack.Count(FieldItem.IslandFruit)!=0))
                throw new InvalidOperationException("Orchard capture must show uncollected fruit on the ground without an inventory reward.");
            if(selected==FishingAcceptanceCase.PortalEntrance || selected==FishingAcceptanceCase.PortalExit)
            {
                int expected=selected==FishingAcceptanceCase.PortalEntrance?8:4;
                if(Realms.Inside!=(expected==4) || !Realms.TryTarget(out int id,out var portal) || id!=expected || portal.GetComponentInChildren<FishingPortalVisual>()==null)
                    throw new InvalidOperationException("Portal capture lacks a reachable production light-curtain entrance or terminal exit.");
            }
        }
    }
}
