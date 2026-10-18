using System;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Uses the existing session wiring when a gameplay scene is started without GameFlowBootstrap.</summary>
    internal sealed class StandaloneGameplaySession : IDisposable
    {
        private readonly PlayerSessionCoordinator coordinator = new PlayerSessionCoordinator();
        private PlayerRuntime player;
        private NarrativeRuntime runtime;
        private CafeWorld world;
        private DayFlowController tutorialFlow;

        public void Ensure()
        {
            NarrativeRuntime target = NarrativeRuntime.Instance;
            if (target == null) target = new GameObject("NarrativeRuntime.Standalone").AddComponent<NarrativeRuntime>();
            if (!target.IsReady) target.Initialize();
            if (PlayerRuntime.Current != null && PlayerRuntime.Current != player) return; // Explicit session injection wins.
            if (player == null || PlayerRuntime.Current == null)
            {
                player = new PlayerRuntime(PlayerCreationCatalog.Load());
                PlayerRuntime.Current = player;
                target.ConversationPlayback = NarrativeConversationEventPlayback.Play;
                target.ReadingPlayback = ReadingEventPlayback.Play;
                target.NoticePlayback = NarrativeNoticeEventPlayback.Play;
                target.LetterPlayback = LetterEventPlayback.Play;
                target.CharacterResolver = NarrativeCharacterResolver.Resolve;
                target.PlayerNameProvider = () => player.PlayerName;
            }
            if (PlayerCafeOwner.Current == null)
            {
                var catalog = Resources.Load<TextAsset>("FreeCafe/catalog");
                if (catalog == null) throw new InvalidOperationException("Missing FreeCafe/catalog for standalone gameplay.");
                PlayerCafeOwner.Bind(new CafeWorld(JsonUtility.FromJson<CafeCatalog>(catalog.text)));
            }
            if (runtime == target && world == PlayerCafeOwner.Current) return;
            coordinator.Bind(target, PlayerCafeOwner.Current, null);
            runtime = target;
            world = PlayerCafeOwner.Current;
            if (!player.CreationSubmitted) coordinator.Submit(null);
            // Establish value baselines without starting the formal opening or advancing NCS time.
            runtime.Controller.CompleteValueRestore(evaluateInitial: false);
            runtime.Actions.RefreshMapContent();
        }

        public bool TryStartCafeTutorial(GameObject owner, out string message)
        {
            Ensure();
            message = "独立玩家/剧情会话尚未就绪，请稍后重试。";
            if (player == null || PlayerRuntime.Current != player || runtime == null
                || !runtime.IsReady || !player.CreationSubmitted || world != PlayerCafeOwner.Current) return false;
            if (tutorialFlow != null)
            {
                message = "本次沙盒已经启动过首位客人教程；请重新运行 CoffeeShop 后再试。";
                return false;
            }
            if (runtime.Controller.IsEnded || runtime.Controller.IsBusy || !runtime.Controller.IsSafeBoundary
                || runtime.Actions.IsExecuting || runtime.Actions.CurrentDay != 1)
            {
                message = "当前剧情或行动未结束，或已不在首日；请重新运行 CoffeeShop 后再试。";
                return false;
            }
            tutorialFlow = owner.AddComponent<DayFlowController>();
            tutorialFlow.Initialize(null);
            // Keep the existing identity, economy and ability readers. Do not create a second NCS/player owner.
            coordinator.Bind(runtime, world, tutorialFlow);
            runtime.Controller.SetOfficialDay(1);
            runtime.Controller.CompleteValueRestore(evaluateInitial: false);
            if (!player.HasSelectedAvatar) player.SelectAvatar(player.Catalog.DefaultAvatar);
            tutorialFlow.BeginStandaloneCafeTutorial();
            message = "已启动首位客人到访剧情及完整咖啡馆教学；跳过独立序章、来信与角色创建，不自动保存。";
            return true;
        }

        public void Dispose()
        {
            if (tutorialFlow != null)
            {
                tutorialFlow.HideForMainMenu();
                UnityEngine.Object.Destroy(tutorialFlow);
                tutorialFlow = null;
            }
            if (runtime != null) coordinator.Dispose();
            if (PlayerRuntime.Current == player) PlayerRuntime.Current = null;
            if (runtime != null) runtime.PlayerNameProvider = null;
            player = null;
            runtime = null;
            world = null;
        }
    }
}
