using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class SelectedCheckGuardTests
    {
        private const string Root = "church.1020.s1";
        private const string LanguageChoice = Root + ".p044";
        private const string CourageChoice = Root + ".p045";
        private const string LanguageCheck = Root + ".explain.language";
        private const string CourageCheck = Root + ".explain.courage";
        private const string Mapping = LanguageChoice + "=" + LanguageCheck + ";"
            + CourageChoice + "=" + CourageCheck + ";"
            + Root + ".p046=" + Root + ".explain.wisdom;"
            + Root + ".p047=" + Root + ".explain.perception";
        private const string Guard = "selectedCheck:" + Mapping;

        private sealed class HeldPresenter : INarrativePresenter
        {
            public void Play(NarrativeEventRequest request, Action completed) { }
        }

        private NarrativeDebugSession session;
        private NarrativeController Controller => session.Controller;

        [SetUp]
        public void SetUp()
        {
            // Load creates a separate configuration; these prerequisites are fixture-only.
            var config = LubanNarrativeLoader.Load();
            var root = config.events.Single(e => e.eventId == Root);
            root.eventProperties.RemoveAll(p => p.property == "blockedBy");
            root.entry = NarrativeEntryPoint.Immediate;
            session = new NarrativeDebugSession(config, new HeldPresenter());
            session.SetDate(15);
            session.EstablishBaseline();
        }

        [TearDown]
        public void TearDown()
        {
            session?.Dispose();
            session = null;
        }

        private void Start()
        {
            Assert.That(session.RequestEvent(Root), Is.True);
            Assert.That(session.CurrentRequest.Event, Is.Not.Null);
            Assert.That(session.CurrentRequest.Event.eventId, Is.EqualTo(Root));
        }

        [TestCase("p044", "language", 5, false)]
        [TestCase("p044", "language", 6, true)]
        [TestCase("p045", "courage", 5, false)]
        [TestCase("p045", "courage", 6, true)]
        [TestCase("p046", "wisdom", 5, false)]
        [TestCase("p046", "wisdom", 6, true)]
        [TestCase("p047", "perception", 5, false)]
        [TestCase("p047", "perception", 6, true)]
        public void ExplanationChoiceUsesOnlyItsCheckAndSharesSnapshotWithNegation(
            string choice, string attribute, int selectedValue, bool passed)
        {
            Start();
            var reads = new List<string>();
            var snapshots = new List<NarrativeAbilityCheckSnapshot>();
            Controller.AbilityCheckEvaluated += snapshots.Add;
            Controller.AbilityReader = id =>
            {
                reads.Add(id);
                return (true, id == attribute ? selectedValue : selectedValue == 5 ? 6 : 5);
            };
            Assert.That(session.SelectChoice(Root + "." + choice), Is.True);
            Assert.That(reads, Is.Empty, "Selecting the explanation must not evaluate its inline guard.");

            // Negation may be the first page guard encountered, and must populate the same cache.
            Assert.That(Controller.ReadPresentationCondition("not:" + Guard), Is.EqualTo(!passed));
            Assert.That(reads, Is.EqualTo(new[] { attribute }));
            Controller.AbilityReader = _ => throw new InvalidOperationException("A cached guard reread its provider.");
            Assert.That(Controller.ReadPresentationCondition(Guard), Is.EqualTo(passed));
            Assert.That(Controller.ReadPresentationCondition("not:" + Guard), Is.EqualTo(!passed));
            Assert.That(Controller.ReadPresentationCondition("not:not:" + Guard), Is.EqualTo(passed));
            string checkId = Root + ".explain." + attribute;
            Assert.That(Controller.ReadPresentationCondition("check:" + checkId), Is.EqualTo(passed));
            Assert.That(snapshots.Select(s => s.checkId), Is.EqualTo(new[] { checkId }),
                "Only the selected inline check may acquire a snapshot.");
            Assert.That(snapshots.Single().values.Select(v => v.attributeId), Is.EqualTo(new[] { attribute }));
            Assert.That(snapshots.Single().values.Single().currentValue, Is.EqualTo(selectedValue));
            Assert.That(snapshots.Single().values.Single().threshold, Is.EqualTo(6));
            Assert.That(Controller.ActiveAbilityCheck, Is.Null);
            Assert.That(Controller.State.HasHappenedEvent(Root), Is.False);
        }

        [TestCase("p044", "language")]
        [TestCase("p045", "courage")]
        [TestCase("p046", "wisdom")]
        [TestCase("p047", "perception")]
        public void UnselectedAbilityProvidersMayBeUnavailable(string choice, string attribute)
        {
            Start();
            var reads = new List<string>();
            Controller.AbilityReader = id =>
            {
                reads.Add(id);
                return id == attribute ? (true, 6) : (false, 0);
            };
            Assert.That(session.SelectChoice(Root + "." + choice), Is.True);
            Assert.That(Controller.ReadPresentationCondition(Guard), Is.True);
            Assert.That(Controller.ReadPresentationCondition("not:" + Guard), Is.False);
            Assert.That(reads, Is.EqualTo(new[] { attribute }));
        }

        [TestCase("", false)]
        [TestCase("not:", false)]
        [TestCase("", true)]
        [TestCase("not:", true)]
        public void NoMappedChoiceSelectedThrowsWithoutReadingAbilities(string prefix, bool selectOtherGroup)
        {
            Start();
            // A selection in another local group does not satisfy the explanation mapping either.
            if (selectOtherGroup) Assert.That(session.SelectChoice(Root + ".p020"), Is.True);
            int reads = 0;
            Controller.AbilityReader = _ => { reads++; return (true, 6); };
            Assert.Throws<InvalidOperationException>(() => Controller.ReadPresentationCondition(prefix + Guard));
            Assert.That(reads, Is.Zero);
        }

        [TestCase("")]
        [TestCase("not:")]
        public void GuardRequiresAnActivePresentation(string prefix)
        {
            Assert.That(session.CurrentRequest.Event, Is.Null);
            int reads = 0;
            Controller.AbilityReader = _ => { reads++; return (true, 6); };
            Assert.Throws<InvalidOperationException>(() => Controller.ReadPresentationCondition(prefix + Guard));
            Assert.That(reads, Is.Zero);
        }

        [TestCase("")]
        [TestCase(LanguageChoice)]
        [TestCase("=" + LanguageCheck)]
        [TestCase(LanguageChoice + "=")]
        [TestCase(LanguageChoice + "=" + LanguageCheck + "=extra")]
        [TestCase(LanguageChoice + "=" + LanguageCheck + ";")]
        [TestCase(LanguageChoice + "=" + LanguageCheck + ";;" + CourageChoice + "=" + CourageCheck)]
        [TestCase(LanguageChoice + "=" + LanguageCheck + ";" + LanguageChoice + "=" + CourageCheck)]
        [TestCase(LanguageChoice + "=" + LanguageCheck + ";missing.choice=" + CourageCheck)]
        [TestCase(LanguageChoice + "=" + LanguageCheck + ";" + CourageChoice + "=missing.check")]
        [TestCase(LanguageChoice + "=" + LanguageCheck + ";church.1023.s1.p021=" + CourageCheck)]
        public void InvalidMappingThrowsBeforeReadingEvenWithAValidSelectedPair(string mapping)
        {
            Start();
            Assert.That(session.SelectChoice(LanguageChoice), Is.True);
            int reads = 0;
            Controller.AbilityReader = _ => { reads++; return (true, 6); };
            string guard = "selectedCheck:" + mapping;
            Assert.Throws<InvalidOperationException>(() => Controller.ReadPresentationCondition(guard));
            Assert.Throws<InvalidOperationException>(() => Controller.ReadPresentationCondition("not:" + guard));
            Assert.That(reads, Is.Zero, "The complete mapping must be validated before evaluating the selected pair.");
        }

        [TestCase("mode")]
        [TestCase("successEventId")]
        [TestCase("failureEventId")]
        [TestCase("successResultOwnerId")]
        [TestCase("failureResultOwnerId")]
        public void InvalidUnselectedCheckConfigurationThrowsBeforeReading(string invalidField)
        {
            // Modify only this session's generated-content copy, before the presentation starts.
            var check = session.Configuration.abilityChecks.Single(c => c.checkId == CourageCheck);
            switch (invalidField)
            {
                case "mode": check.mode = NarrativeAbilityCheckMode.Branch; break;
                case "successEventId": check.successEventId = Root; break;
                case "failureEventId": check.failureEventId = Root; break;
                case "successResultOwnerId": check.successResultOwnerId = Root; break;
                case "failureResultOwnerId": check.failureResultOwnerId = Root; break;
                default: throw new ArgumentOutOfRangeException(nameof(invalidField));
            }
            Start();
            Assert.That(session.SelectChoice(LanguageChoice), Is.True);
            int reads = 0;
            Controller.AbilityReader = _ => { reads++; return (true, 6); };
            Assert.Throws<InvalidOperationException>(() => Controller.ReadPresentationCondition(Guard));
            Assert.Throws<InvalidOperationException>(() => Controller.ReadPresentationCondition("not:" + Guard));
            Assert.That(reads, Is.Zero);
        }

        [TestCase("")]
        [TestCase("not:")]
        public void MultipleMappedSelectionsAcrossGroupsThrowWithoutReadingAbilities(string prefix)
        {
            Start();
            Assert.That(session.SelectChoice(Root + ".p022"), Is.True);
            Assert.That(session.SelectChoice(LanguageChoice), Is.True);
            int reads = 0;
            Controller.AbilityReader = _ => { reads++; return (true, 6); };
            string guard = Guard + ";" + Root + ".p022=" + Root + ".defy";
            Assert.Throws<InvalidOperationException>(() => Controller.ReadPresentationCondition(prefix + guard));
            Assert.That(reads, Is.Zero);
            Assert.That(Controller.SelectedChoices.Count, Is.EqualTo(2));
        }
    }
}
