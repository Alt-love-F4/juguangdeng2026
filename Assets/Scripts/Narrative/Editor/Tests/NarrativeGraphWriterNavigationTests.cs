using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Spotlight.Narrative.Editor.Tests
{
    /// <summary>
    /// In-memory configuration and UI state only: no Draw/OnGUI, scene, runtime,
    /// generated-table loader, preview host, or Unity test-loop pumping.
    /// </summary>
    public sealed class NarrativeGraphWriterNavigationTests
    {
        private const string EventA = "neutral.1008.writer";
        private const string EventB = "rose.1009.writer";
        private const string First = "writer.a.first";
        private const string Middle = "writer.a.middle";
        private const string Last = "writer.a.last";
        private const string OtherFirst = "writer.b.first";
        // A numeric ID is deliberately the SECOND page of B, not a page-number command.
        private const string NumericId = "1";
        private static readonly string NodeA = NarrativeRelationshipGraph.EventId(EventA);
        private static readonly string NodeB = NarrativeRelationshipGraph.EventId(EventB);
        private NarrativeRelationshipGraphView view;

        [SetUp]
        public void SetUp() => view = new NarrativeRelationshipGraphView();

        [TearDown]
        public void TearDown()
        {
            // Also unregisters this view's delayCall if an assertion fails after queueing.
            view?.Dispose();
        }

        [Test]
        public void BeforeLoad_NavigationIsSafeAndReaderIsDefault()
        {
            AssertLocation(null, null);
            Assert.That(view.IsMapMode, Is.False);
            Assert.That(view.CanGoBack, Is.False);
            Assert.That(view.TrySelectPage(First), Is.False);
            Assert.That(view.GoBack(), Is.False);
            Assert.DoesNotThrow(() => view.Select(NodeA));
            Assert.DoesNotThrow(() => view.CancelQueuedActions());
            AssertLocation(null, null);
        }

        [Test]
        public void LoadThenSelect_UsesFirstPageBySequenceWithoutHistoryEntry()
        {
            view.Load(Config());
            Assert.That(view.VisibleNodeCount, Is.EqualTo(2));
            AssertLocation(null, null);
            view.Select(NodeA);
            AssertLocation(NodeA, First);
            Assert.That(view.SelectedNode.Key, Is.EqualTo(EventA));
            Assert.That(view.CanGoBack, Is.False);
            Assert.That(view.GoBack(), Is.False);
            AssertLocation(NodeA, First);
        }

        [Test]
        public void LoadBuildsEndingDirectoryInPriorityOrderAndKeepsDisabledEntriesVisible()
        {
            var config = Config();
            config.endings.Add(new NarrativeEndingDefinition { endingId = "ending.mid", enabled = true, priority = 10 });
            config.endings.Add(new NarrativeEndingDefinition { endingId = "ending.disabled", enabled = false, priority = 50 });
            config.endings.Add(new NarrativeEndingDefinition { endingId = "ending.high", enabled = true, priority = 30 });

            view.Load(config);

            Assert.That(view.EndingNodes.Count, Is.EqualTo(3));
            Assert.That(view.EndingNodes[0].Key, Is.EqualTo("ending.disabled"));
            Assert.That(view.EndingNodes[1].Key, Is.EqualTo("ending.high"));
            Assert.That(view.EndingNodes[2].Key, Is.EqualTo("ending.mid"));
            Assert.That(view.EndingNodes[0].Disabled, Is.True);
        }


        [Test]
        public void TrySelectPage_UsesExactIdEvenForNumericIds()
        {
            LoadAndSelect(NodeB);
            Assert.That(view.TrySelectPage(NumericId), Is.True);
            AssertLocation(NodeB, NumericId);
            Assert.That(view.GoBack(), Is.True);
            AssertLocation(NodeB, OtherFirst);
            Assert.That(view.CanGoBack, Is.False);
        }

        [Test]
        public void BrowsingDoesNotFollowNextPageOrStopAtEndOrEvaluateConditions()
        {
            var config = Config();
            view.Load(config);
            view.Select(NodeA);
            AssertLocation(NodeA, First); // First explicitly points to Last, but browsing stays here.
            Assert.That(view.TrySelectPage(Middle), Is.True); // End page with a false condition.
            AssertLocation(NodeA, Middle);
            Assert.That(view.TrySelectPage(Last), Is.True); // Still browseable after End.
            AssertLocation(NodeA, Last);
            Assert.That(config.conditionKeys[0].initialValue, Is.False);
            Assert.That(config.presentations.conversationPages[1].nextPageId, Is.EqualTo(Last));
            Assert.That(config.presentations.conversationPages.Count, Is.EqualTo(5));
        }

        [Test]
        public void RepeatedSelectOfSameNode_PreservesPageAndDoesNotPushHistory()
        {
            LoadAndSelect(NodeA);
            Assert.That(view.TrySelectPage(Middle), Is.True);
            view.Select(NodeA);
            view.Select(NodeA);
            AssertLocation(NodeA, Middle);
            Assert.That(view.GoBack(), Is.True);
            AssertLocation(NodeA, First);
            Assert.That(view.CanGoBack, Is.False);
        }

        [Test]
        public void RepeatedSelectOfSamePage_SucceedsWithoutPushingHistory()
        {
            LoadAndSelect(NodeA);
            Assert.That(view.TrySelectPage(First), Is.True);
            Assert.That(view.CanGoBack, Is.False);
            Assert.That(view.TrySelectPage(Middle), Is.True);
            Assert.That(view.TrySelectPage(Middle), Is.True);
            Assert.That(view.GoBack(), Is.True);
            AssertLocation(NodeA, First);
            Assert.That(view.CanGoBack, Is.False);
        }

        [Test]
        public void GoBack_RestoresPagesAcrossNodesInLifoOrder()
        {
            LoadAndSelect(NodeA);
            Assert.That(view.TrySelectPage(Middle), Is.True);
            view.Select(NodeB);
            AssertLocation(NodeB, OtherFirst);
            Assert.That(view.TrySelectPage(NumericId), Is.True);
            Assert.That(view.GoBack(), Is.True);
            AssertLocation(NodeB, OtherFirst);
            Assert.That(view.CanGoBack, Is.True);
            Assert.That(view.GoBack(), Is.True);
            AssertLocation(NodeA, Middle);
            Assert.That(view.GoBack(), Is.True);
            AssertLocation(NodeA, First);
            Assert.That(view.CanGoBack, Is.False);
            Assert.That(view.GoBack(), Is.False);
            AssertLocation(NodeA, First);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("WRITER.A.MIDDLE")]
        [TestCase(" writer.a.middle ")]
        [TestCase("writer.a")]
        [TestCase("missing")]
        [TestCase("2")]
        [TestCase(OtherFirst)]
        public void InvalidPageId_DoesNotChangeSelectionOrHistory(string invalidId)
        {
            LoadAndSelect(NodeA);
            Assert.That(view.TrySelectPage(Middle), Is.True);
            Assert.That(view.TrySelectPage(invalidId), Is.False);
            AssertLocation(NodeA, Middle);
            Assert.That(view.GoBack(), Is.True);
            AssertLocation(NodeA, First);
            Assert.That(view.CanGoBack, Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("event:missing")]
        [TestCase(EventA)]
        public void InvalidNodeId_DoesNotChangeSelectionOrHistory(string invalidId)
        {
            LoadAndSelect(NodeA);
            Assert.That(view.TrySelectPage(Middle), Is.True);
            view.Select(invalidId);
            AssertLocation(NodeA, Middle);
            Assert.That(view.GoBack(), Is.True);
            AssertLocation(NodeA, First);
            Assert.That(view.CanGoBack, Is.False);
        }

        [Test]
        public void MapModeToggle_PreservesSelectionAndDoesNotPushHistory()
        {
            LoadAndSelect(NodeA);
            Assert.That(view.IsMapMode, Is.False);
            Assert.That(view.TrySelectPage(Middle), Is.True);
            view.IsMapMode = true;
            Assert.That(view.IsMapMode, Is.True);
            AssertLocation(NodeA, Middle);
            view.IsMapMode = false;
            Assert.That(view.IsMapMode, Is.False);
            AssertLocation(NodeA, Middle);
            Assert.That(view.GoBack(), Is.True);
            AssertLocation(NodeA, First);
            Assert.That(view.CanGoBack, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LoadExistingNode_PreservesExactPageIdAndClearsAllHistory(bool reorderPages)
        {
            LoadAndSelect(NodeA);
            Assert.That(view.TrySelectPage(Middle), Is.True);
            view.Select(NodeB);
            Assert.That(view.TrySelectPage(NumericId), Is.True);
            Assert.That(view.CanGoBack, Is.True);
            var refreshed = Config();
            // Preserve identity, not the old list index, even when seq changes.
            if (reorderPages)
                refreshed.presentations.conversationPages.Find(p => p.pageId == NumericId).seq = 5;
            view.Load(refreshed);
            AssertLocation(NodeB, NumericId);
            Assert.That(view.CanGoBack, Is.False);
            Assert.That(view.GoBack(), Is.False);
            AssertLocation(NodeB, NumericId);
        }

        [Test]
        public void LoadWithSelectedPageRemoved_ReturnsToFirstPageAndClearsAllHistory()
        {
            LoadAndSelect(NodeA);
            Assert.That(view.TrySelectPage(Middle), Is.True);
            view.Select(NodeB);
            Assert.That(view.TrySelectPage(NumericId), Is.True);
            Assert.That(view.CanGoBack, Is.True);
            var refreshed = Config();
            Assert.That(refreshed.presentations.conversationPages.RemoveAll(p => p.pageId == NumericId), Is.EqualTo(1));
            view.Load(refreshed);
            AssertLocation(NodeB, OtherFirst);
            Assert.That(view.CanGoBack, Is.False);
            Assert.That(view.GoBack(), Is.False);
            Assert.That(view.TrySelectPage(NumericId), Is.False);
            AssertLocation(NodeB, OtherFirst);
            Assert.That(view.CanGoBack, Is.False);
        }

        [Test]
        public void LoadFailure_ClearsSelectionAndHistoryAndAllowsLaterLoad()
        {
            LoadAndSelect(NodeA);
            Assert.That(view.TrySelectPage(Middle), Is.True);
            view.Select(NodeB);
            Assert.That(view.CanGoBack, Is.True);
            // Build(null) throws; Load owns the failure boundary and discards old history.
            Assert.DoesNotThrow(() => view.Load(null));
            AssertLocation(null, null);
            Assert.That(view.VisibleNodeCount, Is.Zero);
            Assert.That(view.CanGoBack, Is.False);
            Assert.That(view.GoBack(), Is.False);
            Assert.That(view.TrySelectPage(NumericId), Is.False);
            LoadAndSelect(NodeA);
            AssertLocation(NodeA, First);
            Assert.That(view.CanGoBack, Is.False);
        }

        [Test]
        public void LoadEmptySnapshot_ClearsSelectionAndHistoryAndAllowsLaterLoad()
        {
            LoadAndSelect(NodeA);
            Assert.That(view.TrySelectPage(Last), Is.True);
            view.Load(new NarrativeConfiguration());
            Assert.That(view.VisibleNodeCount, Is.Zero);
            AssertLocation(null, null);
            Assert.That(view.CanGoBack, Is.False);
            Assert.That(view.GoBack(), Is.False);
            Assert.That(view.TrySelectPage(First), Is.False);
            view.Select(NodeA);
            AssertLocation(null, null);
            LoadAndSelect(NodeA);
            AssertLocation(NodeA, First);
            Assert.That(view.CanGoBack, Is.False);
        }

        [Test]
        public void HistoryLimit_KeepsMostRecent64Locations()
        {
            LoadAndSelect(NodeA);
            for (int i = 1; i <= 70; i++) view.Select(i % 2 == 0 ? NodeA : NodeB);
            for (int i = 69; i >= 6; i--)
            {
                Assert.That(view.CanGoBack, Is.True, "Missing retained location " + i);
                Assert.That(view.GoBack(), Is.True);
                AssertLocation(i % 2 == 0 ? NodeA : NodeB, i % 2 == 0 ? First : OtherFirst);
            }
            Assert.That(view.CanGoBack, Is.False);
            Assert.That(view.GoBack(), Is.False);
            AssertLocation(NodeA, First);
        }

        [Test]
        public void Dispose_IsIdempotentAndNavigationCannotReviveView()
        {
            LoadAndSelect(NodeA);
            Assert.That(view.TrySelectPage(Middle), Is.True);
            view.Dispose();
            Assert.DoesNotThrow(() => view.Dispose());
            Assert.DoesNotThrow(() => view.CancelQueuedActions());
            view.Load(Config());
            view.Select(NodeA);
            Assert.That(view.TrySelectPage(First), Is.False);
            Assert.That(view.GoBack(), Is.False);
            Assert.That(view.VisibleNodeCount, Is.Zero);
            AssertLocation(null, null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void QueuedHostAction_IsDeferredAndDispatchesOnlyOnce(bool refresh)
        {
            LoadAndSelect(NodeA);
            int calls = 0;
            var dispatch = QueueHostAction(refresh, () => calls++);
            Assert.That(calls, Is.Zero, "Queueing must not call a host synchronously.");
            dispatch();
            dispatch();
            Assert.That(calls, Is.EqualTo(1), "Positive control for the cancellation tests.");
        }

        [Test]
        public void PreviewDispatch_PassesExactSelectedPageIdToHost()
        {
            LoadAndSelect(NodeB);
            Assert.That(view.TrySelectPage(NumericId), Is.True);
            string receivedNode = null, receivedPage = null;
            view.PreviewRequested = (node, pageId) => { receivedNode = node.Id; receivedPage = pageId; };
            InvokePrivate("QueuePreview", view.SelectedNode, view.SelectedPageId);
            var dispatch = DispatchField().GetValue(view) as EditorApplication.CallbackFunction;
            Assert.That(dispatch, Is.Not.Null);
            Assert.That(receivedNode, Is.Null);
            dispatch();
            Assert.That(receivedNode, Is.EqualTo(NodeB));
            Assert.That(receivedPage, Is.EqualTo(NumericId));
        }

        [Test]
        public void PublicNavigation_DoesNotInvokeHostActions()
        {
            int previews = 0, refreshes = 0;
            view.PreviewRequested = (node, pageId) => previews++;
            view.RefreshRequested = () => refreshes++;
            LoadAndSelect(NodeA);
            Assert.That(view.TrySelectPage(Middle), Is.True);
            view.Select(NodeB);
            view.IsMapMode = true;
            Assert.That(view.GoBack(), Is.True);
            view.Load(Config());
            view.CancelQueuedActions();
            Assert.That(previews, Is.Zero);
            Assert.That(refreshes, Is.Zero);
            Assert.That(DispatchField().GetValue(view), Is.Null);
        }
        [TestCase(false, "cancel")]
        [TestCase(true, "cancel")]
        [TestCase(false, "load")]
        [TestCase(true, "load")]
        [TestCase(false, "dispose")]
        [TestCase(true, "dispose")]
        public void CancelLoadOrClose_PreventsStaleQueuedHostCallback(bool refresh, string boundary)
        {
            LoadAndSelect(NodeA);
            int calls = 0;
            var staleDispatch = QueueHostAction(refresh, () => calls++);
            Assert.That(calls, Is.Zero);
            if (boundary == "cancel") view.CancelQueuedActions();
            else if (boundary == "load") view.Load(Config());
            else view.Dispose();
            staleDispatch(); // Simulates a callback already copied by the editor before cancellation.
            Assert.That(calls, Is.Zero);
            Assert.That(DispatchField().GetValue(view), Is.Null);
        }

        [Test]
        public void CancelThenRequeue_StaleDispatchCannotDrainNewRequest()
        {
            LoadAndSelect(NodeA);
            int oldCalls = 0, newCalls = 0;
            var staleDispatch = QueueHostAction(false, () => oldCalls++);
            view.CancelQueuedActions();
            view.CancelQueuedActions();
            var currentDispatch = QueueHostAction(false, () => newCalls++);
            staleDispatch();
            Assert.That(oldCalls, Is.Zero);
            Assert.That(newCalls, Is.Zero);
            currentDispatch();
            Assert.That(oldCalls, Is.Zero);
            Assert.That(newCalls, Is.EqualTo(1));
        }

        private void LoadAndSelect(string nodeId)
        {
            view.Load(Config());
            view.Select(nodeId);
        }

        private void AssertLocation(string nodeId, string pageId)
        {
            Assert.That(view.SelectedNodeId, Is.EqualTo(nodeId));
            Assert.That(view.SelectedPageId, Is.EqualTo(pageId));
            if (nodeId == null) Assert.That(view.SelectedNode, Is.Null);
            else
            {
                Assert.That(view.SelectedNode, Is.Not.Null);
                Assert.That(view.SelectedNode.Id, Is.EqualTo(nodeId));
            }
        }

        // Only queue/dispatch setup uses reflection because the production queue entry points
        // are IMGUI-private. Navigation assertions above use public APIs exclusively. Capture
        // this view's delegate, never invoke the editor's global delayCall or pump other agents' work.
        private EditorApplication.CallbackFunction QueueHostAction(bool refresh, Action callback)
        {
            if (refresh)
            {
                view.RefreshRequested = callback;
                InvokePrivate("RequestRefresh");
            }
            else
            {
                view.PreviewRequested = (node, pageId) => callback();
                InvokePrivate("QueuePreview", view.SelectedNode, view.SelectedPageId);
            }
            var dispatch = DispatchField().GetValue(view) as EditorApplication.CallbackFunction;
            Assert.That(dispatch, Is.Not.Null, "Fixture must actually queue a callback.");
            return dispatch;
        }

        private void InvokePrivate(string name, params object[] arguments)
        {
            var method = typeof(NarrativeRelationshipGraphView).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Queue test seam changed: " + name);
            method.Invoke(view, arguments);
        }

        private static FieldInfo DispatchField()
        {
            var field = typeof(NarrativeRelationshipGraphView).GetField("editorDispatch", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Queue test seam changed: editorDispatch");
            return field;
        }

        private static NarrativeConfiguration Config()
        {
            var config = new NarrativeConfiguration();
            config.events.Add(new NarrativeEventDefinition { eventId = EventA, presentationKey = "writer.a" });
            config.events.Add(new NarrativeEventDefinition { eventId = EventB, presentationKey = "writer.b" });
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "writer.unmet", initialValue = false });
            // Deliberately unsorted rows; browsing must use seq rather than insertion order.
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { pageId = Last, presentationKey = "writer.a", seq = 30 });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { pageId = First, presentationKey = "writer.a", seq = 10, nextPageId = Last });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { pageId = Middle, presentationKey = "writer.a", seq = 20, stepType = NarrativeConversationStepType.End, pageCondKeys = "writer.unmet" });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { pageId = NumericId, presentationKey = "writer.b", seq = 20 });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { pageId = OtherFirst, presentationKey = "writer.b", seq = 10 });
            return config;
        }
    }
}
