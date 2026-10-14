using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.GameFlow
{
    /// <summary>Main-thread simulation gate. Never changes timeScale or presentation time.</summary>
    public static class PresentationPauseRegistry
    {
        private static readonly HashSet<Lease> owners = new HashSet<Lease>();
        public static bool IsPaused => owners.Count != 0;
        public static int OwnerCount => owners.Count;

        public static IDisposable Acquire(string reason)
        {
            Scene scene = SceneManager.GetActiveScene();
            var lease = new Lease(scene.handle, reason);
            owners.Add(lease);
            SceneManager.sceneUnloaded -= SceneUnloaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
            return lease;
        }

        private static void SceneUnloaded(Scene scene)
        {
            owners.RemoveWhere(owner => owner.SceneHandle == scene.handle);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            owners.Clear();
            SceneManager.sceneUnloaded -= SceneUnloaded;
        }

        private sealed class Lease : IDisposable
        {
            public readonly int SceneHandle;
            public readonly string Reason;
            public Lease(int sceneHandle, string reason) { SceneHandle = sceneHandle; Reason = reason; }
            public void Dispose() => owners.Remove(this);
        }
    }
}
