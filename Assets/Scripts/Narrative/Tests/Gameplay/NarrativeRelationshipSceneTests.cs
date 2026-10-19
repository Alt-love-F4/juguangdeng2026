#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.Narrative.Tests
{
    /// <summary>Loads the actual dedicated read-only graph scene. No temporary panel/canvas/input setup.</summary>
    public sealed class NarrativeRelationshipSceneTests
    {
        [UnityTest] public IEnumerator ActualRelationshipSceneDrawsAndBrowsingNeverBootstrapsNarrative()
        {
            var before = NarrativeRuntime.Instance;
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.LoadSceneInPlayMode(NarrativeRelationshipSceneController.ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            SceneManager.SetActiveScene(scene);
            var root = System.Array.Find(scene.GetRootGameObjects(), g => g.GetComponent<NarrativeRelationshipSceneController>() != null);
            Assert.That(root, Is.Not.Null);
            var controller = root.GetComponent<NarrativeRelationshipSceneController>();
            // OnGUI is drawn by Unity; give it actual frames rather than invoking GUI outside its pass.
            yield return null; yield return null;
            var field = typeof(NarrativeRelationshipSceneController).GetField("view",BindingFlags.Instance|BindingFlags.NonPublic);
            var view = (NarrativeRelationshipGraphView)field.GetValue(controller);
            Assert.That(view, Is.Not.Null);
            view.Select("event:rose.1011.s1");
            yield return null;
            Assert.That(view.SelectedNodeId, Is.EqualTo("event:rose.1011.s1"));
            Assert.That(view.VisibleNodeCount, Is.GreaterThanOrEqualTo(62));
            Assert.That(NarrativeRuntime.Instance, Is.SameAs(before));
            Assert.That(scene.GetRootGameObjects().Length, Is.EqualTo(2));
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(scene);
            Assert.That(NarrativeRuntime.Instance, Is.SameAs(before));
        }
    }
}
#endif
