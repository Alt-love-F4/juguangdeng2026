using System;
using System.IO;
using Spotlight.FreeCafe;
using UnityEditor;
using UnityEngine;

namespace Spotlight.FreeCafe.Editor
{
    public static class FreeCafeGlassCupBuilder
    {
        [MenuItem("Tools/Free Cafe/Update Orthographic Glass Cups")]
        public static void UpdateCups()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Exit Play Mode and wait for compilation before updating cups.");
            string backup = "output/glass-cups/backup/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            UpdateCup("cup.small", backup);
            UpdateCup("cup.large", backup);
            AssetDatabase.SaveAssets();
            Debug.Log("[FreeCafe Glass Cups] Updated both cup prefabs; originals backed up in " + backup);
        }

        private static void UpdateCup(string specId, string backup)
        {
            string path = FreeCafeEntityPrefabBuilder.PathFor(specId);
            File.Copy(path, Path.Combine(backup, Path.GetFileName(path)));
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<FreeEntityView>();
                if (view == null || view.PrefabSpecId != specId)
                    throw new InvalidOperationException("Invalid cup prefab: " + path);
                view.ConfigureGlassCup();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
