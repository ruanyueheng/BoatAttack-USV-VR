namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingVoyageWorld Voyage { get; private set; }
        public FishingSeaEventWorld SeaEvents { get; private set; }
        public FishingSeaMarketWorld SeaMarket { get; private set; }
        private void BuildVoyageWorld()
        {
            if (Voyage != null) return;
            Voyage = gameObject.AddComponent<FishingVoyageWorld>();
            Voyage.Build(this);
            SeaEvents = gameObject.AddComponent<FishingSeaEventWorld>();
            SeaEvents.Build(this);
            SeaMarket = gameObject.AddComponent<FishingSeaMarketWorld>();
            SeaMarket.Build(this);
        }
    }
}
