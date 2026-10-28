namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingKeeperWorld Keeper { get; private set; }
        public FishingTidalFlat TidalFlat { get; private set; }
        private void BuildKeeperWorld()
        {
            Keeper = gameObject.AddComponent<FishingKeeperWorld>(); Keeper.Build(this);
            TidalFlat = gameObject.AddComponent<FishingTidalFlat>(); TidalFlat.Build(this);
        }
    }
}
