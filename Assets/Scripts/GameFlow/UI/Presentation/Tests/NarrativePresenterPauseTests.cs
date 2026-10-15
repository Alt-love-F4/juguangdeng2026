using System.Collections;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NarrativePresenterPauseTests
    {
        [UnityTest]
        public IEnumerator GmPauseFreezesNonDialogueWaitAndDoesNotReleaseHostPause()
        {
            var previous = NarrativePresenter.ExternalInputBlocked;
            bool gm = false;
            NarrativePresenter.ExternalInputBlocked = () => gm;
            var config = new NarrativeConfiguration();
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "wait", atomSeq = "A_WAIT" });
            config.setups.Add(new NarrativeSetupParamsDefinition { setupKey = "wait", tplName = "wait", slotValues = "duration=0.2" });
            var definition = new NarrativeEventDefinition { eventId = "wait", setupKey = "wait", setupType = "wait" };
            var presenter = new NarrativePresenter(config);
            int completed = 0;
            try
            {
                presenter.Play(new NarrativeEventRequest(definition, null, 0), () => completed++);
                gm = true;
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(completed, Is.Zero, "unscaled atom wait must freeze while GM is open");
                presenter.SetSuspended(true);
                gm = false;
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(completed, Is.Zero, "closing GM cannot release the host suspension");
                presenter.SetSuspended(false);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(completed, Is.EqualTo(1));
            }
            finally { presenter.Dispose(); NarrativePresenter.ExternalInputBlocked = previous; }
        }
    }
}
