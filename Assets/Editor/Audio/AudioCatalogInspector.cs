using UnityEditor;
using UnityEngine;

namespace Spotlight.Audio.Editor
{
    [CustomEditor(typeof(AudioCatalog))]
    public sealed class AudioCatalogInspector : UnityEditor.Editor
    {
        private string search = string.Empty;
        private bool missingOnly;
        private readonly object previewOwner = new object();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("musicTransitionSeconds"), new GUIContent("音乐切换时长"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("videoVolume"), new GUIContent("视频音轨基础音量"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("videoCategory"), new GUIContent("视频音轨分类"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("warnMissing"), new GUIContent("运行时提醒缺素材"));
            search = EditorGUILayout.TextField("搜索 Key / 说明", search);
            missingOnly = EditorGUILayout.Toggle("只看缺失 / 临时替代", missingOnly);
            if (GUILayout.Button("停止试听")) GameAudio.StopOwner(previewOwner);
            SerializedProperty entries = serializedObject.FindProperty("entries");
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                string key = entry.FindPropertyRelative("key").stringValue;
                string description = entry.FindPropertyRelative("description").stringValue;
                bool missing = entry.FindPropertyRelative("clip").objectReferenceValue == null && string.IsNullOrWhiteSpace(entry.FindPropertyRelative("resourcePath").stringValue);
                bool provisional = entry.FindPropertyRelative("provisional").boolValue;
                if (!string.IsNullOrEmpty(search) && (key + description).IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (missingOnly && !missing && !provisional) continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, (missing ? "[缺素材] " : provisional ? "[临时] " : "") + key + " — " + description, true);
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("clip"), new GUIContent("音频素材"));
                if (entry.isExpanded)
                {
                    foreach (string field in new[] { "key", "description", "enabled", "resourcePath", "category", "loop", "volume", "pitch", "fadeIn", "fadeOut", "cooldown", "maxVoices", "provisional", "materialNote" })
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative(field));
                    using (new EditorGUI.DisabledScope(!Application.isPlaying || missing))
                        if (GUILayout.Button("试听（Play Mode）"))
                        {
                            serializedObject.ApplyModifiedProperties();
                            GameAudio.StopOwner(previewOwner);
                            AudioCue cue = ((AudioCatalog)target).Find(key);
                            if (cue.category == AudioCategory.Music) GameAudio.OverrideMusic(previewOwner, key);
                            else if (cue.loop) GameAudio.SetLoop(previewOwner, "preview", key, true);
                            else GameAudio.Play(key, previewOwner);
                        }
                }
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("新增声音配置"))
            {
                int index = entries.arraySize++;
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("key").stringValue = "new.cue." + index;
                entry.FindPropertyRelative("description").stringValue = string.Empty;
                entry.FindPropertyRelative("clip").objectReferenceValue = null;
                entry.FindPropertyRelative("resourcePath").stringValue = string.Empty;
                entry.FindPropertyRelative("enabled").boolValue = true;
                entry.FindPropertyRelative("category").enumValueIndex = (int)AudioCategory.Sfx;
                entry.FindPropertyRelative("loop").boolValue = false;
                entry.FindPropertyRelative("volume").floatValue = 1f;
                entry.FindPropertyRelative("pitch").floatValue = 1f;
                entry.FindPropertyRelative("fadeIn").floatValue = 0f;
                entry.FindPropertyRelative("fadeOut").floatValue = .15f;
                entry.FindPropertyRelative("cooldown").floatValue = .08f;
                entry.FindPropertyRelative("maxVoices").intValue = 3;
                entry.FindPropertyRelative("provisional").boolValue = false;
                entry.FindPropertyRelative("materialNote").stringValue = string.Empty;
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void OnDisable() => GameAudio.StopOwner(previewOwner);
    }
}
