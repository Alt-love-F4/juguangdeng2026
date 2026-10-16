namespace Spotlight.FreeCafe
{
    /// <summary>Immutable presentation snapshot captured around the existing delivery transaction.</summary>
    public readonly struct CafeDeliveryReceipt
    {
        public readonly string product, customer, message;
        public readonly ProductionQuality quality;
        public readonly int money, reputation;
        public readonly CafeFeedbackKind feedback;
        public int Grade => quality.total < 40 ? 0 : quality.total < 60 ? 1 : quality.total < 80 ? 2 : 3;
        public string GradeLabel => Grade == 0 ? "糟糕" : Grade == 1 ? "一般" : Grade == 2 ? "良好" : "精品";
        public CafeDeliveryReceipt(string product, string customer, ProductionQuality quality, int money,
            int reputation, CafeFeedbackKind feedback, string message)
        {
            this.product = product; this.customer = customer; this.quality = quality;
            this.money = money; this.reputation = reputation; this.feedback = feedback; this.message = message;
        }
    }
}
