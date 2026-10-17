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

        public void Dispose()
        {
            if (runtime != null) coordinator.Dispose();
            if (PlayerRuntime.Current == player) PlayerRuntime.Current = null;
            if (runtime != null) runtime.PlayerNameProvider = null;
            player = null;
            runtime = null;
            world = null;
        }
    }
}
