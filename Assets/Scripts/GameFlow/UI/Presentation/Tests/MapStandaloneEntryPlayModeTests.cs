#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    /// <summary>Exercise the real standalone Map entry without a main-flow bootstrap.</summary>
    public sealed class MapStandaloneEntryPlayModeTests
    {
        private float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            Assert.That(Object.FindFirstObjectByType<GameFlowBootstrap>(), Is.Null,
                "This regression must use the standalone map entry, without the main flow.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale;
            ShortLoadingPanel panel = UIKit.GetPanel<ShortLoadingPanel>();
            if (panel != null) panel.EndLoading();
            Scene map = SceneManager.GetSceneByName("Map");
            if (map.IsValid() && map.isLoaded) SceneManager.SetActiveScene(map);
            Scene cafe = SceneManager.GetSceneByName("CoffeeShop");
            if (cafe.IsValid() && cafe.isLoaded)
                yield return SceneManager.UnloadSceneAsync(cafe);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StandaloneMapEntersCafeAndClosesLoading()
        {
            yield return EnterCafe(1f);
        }

        [UnityTest]
        public IEnumerator StandaloneMapEntersCafeAndClosesLoadingWhileTimeScaleIsZero()
        {
            yield return EnterCafe(0f);
        }

        private IEnumerator EnterCafe(float timeScale)
        {
            MapSceneController map = null;
            foreach (MapSceneController candidate in Object.FindObjectsByType<MapSceneController>(FindObjectsSortMode.None))
                if (candidate.isActiveAndEnabled && candidate.transform is RectTransform)
                    map = candidate;
            Assert.That(map, Is.Not.Null, "Map must instantiate its authored panel.");
            Time.timeScale = timeScale;

            map.transform.Find("MapViewport/MapContent/Entities/Location:cafe")
                .GetComponent<Button>().onClick.Invoke();
            yield return null;
            var popup = map.GetComponentInChildren<MapActionDetailsView>(true);
            var entry = popup.transform.Find("Viewport/ActionList/EnterCafe");
            Assert.That(entry, Is.Not.Null);
            Transform execute = entry.Find("Execute");
            if (execute == null) execute = entry.Find("ExecutePreview");
            Assert.That(execute, Is.Not.Null);
            execute.GetComponent<Button>().onClick.Invoke();
            // Repeated input during the transition must not start another scene load.
            execute.GetComponent<Button>().onClick.Invoke();

            float deadline = Time.realtimeSinceStartup + 15f;
            while (SceneManager.GetActiveScene().name != "CoffeeShop" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CoffeeShop"),
                "The map's coroutine must survive until it allows scene activation.");
            Assert.That(SceneManager.GetSceneByName("CoffeeShop").isLoaded, Is.True);
            Assert.That(SceneManager.GetSceneByName("Map").isLoaded, Is.True,
                "The map stays resident for returning from gameplay.");
            Assert.That(map.gameObject.activeSelf, Is.False,
                "Hide the map only after CoffeeShop has finished loading.");

            deadline = Time.realtimeSinceStartup + 5f;
            ShortLoadingPanel loading = UIKit.GetPanel<ShortLoadingPanel>();
            while (loading != null && loading.gameObject.activeInHierarchy && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(loading == null || !loading.gameObject.activeInHierarchy, Is.True,
                "The transition must finish and release its full-screen input blocker.");
            int cafeCount = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).name == "CoffeeShop") cafeCount++;
            Assert.That(cafeCount, Is.EqualTo(1));
        }
    }
}
#endif
