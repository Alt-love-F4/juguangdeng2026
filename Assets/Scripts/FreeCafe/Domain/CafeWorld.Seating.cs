namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        private CafeSeatPosition CustomerSeat(int index)
        {
            var seats = Catalog.customerSeats;
            if (seats != null && index >= 0 && index < seats.Length && seats[index] != null)
                return seats[index];
            return new CafeSeatPosition { x = -6 + (index % 4) * 3.5f, y = .5f - (index / 4) * 2.5f };
        }
    }
}
