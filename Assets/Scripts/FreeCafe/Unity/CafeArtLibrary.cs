using System;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    [Serializable] public sealed class ArtBrief
    {
        public string id,label,category,pivot,outputFolder,prompt,negativePrompt;
        public int width,height; public bool transparent; public float worldWidth,worldHeight; public string[] states;
    }
    [Serializable] public sealed class ArtBriefFile { public string style; public ArtBrief[] briefs; }
    [Serializable] public sealed class ArtVariant { public string state="default"; public Sprite sprite; }
    [Serializable] public sealed class CafeArtEntry { public ArtBrief brief; public Sprite whitebox; public ArtVariant[] variants; }
    [CreateAssetMenu(menuName="Free Cafe/Art Library")]
    public sealed class CafeArtLibrary : ScriptableObject
    {
        public CafeArtEntry[] entries;
        public CafeArtEntry Find(string id)
        {
            if(entries!=null) foreach(var entry in entries) if(entry!=null && entry.brief!=null && entry.brief.id==id) return entry;
            return null;
        }
        public Sprite Resolve(string id,string state="default")
        {
            var entry=Find(id); if(entry==null) return null;
            if(entry.variants!=null)
            {
                foreach(var variant in entry.variants) if(variant.state==state && variant.sprite!=null) return variant.sprite;
                foreach(var variant in entry.variants) if(variant.state=="default" && variant.sprite!=null) return variant.sprite;
            }
            return entry.whitebox;
        }
    }
}
