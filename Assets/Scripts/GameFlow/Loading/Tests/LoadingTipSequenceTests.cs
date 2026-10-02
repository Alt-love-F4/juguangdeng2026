using NUnit.Framework;

namespace Spotlight.GameFlow.Tests
{
    public sealed class LoadingTipSequenceTests
    {
        [Test]
        public void PreviousRestoresTheTipThatWasShownBeforeNext()
        {
            var sequence = new LoadingTipSequence(new[] { "A", "B", "C" }, 7);
            string first = sequence.CurrentTip;
            string second = sequence.Next();

            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(sequence.Previous(), Is.EqualTo(first));
            Assert.That(sequence.Next(), Is.EqualTo(second));
        }

        [Test]
        public void TickUsesUnscaledSecondsAndCarriesRemainder()
        {
            var sequence = new LoadingTipSequence(new[] { "A", "B", "C" }, 11);
            string first = sequence.CurrentTip;

            Assert.That(sequence.Tick(3.9f), Is.False);
            Assert.That(sequence.CurrentTip, Is.EqualTo(first));
            Assert.That(sequence.Tick(0.2f), Is.True);
            string second = sequence.CurrentTip;
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(sequence.Tick(3.8f), Is.False);
            Assert.That(sequence.Tick(0.2f), Is.True);
            Assert.That(sequence.CurrentTip, Is.Not.EqualTo(second));
        }

        [Test]
        public void EmptyAndNullTipsAreSafe()
        {
            var sequence = new LoadingTipSequence(new[] { "A", null, "B" }, 3);
            Assert.That(sequence.Count, Is.EqualTo(2));
            Assert.That(sequence.Previous(), Is.EqualTo(sequence.CurrentTip));
            Assert.That(sequence.Tick(-1f), Is.False);

            var empty = new LoadingTipSequence(null);
            Assert.That(empty.CurrentTip, Is.Empty);
            Assert.That(empty.Next(), Is.Empty);
            Assert.That(empty.Tick(10f), Is.False);
        }
    }
}
