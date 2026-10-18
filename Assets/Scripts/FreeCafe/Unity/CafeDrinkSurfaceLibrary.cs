using System;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>
    /// 杯口液面贴图：识别出的饮品 → 一张椭圆液面图；每种杯子标记自己的杯口椭圆。
    /// 坐标都以 Sprite 矩形归一化（左下为 0,0），与图集和 PPU 无关。
    /// </summary>
    [CreateAssetMenu(menuName = "Free Cafe/Drink Surface Library", fileName = "DrinkSurfaces")]
    public sealed class CafeDrinkSurfaceLibrary : ScriptableObject
    {
        public const string ResourcePath = "FreeCafe/DrinkSurfaces";

        [Serializable]
        public sealed class DrinkSurface
        {
            public string productId;
            public Sprite sprite;
        }

        [Serializable]
        public sealed class CupMouth
        {
            [Tooltip("实体规格 id，例如 cup.small / cup.large")]
            public string specId;
            [Tooltip("杯口椭圆中心（杯子 Sprite 归一化坐标，左下为原点）")]
            public Vector2 center = new Vector2(.5f, .8f);
            [Tooltip("杯口椭圆半径（以杯子 Sprite 宽 / 高归一化）")]
            public Vector2 radius = new Vector2(.35f, .15f);
            [Tooltip("液面相对杯口内沿的缩放，略小于 1 可露出一圈杯壁")]
            [Range(.5f, 1.2f)] public float inset = .96f;
        }

        [Header("液面贴图里的椭圆（所有贴图共用同一画框）")]
        public Vector2 surfaceCenter = new Vector2(.5f, .3772f);
        public Vector2 surfaceRadius = new Vector2(.484375f, .34598f);

        [Header("显示")]
        [Min(0f)] public float fadeSeconds = .18f;

        public CupMouth[] cups = Array.Empty<CupMouth>();
        public DrinkSurface[] drinks = Array.Empty<DrinkSurface>();

        private static CafeDrinkSurfaceLibrary loaded;
        private static bool attempted;

        // Survives "Enter Play Mode without domain reload": look the asset up again each session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { loaded = null; attempted = false; }

        public static CafeDrinkSurfaceLibrary Default
        {
            get
            {
                if (!attempted)
                {
                    attempted = true;
                    loaded = Resources.Load<CafeDrinkSurfaceLibrary>(ResourcePath);
                }
                return loaded;
            }
        }

        public CupMouth Cup(string specId)
        {
            if (string.IsNullOrEmpty(specId) || cups == null) return null;
            foreach (var cup in cups) if (cup != null && cup.specId == specId) return cup;
            return null;
        }

        public Sprite Surface(string productId)
        {
            if (string.IsNullOrEmpty(productId) || drinks == null) return null;
            foreach (var drink in drinks) if (drink != null && drink.productId == productId) return drink.sprite;
            return null;
        }
    }
}
