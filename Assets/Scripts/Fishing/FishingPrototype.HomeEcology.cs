namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingHomeEcologyWorld HomeEcology { get; private set; }
        public void OpenHomeEcologyPage(int mode=0){_hud.OpenHomeEcologyPage(mode);}
    }
}
