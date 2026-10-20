using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Spotlight.Narrative
{
    public sealed class PresentationResources : IDisposable
    {
        private readonly NarrativePresentationUI ui;
        private readonly Dictionary<string, GameObject> npcs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly List<GameObject> effects = new List<GameObject>();
        private Camera camera;
        private Vector3 originalPosition;
        private float originalZoom;
        private bool cameraCaptured;

        public PresentationResources(NarrativePresentationUI ui)
        {
            this.ui = ui;
        }

        private T Load<T>(string path, string kind) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(path)) { Error(kind + " requires a Resources path."); return null; }
            T resource = Resources.Load<T>(path);
            if (resource == null) Error("Missing " + kind + " resource: Resources/" + path);
            return resource;
        }

        private void Error(string message) { Debug.LogError("[NCS][Presentation] " + message); ui.ShowHint(message); }

        public void Background(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (string.Equals(path, NarrativeBackgroundKeys.CafeScene, StringComparison.Ordinal))
            {
                ui.SetBackground(null);
                return;
            }
            Sprite sprite = Load<Sprite>(path, "background");
            if (sprite != null) ui.SetBackground(sprite);
        }

        public IEnumerator Portraits(string a, string b, string posA, string posB, float duration)
        {
            Image left = ui.Portrait(false), right = ui.Portrait(true);
            SetPortrait(left, a, posA); SetPortrait(right, b, posB);
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float alpha = Mathf.Clamp01(elapsed / duration);
                left.color = right.color = new Color(1, 1, 1, alpha); yield return null;
            }
            left.color = right.color = Color.white;
        }

        private void SetPortrait(Image image, string path, string position)
        {
            if (string.IsNullOrWhiteSpace(path)) { image.enabled = false; return; }
            Sprite sprite = Load<Sprite>(path, "portrait");
            image.sprite = sprite; image.enabled = sprite != null;
            RectTransform rect = image.rectTransform;
            bool right = string.Equals(position, "right", StringComparison.OrdinalIgnoreCase);
            bool center = string.Equals(position, "center", StringComparison.OrdinalIgnoreCase);
            float min = center ? .325f : right ? .6f : .05f;
            rect.anchorMin = new Vector2(min, .2f); rect.anchorMax = new Vector2(min + .35f, .92f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public IEnumerator Camera(float zoom, float shake, float duration, Func<bool> skipped)
        {
            camera = UnityEngine.Camera.main;
            if (camera == null) { Error("Camera atom requires Camera.main."); yield break; }
            if (!cameraCaptured)
            {
                originalPosition = camera.transform.position;
                originalZoom = camera.orthographic ? camera.orthographicSize : camera.fieldOfView;
                cameraCaptured = true;
            }
            float start = camera.orthographic ? camera.orthographicSize : camera.fieldOfView;
            float target = originalZoom / Mathf.Max(.01f, zoom);
            for (float elapsed = 0; elapsed < duration && !skipped(); elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float value = Mathf.Lerp(start, target, t * t * (3 - 2 * t));
                if (camera.orthographic) camera.orthographicSize = value; else camera.fieldOfView = value;
                camera.transform.position = originalPosition + new Vector3(Mathf.Sin(elapsed * 63), Mathf.Sin(elapsed * 47), 0) * shake * (1 - t);
                yield return null;
            }
            if (camera != null)
            {
                if (camera.orthographic) camera.orthographicSize = target; else camera.fieldOfView = target;
                camera.transform.position = originalPosition;
            }
        }

        public void RestoreCamera()
        {
            if (cameraCaptured && camera != null)
            {
                camera.transform.position = originalPosition;
                if (camera.orthographic) camera.orthographicSize = originalZoom; else camera.fieldOfView = originalZoom;
            }
            cameraCaptured = false;
        }

        public void Sfx(string path, float volume)
        {
            Spotlight.Audio.GameAudio.PlayNarrative(path, this, volume);
        }

        public void SetMusic(string key) => Spotlight.Audio.GameAudio.OverrideMusic(this, key);

        public IEnumerator Bgm(string path, float volume, float fadeIn, float fadeOut, Func<bool> skipped)
        {
            Spotlight.Audio.GameAudio.OverrideMusic(this, path, volume, fadeIn, fadeOut);
            for (float elapsed = 0; elapsed < Mathf.Max(fadeIn, fadeOut) && !skipped(); elapsed += Time.unscaledDeltaTime)
                yield return null;
        }

        public IEnumerator Fx(string path, float duration, Func<bool> skipped)
        {
            GameObject prefab = Load<GameObject>(path, "FX");
            if (prefab == null) yield break;
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            effects.Add(instance);
            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem particle in particles) particle.Play();
            for (float elapsed = 0; elapsed < Mathf.Max(.01f, duration) && !skipped(); elapsed += Time.unscaledDeltaTime) yield return null;
            effects.Remove(instance); UnityEngine.Object.Destroy(instance);
        }

        public void Npc(string id, string location, bool spawn)
        {
            if (npcs.TryGetValue(id, out GameObject previous)) { if (previous != null) UnityEngine.Object.Destroy(previous); npcs.Remove(id); }
            if (!spawn || string.IsNullOrWhiteSpace(location)) return;
            string destination = location.Trim();
            if (destination.StartsWith("scene:", StringComparison.OrdinalIgnoreCase)) destination = destination.Substring(6);
            // Cafe owns seats, orders and visitor entities from committed NCS visit snapshots.
            if (string.Equals(destination, "cafe", StringComparison.OrdinalIgnoreCase)) return;
            if (!string.Equals(destination, SceneManager.GetActiveScene().name, StringComparison.OrdinalIgnoreCase)) return;
            GameObject prefab = Load<GameObject>(id, "NPC");
            if (prefab == null) return;
            GameObject instance = UnityEngine.Object.Instantiate(prefab); instance.name = "NCS_NPC_" + id;
            npcs[id] = instance;
        }

        private bool suspended;
        private readonly HashSet<ParticleSystem> pausedParticles = new HashSet<ParticleSystem>();
        public void SetSuspended(bool value)
        {
            if (suspended == value) return;
            suspended = value;
            Spotlight.Audio.GameAudio.SetOwnerPaused(this, value);
            if (value)
            {
                foreach (var effect in effects)
                    if (effect != null) foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>())
                        if (particle.isPlaying) { pausedParticles.Add(particle); particle.Pause(false); }
            }
            else
            {
                foreach (var particle in pausedParticles) if (particle != null) particle.Play(false);
                pausedParticles.Clear();
            }
        }

        public void StopAudio() => Spotlight.Audio.GameAudio.StopOwner(this);

        public void Dispose()
        {
            RestoreCamera();
            StopAudio();
            foreach (GameObject effect in effects) if (effect != null) UnityEngine.Object.Destroy(effect);
            foreach (GameObject npc in npcs.Values) if (npc != null) UnityEngine.Object.Destroy(npc);
            effects.Clear(); npcs.Clear();
        }
    }
}
