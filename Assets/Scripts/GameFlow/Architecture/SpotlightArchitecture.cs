using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// Project-wide YokiFrame service composition root.
    /// Register shared pure services here; scene-owned systems must register through an explicit boundary.
    /// </summary>
    public sealed class SpotlightArchitecture : Architecture<SpotlightArchitecture>
    {
        protected override void OnInit()
        {
            Register(new GameSettingsServiceAdapter());
            Register(new GameSaveServiceAdapter());
        }
    }

    /// <summary>Yoki service facade for the existing settings implementation.</summary>
    public sealed class GameSettingsServiceAdapter : AbstractService
    {
        private GameSettingsService mService;

        public GameSettingsService Service
        {
            get { return mService; }
        }

        protected override void OnInit()
        {
            mService = new GameSettingsService();
        }

        protected override void OnDispose()
        {
            mService = null;
        }
    }

    /// <summary>Yoki service facade for the existing save implementation.</summary>
    public sealed class GameSaveServiceAdapter : AbstractService
    {
        private GameSaveService mService;

        public GameSaveService Service
        {
            get { return mService; }
        }

        protected override void OnInit()
        {
            mService = new GameSaveService();
        }

        protected override void OnDispose()
        {
            mService = null;
        }
    }
}
