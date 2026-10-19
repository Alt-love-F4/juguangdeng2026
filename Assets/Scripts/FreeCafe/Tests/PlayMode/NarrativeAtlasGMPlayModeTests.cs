#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Spotlight.GameFlow;
using Spotlight.Narrative;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Spotlight.FreeCafe.Tests
{
    /// <summary>Exercises the bootstrapped global GM and actual shared presentation scene, not a test UI.</summary>
    public sealed class NarrativeAtlasGMPlayModeTests
    {
        private FreeCafeGMPanel shell;
        private NarrativeAtlasGM atlas;
        private PresentationDebugSceneController controller;
        private Scene previousScene, debugScene;
        private float previousTimeScale;
        private int previousCategory, pauseOwners;
        private bool previousVisible, previousAtlasOpen, ownsFixture;
        private string previousSelection;
        private EventSystem[] previousSystems;
        private bool[] previousSystemEnabled;
        private NarrativeRuntime originalRuntime;
        private NarrativeRuntime[] originalRuntimeObjects;
        private FreeCafeRuntime[] originalCafes;
        private string[] originalRequests;
        private string originalState, originalActions, originalInventory;
        private NarrativeConfiguration configuration;
        private string originalConfiguration;
        private string captureDirectory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            captureDirectory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../output/narrative-gm-atlas/writer-ui",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            previousTimeScale = Time.timeScale;
            previousScene = SceneManager.GetActiveScene();
            // Verify an already-paused game stays paused through browsing, playback and close.
            Time.timeScale = 0f;
            shell = FreeCafeGMPanel.Instance;
            Assert.That(shell, Is.Not.Null, "Use the global GM bootstrap; do not build another shell/Canvas.");
            atlas = shell.GetComponent<NarrativeAtlasGM>();
            Assert.That(atlas, Is.Not.Null, "NarrativeDebugGM.Awake must attach the production atlas.");
            Assert.That(atlas.IsPreviewPlaying, Is.False, "Do not interrupt someone else's existing GM preview.");
            previousCategory = ReadField<int>(shell, "activeCategory");
            previousVisible = shell.IsVisible;
            previousAtlasOpen = atlas.IsOpen;
            previousSelection = atlas.View?.SelectedNodeId;
            ownsFixture = true;
            shell.SetVisible(false);
            AssertShell(false);
            pauseOwners = PresentationPauseRegistry.OwnerCount;

            previousSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            previousSystemEnabled = previousSystems.Select(system => system.enabled).ToArray();
            foreach (var system in previousSystems) system.enabled = false;
            debugScene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            Assert.That(debugScene.IsValid() && debugScene.isLoaded, Is.True);
            SceneManager.SetActiveScene(debugScene);
            controller = debugScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PresentationDebugSceneController>(true)).Single();
            Assert.That(controller.CurrentPanel, Is.Null);
            if (controller.HelpVisible) controller.ToggleHelp();

            originalRuntime = NarrativeRuntime.Instance;
            originalRuntimeObjects = Object.FindObjectsByType<NarrativeRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            originalCafes = Object.FindObjectsByType<FreeCafeRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            originalRequests = originalCafes.Select(cafe => Fingerprint(cafe.World?.State.requests)).ToArray();
            if (originalRuntime != null && originalRuntime.IsReady)
            {
                originalState = Fingerprint(originalRuntime.Controller.State.Capture());
                originalActions = originalRuntime.Actions?.CaptureJson();
                originalInventory = Fingerprint(originalRuntime.MapInventory?.Items);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!ownsFixture) { Time.timeScale = previousTimeScale; yield break; }
            // Only this service's preview is stopped. Never close arbitrary UIKit tags/panels.
            if (atlas != null) atlas.StopPreview();
            if (shell != null) shell.SetVisible(false);
            if (controller != null) controller.StopCurrent();
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (debugScene.IsValid() && debugScene.isLoaded) yield return SceneManager.UnloadSceneAsync(debugScene);
            if (previousSystems != null)
                for (int i = 0; i < previousSystems.Length; i++)
                    if (previousSystems[i] != null) previousSystems[i].enabled = previousSystemEnabled[i];
            if (shell != null)
            {
                // Restore the original category even when an assertion failed mid-overlay.
                var method = typeof(FreeCafeGMPanel).GetMethod("ShowCategory", BindingFlags.Instance | BindingFlags.NonPublic);
                method?.Invoke(shell, new object[] { previousCategory });
                yield return null; // Drain a restored atlas category's queued Open before closing it.
                if (atlas != null && previousSelection != null) atlas.SelectNode(previousSelection);
                shell.SetVisible(false);
                shell.SetVisible(previousVisible);
                if (previousAtlasOpen && atlas != null) atlas.Open();
            }
            Time.timeScale = previousTimeScale;
            yield return null; // Drain the GM visibility-frame input shield.
        }

        [UnityTest]
        public IEnumerator GlobalGMShellOpensRealAtlasAndDailyPreviewWithoutChangingTheGame()
        {
            shell.SetVisible(true);
            Assert.That(shell.SelectCategory("全局调试"), Is.True);
            yield return null;
            AssertShell(false);
            Assert.That(shell.IsVisible, Is.True);
            yield return Capture("gm-shell.png");

            // Selecting the existing category must open by itself on the next Update.
            Assert.That(shell.SelectCategory("剧情图谱"), Is.True);
            yield return WaitFor(() => atlas.IsOpen, "Selecting 剧情图谱 must enqueue Open.");
            Assert.That(atlas.View, Is.Not.Null);
            Assert.That(atlas.View.VisibleNodeCount, Is.GreaterThan(0), "Use the real generated graph.");
            configuration = ReadField<NarrativeConfiguration>(atlas, "config");
            Assert.That(configuration, Is.Not.Null);
            originalConfiguration = Fingerprint(configuration);
            var daily = configuration.events.Single(e => e.eventId == "daily.st.01");
            Assert.That(daily.presentationClass, Is.EqualTo("Conversation").IgnoreCase);
            Assert.That(configuration.presentations.conversationPages.Any(page => page.presentationKey == daily.presentationKey), Is.True);

            atlas.SelectNode("event:daily.st.01");
            yield return null;
            var view = atlas.View;
            Assert.That(view.SelectedNodeId, Is.EqualTo("event:daily.st.01"));
            Assert.That(view.SelectedNode, Is.Not.Null);
            Assert.That(view.StatusLabel, Is.Not.Null, "Runtime status must be wired into the production graph renderer.");
            Assert.That(view.StatusDetails, Is.Not.Null);
            var expectedStatus = new NarrativeGraphRuntimeStatus(configuration);
            expectedStatus.Refresh(originalRuntime != null && originalRuntime.IsReady ? originalRuntime.Controller : null);
            string label = view.StatusLabel(view.SelectedNode);
            Assert.That(label, Is.Not.Null.And.Not.Empty);
            Assert.That(label, Is.EqualTo(expectedStatus.Label(view.SelectedNode)));
            Assert.That(view.StatusDetails(view.SelectedNode), Does.Contain(label));
            Assert.That(view.SelectedPageId, Is.Not.Null.And.Not.Empty);
            Assert.That(view.PreviewBlockReason(view.SelectedNode), Is.Null.Or.Empty);
            Assert.That(shell.IsVisible && FreeCafeGMPanel.BlocksDialogueInput, Is.True,
                "Transparent shell remains logically visible and shields world/dialogue input.");
            AssertShell(true);
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners + 1));
            AssertUnchanged();
            string selectedPage = view.SelectedPageId;
            view.IsMapMode = false;
            yield return Capture("gm-atlas.png"); // Preserve the default writer-reading capture.
            try
            {
                var mainline = configuration.events.Where(e => NarrativeRelationshipGraph.IsStory(e.eventId))
                    .OrderBy(e => e.eventId, StringComparer.Ordinal)
                    .FirstOrDefault(e => configuration.presentations.conversationPages.Count(p => p.presentationKey == e.presentationKey) > 1);
                Assert.That(mainline, Is.Not.Null, "The generated configuration must contain a multi-page story for writer UI review.");
                atlas.SelectNode("event:" + mainline.eventId);
                yield return null;
                yield return Capture("gm-atlas-writer-reading.png");
                view.IsMapMode = true;
                yield return Capture("gm-atlas-writer-map.png");
            }
            finally
            {
                view.IsMapMode = false;
                atlas.SelectNode("event:daily.st.01");
                view.TrySelectPage(selectedPage);
            }

            atlas.PlaySelected(); // Production service consumes this request in Update, not OnGUI.
            yield return WaitFor(() => atlas.IsPreviewPlaying && !shell.IsVisible && !atlas.IsOpen
                && DialoguePanel.Active != null && DialoguePanel.Active.IsPlaying && DialoguePanel.Active.IsReady
                && DialoguePanel.Active.CurrentSource?.PageId == selectedPage,
                "Queued daily preview must hide GM and open its actual DialoguePanel. Status: " + atlas.LastStatus);
            var dialogue = DialoguePanel.Active;
            Assert.That(dialogue.Tag, Is.EqualTo(NarrativeAtlasPreview.PanelTag));
            Assert.That(dialogue.CurrentSource.EventId, Is.EqualTo(daily.eventId));
            var sourceLine = configuration.presentations.conversationLines.Single(line => line.lineId == dialogue.CurrentSource.SourceLineId);
            string expectedText = originalRuntime != null && originalRuntime.IsReady
                ? originalRuntime.ResolveText(sourceLine.textKey)
                : configuration.texts.Find(row => row.key == sourceLine.textKey)?.content ?? sourceLine.textKey;
            Assert.That(expectedText, Is.Not.Null.And.Not.Empty);
            Assert.That(dialogue.GetComponentsInChildren<TMP_Text>(true)
                .Any(text => text.gameObject.activeInHierarchy && text.text == expectedText), Is.True,
                "Actual daily source text must reach the real DialoguePanel, not just a status label.");
            Assert.That(dialogue.InputSuspended, Is.False, "Hiding the GM must release presenter suspension on a subsequent frame.");
            Assert.That(dialogue.GetComponentsInChildren<TMP_Text>(true)
                .Any(text => text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text)), Is.True);
            AssertShell(false);
            AssertUnchanged();

            // Reopen the same global shell during playback, not a second atlas instance.
            shell.SetVisible(true);
            yield return WaitFor(() => atlas.IsOpen, "Reopening GM during its preview should return to the atlas.");
            Assert.That(atlas.View, Is.SameAs(view));
            Assert.That(view.SelectedNodeId, Is.EqualTo("event:daily.st.01"));
            Assert.That(view.SelectedPageId, Is.EqualTo(selectedPage));
            Assert.That(atlas.IsPreviewPlaying, Is.True);
            AssertShell(true);
            string previewDetails = view.StatusDetails(view.SelectedNode);
            Assert.That(previewDetails, Does.Contain("本工具沙盒试播正在显示此页"));
            Assert.That(previewDetails, Does.Not.Contain("当前真实舞台"));
            Assert.That(view.StatusLabel(view.SelectedNode), Is.EqualTo(expectedStatus.Label(view.SelectedNode)),
                "Preview stage annotations must not replace formal runtime status.");

            // Closing the browser to continue watching cancels only queued work, not the active preview.
            var ownedPreview = ReadField<NarrativeAtlasPreview>(atlas, "preview");
            InvokePrivate(atlas, "ContinueWatching");
            yield return null;
            Assert.That(atlas.IsOpen || shell.IsVisible, Is.False);
            Assert.That(atlas.IsPreviewPlaying, Is.True);
            Assert.That(ReadField<NarrativeAtlasPreview>(atlas, "preview"), Is.SameAs(ownedPreview));
            Assert.That(ReadField<bool>(atlas, "reopenAfterPlayback"), Is.True);
            shell.SetVisible(true);
            yield return WaitFor(() => atlas.IsOpen, "ContinueWatching must allow returning to the same preview.");
            atlas.StopPreview();
            yield return WaitFor(() => !atlas.IsPreviewPlaying && (dialogue == null || !dialogue.IsPlaying),
                "StopPreview must release only the atlas-owned presentation.");
            Assert.That(atlas.LastStatus, Does.Contain("停止"));
            Assert.That(view.StatusDetails(view.SelectedNode), Does.Not.Contain("本工具沙盒试播正在显示此页"));
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners + 1), "Browsing still owns its pause.");
            AssertUnchanged();

            shell.SetVisible(false);
            yield return null;
            Assert.That(atlas.IsOpen, Is.False);
            AssertShell(false);
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners));
            Assert.That(Time.timeScale, Is.Zero, "Closing GM must not unpause an already-paused game.");
            shell.SetVisible(true);
            Assert.That(shell.SelectCategory("全局调试"), Is.True);
            yield return null;
            Assert.That(atlas.IsOpen, Is.False);
            AssertShell(false);
            AssertUnchanged();
        }

        [UnityTest]
        public IEnumerator ClosingOrStoppingCancelsHostRequestBeforeUpdate()
        {
            foreach (string cancellation in new[] { "close", "hide", "stop" })
            {
                OpenDailyForCancellation();
                atlas.PlaySelected();
                Assert.That(ReadField<bool>(atlas, "playRequested"), Is.True);
                CancelBeforeUpdate(cancellation);
                AssertPendingPlaybackCleared();
                // Reopening before the next Update must not resurrect the old request.
                atlas.Open();
                yield return null;
                yield return null;
                Assert.That(atlas.IsPreviewPlaying, Is.False, cancellation);
                Assert.That(atlas.IsOpen && shell.IsVisible, Is.True);
                Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners + 1));
                AssertUnchanged();
            }
        }

        [UnityTest]
        public IEnumerator ClosingOrStoppingInvalidatesDeferredViewCallback()
        {
            foreach (string cancellation in new[] { "close", "hide", "stop" })
            {
                OpenDailyForCancellation();
                var view = atlas.View;
                // Exercise the real view queue, without synthesizing OnGUI or creating test UI.
                InvokePrivate(view, "QueuePreview", view.SelectedNode, view.SelectedPageId);
                Assert.That(ReadField<Action>(view, "pendingRequest"), Is.Not.Null);
                int staleTicket = ReadField<int>(view, "requestTicket");
                CancelBeforeUpdate(cancellation);
                Assert.That(ReadField<Action>(view, "pendingRequest"), Is.Null);
                AssertPendingPlaybackCleared();
                atlas.Open();
                // A stale posted callback cannot drain a newly queued action either.
                InvokePrivate(view, "QueuePreview", view.SelectedNode, view.SelectedPageId);
                var newerAction = ReadField<Action>(view, "pendingRequest");
                Assert.That(newerAction, Is.Not.Null);
                InvokePrivate(view, "DispatchRequest", staleTicket);
                Assert.That(ReadField<Action>(view, "pendingRequest"), Is.SameAs(newerAction));
                Assert.That(ReadField<bool>(atlas, "playRequested"), Is.False);
                atlas.StopPreview();
                yield return null;
                yield return null;
                AssertPendingPlaybackCleared();
                Assert.That(atlas.IsPreviewPlaying, Is.False, cancellation);
                AssertUnchanged();
            }
        }

        [UnityTest]
        public IEnumerator ClosedAtlasRejectsQueueAndExecutionWithoutStartingPlayback()
        {
            OpenDailyForCancellation();
            var node = atlas.View.SelectedNode;
            string pageId = atlas.View.SelectedPageId;
            InvokePrivate(atlas, "CloseAtlas"); // Shell remains visible: open itself must be checked.
            atlas.PlaySelected();
            atlas.View.PreviewRequested(node, pageId);
            AssertPendingPlaybackCleared();
            yield return null;
            Assert.That(atlas.IsPreviewPlaying, Is.False);

            atlas.Open();
            atlas.PlaySelected();
            Assert.That(ReadField<bool>(atlas, "playRequested"), Is.True);
            // Deliberately bypass CloseAtlas cleanup to exercise the execution-side guard independently.
            var openField = typeof(NarrativeAtlasGM).GetField("open", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(openField, Is.Not.Null);
            openField.SetValue(atlas, false);
            InvokePrivate(atlas, "Update");
            AssertPendingPlaybackCleared();
            Assert.That(atlas.IsPreviewPlaying, Is.False);
            InvokePrivate(atlas, "CloseAtlas");
            yield return null;
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners));
            AssertUnchanged();
        }

        [Test]
        public void HiddenAtlasSkipsStatusPollingEvenIfOpenFlagIsStale()
        {
            Assert.That(shell.IsVisible, Is.False);
            var openField = typeof(NarrativeAtlasGM).GetField("open", BindingFlags.Instance | BindingFlags.NonPublic);
            var nextRefreshField = typeof(NarrativeAtlasGM).GetField("nextRefresh", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(openField, Is.Not.Null);
            Assert.That(nextRefreshField, Is.Not.Null);
            openField.SetValue(atlas, true);
            nextRefreshField.SetValue(atlas, -1f);

            InvokePrivate(atlas, "Update");

            Assert.That(nextRefreshField.GetValue(atlas), Is.EqualTo(-1f), "A hidden atlas must not poll or refresh NCS status.");
            openField.SetValue(atlas, false);
        }

        [Test]
        public void OpenAtlasWiresReadOnlyEndingForecastAndConfiguredEndingDirectory()
        {
            atlas.Open();

            Assert.That(atlas.IsOpen, Is.True);
            Assert.That(atlas.View, Is.Not.Null);
            Assert.That(atlas.View.EndingNodes, Is.Not.Empty);
            Assert.That(atlas.View.EndingForecastText, Is.Not.Null);
            var configurationField = typeof(NarrativeAtlasGM).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(field => field.FieldType == typeof(NarrativeConfiguration));
            configuration = (NarrativeConfiguration)configurationField.GetValue(atlas);
            Assert.That(configuration, Is.Not.Null);
            originalConfiguration = Fingerprint(configuration);
            Assert.That(atlas.View.EndingForecastText(), Is.Not.Null.And.Not.Empty);
            AssertUnchanged();
        }

        [UnityTest]
        public IEnumerator ResponsiveCanvasUsesNativeDesktopPixelsAndMinimumLogicalSize()
        {
            var calculate = typeof(NarrativeAtlasGM).GetMethod("CalculateCanvas", BindingFlags.Static | BindingFlags.NonPublic);
            var toolbarMethod = typeof(NarrativeAtlasGM).GetMethod("ToolbarRect", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(calculate, Is.Not.Null);
            Assert.That(toolbarMethod, Is.Not.Null);
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                new Vector2Int(1000, 650), new Vector2Int(800, 600), new Vector2Int(640, 360) })
            {
                var canvas = (Vector3)calculate.Invoke(null, new object[] { size.x, size.y });
                Assert.That(canvas.x, Is.GreaterThanOrEqualTo(999.99f));
                Assert.That(canvas.y, Is.GreaterThanOrEqualTo(649.99f));
                Assert.That(canvas.x * canvas.z, Is.EqualTo(size.x).Within(.01f));
                Assert.That(canvas.y * canvas.z, Is.EqualTo(size.y).Within(.01f));
                if (size.x >= 1000 && size.y >= 650) Assert.That(canvas.z, Is.EqualTo(1f));
                var toolbar = (Rect)toolbarMethod.Invoke(null, new object[] { canvas.x });
                Assert.That(toolbar.xMin, Is.GreaterThanOrEqualTo(22f));
                Assert.That(toolbar.xMax, Is.LessThanOrEqualTo(canvas.x - 22f));
                Assert.That(toolbar.y, Is.EqualTo(canvas.x < 1160f ? 46f : 14f));
                // Even the maximum three-line status leaves room for the shared view.
                Assert.That(canvas.y - (toolbar.yMax + 6f + 66f + 8f) - 10f, Is.GreaterThan(420f));
            }
            yield return null;
        }

        private void OpenDailyForCancellation()
        {
            atlas.Open();
            atlas.SelectNode("event:daily.st.01");
            configuration = ReadField<NarrativeConfiguration>(atlas, "config");
            originalConfiguration = Fingerprint(configuration);
            Assert.That(atlas.IsOpen && shell.IsVisible, Is.True);
            Assert.That(atlas.View.PreviewBlockReason(atlas.View.SelectedNode), Is.Null.Or.Empty);
        }
        private void CancelBeforeUpdate(string cancellation)
        {
            if (cancellation == "close") InvokePrivate(atlas, "CloseAtlas");
            else if (cancellation == "hide") shell.SetVisible(false);
            else atlas.StopPreview();
        }
        private void AssertPendingPlaybackCleared()
        {
            Assert.That(ReadField<bool>(atlas, "playRequested"), Is.False);
            Assert.That(ReadField<string>(atlas, "requestedNode"), Is.Null);
            Assert.That(ReadField<string>(atlas, "requestedPage"), Is.Null);
            Assert.That(ReadField<bool>(atlas, "playWholeRoot"), Is.False);
        }
        private static void InvokePrivate(object owner, string name, params object[] arguments)
        {
            var method = owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(owner, arguments);
        }

        private void AssertShell(bool overlay)
        {
            var group = ReadField<CanvasGroup>(shell, "shellGroup");
            Assert.That(group, Is.Not.Null);
            Assert.That(group.alpha, Is.EqualTo(overlay ? 0f : 1f));
            Assert.That(group.interactable, Is.EqualTo(!overlay));
            Assert.That(group.blocksRaycasts, Is.True, "Overlay must not allow input through the existing GM shell.");
        }

        private void AssertUnchanged()
        {
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(NarrativeRuntime.Instance, Is.SameAs(originalRuntime), "Browsing/preview must never bootstrap or replace NarrativeRuntime.");
            CollectionAssert.AreEquivalent(originalRuntimeObjects,
                Object.FindObjectsByType<NarrativeRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            CollectionAssert.AreEquivalent(originalCafes,
                Object.FindObjectsByType<FreeCafeRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None), "Do not create a CafeWorld host.");
            for (int i = 0; i < originalCafes.Length; i++)
                Assert.That(Fingerprint(originalCafes[i].World?.State.requests), Is.EqualTo(originalRequests[i]), "No cafe orders may be created/changed.");
            Assert.That(Fingerprint(configuration), Is.EqualTo(originalConfiguration), "Preview must copy, never mutate, the loaded production configuration.");
            if (originalRuntime == null || !originalRuntime.IsReady) return;
            Assert.That(configuration, Is.SameAs(originalRuntime.Configuration));
            Assert.That(Fingerprint(originalRuntime.Controller.State.Capture()), Is.EqualTo(originalState));
            Assert.That(originalRuntime.Actions?.CaptureJson(), Is.EqualTo(originalActions), "No formal AP/day/action settlement.");
            Assert.That(Fingerprint(originalRuntime.MapInventory?.Items), Is.EqualTo(originalInventory));
        }

        private IEnumerator Capture(string name)
        {
            // OnGUI and UGUI must actually render in the shared Game view. No synthetic layout/camera.
            // The main runner owns Game-view focus; WaitForEndOfFrame requires a rendered frame.
            yield return new WaitForEndOfFrame();
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Assert.That(image, Is.Not.Null);
                Assert.That(image.width, Is.GreaterThan(0));
                Assert.That(image.height, Is.GreaterThan(0));
                Directory.CreateDirectory(captureDirectory);
                string path = Path.Combine(captureDirectory, name);
                File.WriteAllBytes(path, image.EncodeToPNG());
                TestContext.Progress.WriteLine("GM writer UI capture: " + path);
            }
            finally { if (image != null) Object.Destroy(image); }
        }

        private static IEnumerator WaitFor(Func<bool> ready, string message)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ready(), Is.True, message);
            yield return null; // Allow GM visibility/input suspension to settle after its Update.
        }

        private static T ReadField<T>(object owner, string name)
        {
            var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(owner);
        }

        // Configuration DTOs include dictionaries: JsonUtility alone silently omits them.
        // Inspect public data only, without adding a Newtonsoft/other assembly reference.
        private static string Fingerprint(object value)
        {
            var text = new StringBuilder();
            Append(value, text);
            return text.ToString();
        }

        private static void Append(object value, StringBuilder text)
        {
            if (value == null) { text.Append("null;"); return; }
            Type type = value.GetType();
            if (value is string word) { text.Append(word.Length).Append(':').Append(word).Append(';'); return; }
            if (type.IsPrimitive || type.IsEnum || value is decimal)
            { text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            text.Append(type.FullName).Append('{');
            if (value is IDictionary dictionary)
                foreach (object key in dictionary.Keys.Cast<object>().OrderBy(key => Convert.ToString(key, CultureInfo.InvariantCulture), StringComparer.Ordinal))
                { Append(key, text); Append(dictionary[key], text); }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            { Append(type.GetProperty("Key").GetValue(value), text); Append(type.GetProperty("Value").GetValue(value), text); }
            else if (value is IEnumerable sequence)
                foreach (object item in sequence) Append(item, text);
            else
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name, StringComparer.Ordinal))
                { text.Append(field.Name).Append('='); Append(field.GetValue(value), text); }
            text.Append('}');
        }
    }
}
#endif
