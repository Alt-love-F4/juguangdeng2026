using System;
using System.IO;
using Spotlight.GameFlow;
using Spotlight.FreeCafe;
using UnityEditor;
using UnityEngine;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>Convenience entries for the same game-owned walkthrough; no separate test scene or game state.</summary>
    public static class NarrativeWalkthroughMenu
    {
        [MenuItem("Tools/Narrative/Walkthrough/Follow Current Todos")]
        private static void Begin() => Start(null, true);
        [MenuItem("Tools/Narrative/Walkthrough/Follow IF0 Target")]
        private static void BeginIf0() => Start("arrival.ending.if0", false);
        [MenuItem("Tools/Narrative/Walkthrough/Follow Full Opening")]
        private static void BeginFullOpening() => Start(null, false);
        private static void Start(string ending, bool skipStartup) => Run(() => {
            var runner = Runner();
            if (runner.Running) throw new InvalidOperationException("请先停止当前现场测试。");
            runner.SkipOpeningAndTutorials = skipStartup;
            runner.FastDialogue = true;
            runner.Begin(ending);
        });
        [MenuItem("Tools/Narrative/Walkthrough/Pause or Continue")]
        private static void Pause() => Run(() => Runner().TogglePause());
        [MenuItem("Tools/Narrative/Walkthrough/Next Step")]
        private static void Step() => Run(() => Runner().StepOnce());
        [MenuItem("Tools/Narrative/Walkthrough/Stop and Keep Current Scene")]
        private static void Stop() => Run(() => Runner().Stop());
        [MenuItem("Tools/Narrative/Walkthrough/Capture Rendered Game Frame")]
        private static void Capture() => Run(() => {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("请先进入 Play Mode。");
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            string directory = Path.GetFullPath("Library/NarrativeWalkthrough");
            Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "rendered-game.png"));
        });
        [MenuItem("Tools/Narrative/Walkthrough/Write Diagnostic Snapshot")]
        private static void Snapshot() => Run(() => {
            var runner = Runner();
            var flow = UnityEngine.Object.FindFirstObjectByType<GameFlowBootstrap>();
            var todo = GameplayHudPanel.Instance?.Tasks?.CurrentGuidance;
            string directory = Path.GetFullPath("Library/NarrativeWalkthrough");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "snapshot.json"), JsonUtility.ToJson(new SnapshotData {
                running = runner.Running, paused = runner.Paused, status = runner.Status,
                report = runner.OutputPath, count = runner.History.Count,
                saveAllowed = flow?.CanSaveCurrentSession == true,
                active = NarrativeAutoRunner.IsWalkthroughActive, todo = todo,
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                session = flow?.SessionState.ToString(),
                ending = Spotlight.Narrative.NarrativeRuntime.Instance?.Controller?.SelectedEndingId,
                dialogue = DialoguePanel.Active?.CurrentLineText,
                choosing = DialoguePanel.Active?.IsChoosing == true,
                tutorial = flow?.GetComponent<DayFlowController>()?.IsFirstSessionGuideActive == true,
                prologue = YokiFrame.UIKit.GetPanel<OpeningProloguePanel>()?.IsPlaying == true,
                firstLetter = YokiFrame.UIKit.GetPanel<FirstDayLetterPanel>()?.IsPlaying == true,
                skipsText = Spotlight.Narrative.NarrativeRuntime.Instance?.Presenter?.SkipDialogueText == true
            }, true));
            Debug.Log("Walkthrough snapshot: " + directory);
        });
        [Serializable] private sealed class SnapshotData
        {
            public bool running, paused, saveAllowed, active, choosing, tutorial, prologue, firstLetter, skipsText;
            public int count;
            public string status, report, scene, session, ending, dialogue;
            public MapTaskItem todo;
        }
        private static NarrativeWalkthroughRunner Runner()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("请先进入 Play Mode。");
            var runner = UnityEngine.Object.FindFirstObjectByType<NarrativeWalkthroughRunner>();
            if (runner == null) throw new InvalidOperationException("全局GM的现场跟随组件尚未就绪。");
            return runner;
        }
        private static void Run(Action action)
        {
            try { action(); } catch (Exception error) { Debug.LogWarning("[NarrativeWalkthrough] " + error.Message); }
        }
    }
}
