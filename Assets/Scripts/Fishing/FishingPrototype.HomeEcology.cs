namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingHomeEcologyWorld HomeEcology { get; private set; }
        public void OpenHomeEcologyPage(){_hud.OpenHomeEcologyPage();}
    }
}
