using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using YokiFrame;

namespace Spotlight.Audio
{
    internal sealed class CatalogAudioLoader : IAudioResourceLoader
    {
        private readonly IAudioResourceLoader fallback;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> durations = new Dictionary<string, float>();
        private readonly HashSet<object> borrowed = new HashSet<object>();
        public string LoaderName => "Spotlight.AudioCatalog";
        public CatalogAudioLoader(IAudioResourceLoader fallback) => this.fallback = fallback;

        public string Register(AudioClip clip)
        {
            string path = "spotlight-audio:" + clip.GetInstanceID();
            clips[path] = clip;
            durations[path] = clip.length;
            borrowed.Add(clip);
            return path;
        }

        public T Load<T>(string path) where T : class
        {
            T asset = clips.TryGetValue(path, out AudioClip clip) ? clip as T : fallback.Load<T>(path);
            if (asset is AudioClip loaded && loaded != null) durations[path] = loaded.length;
            return asset;
        }

        public float Duration(string path) => durations.TryGetValue(path, out float seconds) ? seconds : 30f;

        public async Task<T> LoadAsync<T>(string path, CancellationToken token) where T : class
        {
            token.ThrowIfCancellationRequested();
            T asset = clips.TryGetValue(path, out AudioClip clip) ? clip as T : await fallback.LoadAsync<T>(path, token);
            if (asset is AudioClip loaded && loaded != null) durations[path] = loaded.length;
            return asset;
        }

        public void Release(object asset)
        {
            if (!borrowed.Contains(asset)) fallback.Release(asset);
        }
    }
}
