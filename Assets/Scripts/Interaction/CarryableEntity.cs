namespace Spotlight.Interaction
{
    public sealed class CarryableEntity : InteractiveEntity
    {
        public ItemState State { get; private set; }
        public override bool CanDrag => true;
        public override bool CanReceive => Definition != null && Definition.HasTag("container");
        public override ItemState Item => State;
        public void Bind(ItemState state) => State = state;
    }
}
