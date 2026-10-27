namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingVoyageWorld Voyage { get; private set; }
        private void BuildVoyageWorld()
        {
            if (Voyage != null) return;
            Voyage = gameObject.AddComponent<FishingVoyageWorld>();
            Voyage.Build(this);
        }
    }
}
