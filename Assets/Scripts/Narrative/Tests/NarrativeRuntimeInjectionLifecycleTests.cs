using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeRuntimeInjectionLifecycleTests
    {
        private sealed class HostPresenter : INarrativePresenter, IDisposable
        {
            public readonly List<Action> Completions = new List<Action>();
            public int Disposals;
            public void Play(NarrativeEventRequest request, Action completed) => Completions.Add(completed);
            public void Dispose() => Disposals++;
        }
        private static NarrativeConfiguration Config(string text)
        {
            var config = new NarrativeConfiguration();
            config.events.Add(new NarrativeEventDefinition { eventId = "root" });
            config.texts.Add(new NarrativeTextDefinition { key = "name", content = text });
            return config;
        }
        [TestCase(false)] [TestCase(true)]
        public void ResetRetainsExternalPresenterWithoutUiOwnershipOrStaleCompletion(bool replaceConfiguration)
        {
            int uiBefore = UnityEngine.Object.FindObjectsByType<NarrativePresentationUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var host = new GameObject("NCS injection lifecycle test");
            host.SetActive(false); // No Awake, singleton registration, or scene startup.
            var presenter = new HostPresenter();
            try
            {
                var runtime = host.AddComponent<NarrativeRuntime>();
                var originalConfig = Config("before");
                runtime.Initialize(originalConfig, presenter, key => key == "resolved" ? "host resolver" : key);
                Assert.That(runtime.Controller.QueueEvent("root"));
                var oldController = runtime.Controller;
                Action oldCompletion = presenter.Completions[0];
                var nextConfig = replaceConfiguration ? Config("after") : originalConfig;
                runtime.ResetSession(replaceConfiguration ? nextConfig : null);
                Assert.That(runtime.Controller, Is.Not.SameAs(oldController));
                Assert.That(runtime.Configuration, Is.SameAs(nextConfig));
                Assert.That(runtime.ResolveText("resolved"), Is.EqualTo("host resolver"));
                Assert.That(runtime.ResolveText("name"), Is.EqualTo(replaceConfiguration ? "after" : "before"));
                Assert.That(runtime.Presenter, Is.Null, "An explicit host presenter must not silently create an owned graphical presenter.");
                Assert.That(runtime.Controller.IsBusy, Is.False, "Reset is not a new event-entry request.");
                Assert.That(presenter.Disposals, Is.Zero);
                Assert.That(runtime.Controller.QueueEvent("root"));
                Assert.That(presenter.Completions.Count, Is.EqualTo(2));
                oldCompletion(); // May finish only the detached old controller, never the new session.
                Assert.That(runtime.Controller.IsBusy);
                Assert.That(runtime.Controller.State.HasHappenedEvent("root"), Is.False);
                presenter.Completions[1]();
                Assert.That(runtime.Controller.State.HasHappenedEvent("root"));
                runtime.ResetSession();
                Assert.That(runtime.Presenter, Is.Null); Assert.That(presenter.Disposals, Is.Zero);
                Assert.That(UnityEngine.Object.FindObjectsByType<NarrativePresentationUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(uiBefore));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
            Assert.That(presenter.Disposals, Is.Zero, "The external host remains responsible for disposal even when the runtime is destroyed.");
        }
    }
}
