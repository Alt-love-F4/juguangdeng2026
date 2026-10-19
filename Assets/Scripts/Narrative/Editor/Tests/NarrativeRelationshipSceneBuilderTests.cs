using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.Narrative.Editor.Tests
{
    /// <summary>
    /// In-memory construction only: never invokes CreateScene, saves a .unity file,
    /// changes the user's scene assets, or updates EditorBuildSettings.
    /// </summary>
    public sealed class NarrativeRelationshipSceneBuilderTests
    {
        [Test]
        public void ScenePathUsesControllerContractAndIsNotPresentationDebugScene()
        {
            Assert.That(NarrativeRelationshipSceneBuilder.ScenePath,
                Is.EqualTo(NarrativeRelationshipSceneController.ScenePath));
            Assert.That(NarrativeRelationshipSceneBuilder.ScenePath,
                Is.EqualTo("Assets/Scenes/NarrativeRelationshipScene.unity"));
            Assert.That(NarrativeRelationshipSceneBuilder.ScenePath,
                Does.Not.Contain("PresentationDebugScene"));
        }

        [Test]
        public void BuildContainsOnlyCameraAndReadOnlyGraphWithoutChangingBuildSettings()
        {
            Scene previous = SceneManager.GetActiveScene();
            string[] buildSettings = EditorBuildSettings.scenes.Select(x => x.path + "|" + x.enabled).ToArray();
            Scene scratch = EditorSceneManager.NewPreviewScene();
            try
            {
                InvokePrivate("Build", scratch);
                GameObject[] roots = scratch.GetRootGameObjects();
                Assert.That(roots.Any(x => x.GetComponent<Camera>() != null), Is.True);
                Assert.That(roots.Any(x => x.GetComponent<NarrativeRelationshipSceneController>() != null), Is.True);
                foreach (GameObject root in roots)
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                    Assert.That(component is Transform || component is Camera ||
                        component is NarrativeRelationshipSceneController, Is.True,
                        "Relationship scene must not bootstrap gameplay or another UI/input hierarchy: " + component.GetType().FullName);
                Assert.That(EditorBuildSettings.scenes.Select(x => x.path + "|" + x.enabled).ToArray(),
                    Is.EqualTo(buildSettings));
                Assert.That(scratch.path, Is.Empty, "The structural test must not write a scene asset.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.ClosePreviewScene(scratch);
            }
        }

        [Test]
        public void DirtyTargetIsRejectedWithoutSavingOrDiscardingChanges()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scratch = EditorSceneManager.NewPreviewScene();
            try
            {
                EditorSceneManager.MarkSceneDirty(scratch);
                var exception = Assert.Throws<TargetInvocationException>(() => InvokePrivate("RequireCleanTarget", scratch));
                Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(scratch.isLoaded, Is.True);
                Assert.That(scratch.isDirty, Is.True);
                Assert.That(scratch.path, Is.Empty);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.ClosePreviewScene(scratch);
            }
        }

        private static void InvokePrivate(string name, Scene scene)
        {
            MethodInfo method = typeof(NarrativeRelationshipSceneBuilder).GetMethod(name,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(null, new object[] { scene });
        }
    }
}
