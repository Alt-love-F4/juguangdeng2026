using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Apply the approved design plan to the existing ending artwork catalogue only.</summary>
    public static class NarrativeEndingArtRequirements
    {
        [Serializable] private sealed class Plan { public Requirement[] requirements; }
        [Serializable] private sealed class Requirement
        {
            public string id,endingId,displayCode,assetPath,reuseKey,requirement;
            public bool optional;
        }
        [MenuItem("Tools/Narrative/Apply Ending Art Requirement Plan")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Apply ending art in Edit Mode.");
            const string path="Assets/Resources/Dialogue/NarrativeEndingArtworkCatalog.asset";
            var catalog=AssetDatabase.LoadAssetAtPath<NarrativeEndingArtworkCatalog>(path);
            if(catalog==null)throw new InvalidOperationException("Existing ending artwork catalogue required.");
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText("Docs/NarrativeEndingArtManifest.json"));
            if(plan?.requirements==null||plan.requirements.Length!=16)throw new InvalidOperationException("Incomplete ending art revision.");
            var registry=AssetDatabase.LoadAssetAtPath<NarrativeStageVisuals>("Assets/Resources/Dialogue/NarrativeStageVisuals.asset");
            Undo.RecordObject(catalog,"Apply approved ending art plan");
            foreach(var requirement in plan.requirements)
            {
                var entry=catalog.Find(requirement.endingId);
                if(entry==null)throw new InvalidOperationException("Missing existing ending art record: "+requirement.endingId);
                entry.displayCode=requirement.displayCode;entry.artRequirementId=requirement.id;entry.notes=requirement.requirement;
                entry.status=string.IsNullOrEmpty(requirement.assetPath)?NarrativeEndingArtworkCatalog.ArtStatus.NotRequired:
                    requirement.optional?NarrativeEndingArtworkCatalog.ArtStatus.OptionalMissing:NarrativeEndingArtworkCatalog.ArtStatus.RequiredMissing;
                // Delivered artwork always wins; a rerun never overwrites it with a card.
                if(entry.artwork!=null&&!entry.artworkIsPlaceholder)continue;
                if(!string.IsNullOrEmpty(requirement.reuseKey))
                {
                    var texture=registry.Find(requirement.reuseKey)?.texture;
                    var texturePath=texture!=null?AssetDatabase.GetAssetPath(texture):null;
                    var sprite=!string.IsNullOrEmpty(texturePath)?AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().FirstOrDefault():null;
                    if(sprite==null)throw new InvalidOperationException("Missing complete dialogue composition for "+requirement.reuseKey);
                    entry.artwork=sprite;entry.artworkIsPlaceholder=false;
                }
                else if(!string.IsNullOrEmpty(requirement.assetPath))
                {
                    var importer=AssetImporter.GetAtPath(requirement.assetPath) as TextureImporter;
                    if(importer==null)throw new InvalidOperationException("Missing ending card: "+requirement.assetPath);
                    importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                    importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize=2048;importer.SaveAndReimport();
                    entry.artwork=AssetDatabase.LoadAssetAtPath<Sprite>(requirement.assetPath);entry.artworkIsPlaceholder=true;
                    if(entry.artwork==null)throw new InvalidOperationException("Cannot load ending card sprite "+requirement.id);
                }
            }
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
            Debug.Log("[NarrativeEnding] Applied 14 ending cards, one reuse composition and IF0 registration; authored gates unchanged.");
        }
    }
}
