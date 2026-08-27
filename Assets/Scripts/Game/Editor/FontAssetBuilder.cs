using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace STS.Game.Editor
{
    /// <summary>
    /// 從 Assets/Fonts/NotoSansTC-Regular.otf 生成動態 TMP 字型資產。
    /// 動態模式 + 多圖集:CJK 字形量大,啟動時不預烘、用到才加進圖集。
    /// </summary>
    public static class FontAssetBuilder
    {
        private const string SourceFontPath = "Assets/Fonts/NotoSansTC-Regular.otf";
        private const string OutputPath = "Assets/Fonts/Resources/NotoSansTC SDF.asset";

        [MenuItem("STS/生成 TMP 繁中字型資產")]
        public static void Generate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath);
            if (existing != null)
            {
                Debug.Log($"字型資產已存在,略過生成:{OutputPath}");
                return;
            }
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"找不到來源字型:{SourceFontPath}");
                return;
            }
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont, 48, 6, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            if (fontAsset == null)
            {
                Debug.LogError("TMP 字型資產生成失敗");
                return;
            }
            fontAsset.name = "NotoSansTC SDF";
            AssetDatabase.CreateAsset(fontAsset, OutputPath);
            // 圖集與材質是子資產,不掛上去會變成散落的隱形物件
            fontAsset.atlasTexture.name = "NotoSansTC Atlas";
            fontAsset.material.name = "NotoSansTC Material";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"字型資產已生成:{OutputPath}");
        }
    }
}
