using UnityEngine;

namespace Spotlight.Narrative
{
    /// <summary>Dedicated read-only relationship browser. Does not create presentation/gameplay hosts.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class NarrativeRelationshipSceneController : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/NarrativeRelationshipScene.unity";
        private NarrativeRelationshipGraphView view;
        private void OnGUI()
        {
            // Edit-mode authoring is served by the EditorWindow. A scene has a useful Game view in Play.
            if (!Application.isPlaying) return;
            view ??= new NarrativeRelationshipGraphView();
            int old = GUI.depth; GUI.depth = -500;
            view.Draw(new Rect(0,0,Screen.width,Screen.height));GUI.depth = old;
        }
        private void OnDisable(){view?.Dispose();view=null;}
        private void OnDestroy(){view?.Dispose();view=null;}
    }
}
