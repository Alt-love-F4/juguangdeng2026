namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        private bool SameCustomerTable(string first, string second)
        {
            if (first == second) return true;
            int count = Catalog.customerTableCount;
            return count > 0 && first != null && second != null
                && first.StartsWith("customer_", System.StringComparison.Ordinal)
                && second.StartsWith("customer_", System.StringComparison.Ordinal)
                && int.TryParse(first.Substring(9), out int firstIndex) && int.TryParse(second.Substring(9), out int secondIndex)
                && firstIndex >= 0 && secondIndex >= 0 && firstIndex < count * 2 && secondIndex < count * 2
                && firstIndex % count == secondIndex % count;
        }

        private void PlaceCustomerAtSeat(WorldEntity customer, int slot, bool randomSide)
        {
            int count = Catalog.customerTableCount;
            customer.alternateSeatSide = randomSide && count > 0 && NextRandom() % 2 != 0;
            int physicalSlot = customer.alternateSeatSide ? (slot + count) % (count * 2) : slot;
            var position = CustomerSeat(physicalSlot);
            customer.x = position.x;
            customer.y = position.y;
        }

        private CafeSeatPosition CustomerSeat(int index)
        {
            var seats = Catalog.customerSeats;
            if (seats != null && index >= 0 && index < seats.Length && seats[index] != null)
                return seats[index];
            return new CafeSeatPosition { x = -6 + (index % 4) * 3.5f, y = .5f - (index / 4) * 2.5f };
        }
    }
}
