using System.IO;
using UnityEditor;
using UnityEngine;

namespace Spotlight.EditorTools
{
    /// <summary>
    /// 为 Assets/Resources/Art/UI 下的像素素材自动应用正确的导入设置:
    /// Point 过滤 / 关闭压缩与 mipmap / 九宫格边框 / 加载动画帧切片。
    /// 这套设置是像素素材放大后保持锐利的前提。
    /// 如果导入设置由其他流程接管, 直接删除本文件即可。
    /// </summary>
    public sealed class PixelUIImportSettings : AssetPostprocessor
    {
        private const string Root = "/Art/UI/";

        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains(Root))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 100f;
            importer.maxTextureSize = 2048;

            string fileName = Path.GetFileName(path);

            if (fileName == "loading_logo_sheet.png")
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritesheet = BuildLoadingFrames();
                return;
            }

            Vector4 border = BorderFor(path, fileName);
            if (border != Vector4.zero)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = border;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
        }

        private static Vector4 BorderFor(string path, string fileName)
        {
            if (path.Contains("/Panels/"))
                return new Vector4(12f, 12f, 12f, 12f);
            if (path.Contains("/Slots/"))
                return new Vector4(10f, 10f, 10f, 10f);
            if (path.Contains("/Buttons/"))
                return fileName.StartsWith("btn_small")
                    ? new Vector4(5f, 5f, 5f, 5f)
                    : new Vector4(8f, 8f, 8f, 8f);
            if (path.Contains("/Widgets/"))
            {
                if (fileName.StartsWith("dropdown")) return new Vector4(8f, 8f, 8f, 8f);
                if (fileName.StartsWith("progress_track")) return new Vector4(6f, 6f, 6f, 6f);
                if (fileName.StartsWith("progress_fill")) return new Vector4(4f, 4f, 4f, 4f);
                if (fileName.StartsWith("slider_track")) return new Vector4(4f, 4f, 4f, 4f);
                if (fileName.StartsWith("slider_fill")) return new Vector4(3f, 3f, 3f, 3f);
            }
            return Vector4.zero;
        }

        private static SpriteMetaData[] BuildLoadingFrames()
        {
            const int size = 64;
            const int count = 8;
            var frames = new SpriteMetaData[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = new SpriteMetaData
                {
                    name = "loading_logo_" + i.ToString("00"),
                    rect = new Rect(i * size, 0, size, size),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                };
            }
            return frames;
        }

        [MenuItem("MVP/重新导入像素 UI 素材")]
        private static void ReimportPixelUI()
        {
            AssetDatabase.ImportAsset("Assets/Resources/Art/UI",
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Debug.Log("已按像素素材设置重新导入 Assets/Resources/Art/UI");
        }
    }
}
