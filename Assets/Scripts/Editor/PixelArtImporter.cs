using UnityEditor;
using UnityEngine;

namespace Executor.EditorTools
{
    /// <summary>
    /// Assets/Resources/Pixel/ 아래 그림의 가져오기 설정을 자동으로 맞춘다 (docs/art-guide.md). 그림을 넣거나 바꿀 때마다 적용된다.
    /// <list type="bullet">
    ///   <item>작은 도트(긴 변 256px 이하, 예: 64x64, 72x128): Point 필터, 압축 없음, 밉맵 없음 → 도트가 흐려지지 않는다</item>
    ///   <item>큰 그림(예: 749x749): Bilinear + 밉맵 → 한 칸(약 100~150px)으로 크게 줄일 때 계단·반짝임 없이 부드럽게</item>
    /// </list>
    /// 크기(한 칸에 맞추기, 플레이어 2칸 높이)는 코드(ArtCatalog)가 정하므로 Pixels Per Unit은 상관없다
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        /// <summary>이 크기보다 크면 "큰 그림"으로 본다</summary>
        public const int SmallArtMax = 256;

        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (path.StartsWith("Assets/Resources/UI/"))
            {
                // UI 그림: 작은 도트 아이콘(16x16 등)은 Point로 크게 늘려 그리고, 큰 그림은 부드럽게. 둘 다 압축 없이
                var ui = (TextureImporter)assetImporter;
                ui.GetSourceTextureWidthAndHeight(out int uw, out int uh);
                bool dot = Mathf.Max(uw, uh) <= SmallArtMax;
                ui.textureType = TextureImporterType.Sprite;
                ui.spriteImportMode = SpriteImportMode.Single;
                ui.filterMode = dot ? FilterMode.Point : FilterMode.Trilinear;
                ui.textureCompression = TextureImporterCompression.Uncompressed;
                ui.mipmapEnabled = !dot;
                ui.alphaIsTransparency = true;
                ui.wrapMode = TextureWrapMode.Clamp;
                return;
            }
            if (!path.StartsWith("Assets/Resources/Pixel/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            bool small = Mathf.Max(width, height) <= SmallArtMax;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = small ? FilterMode.Point : FilterMode.Bilinear;
            importer.textureCompression = small ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.mipmapEnabled = !small;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            if (!small) importer.maxTextureSize = 1024; // 한 칸에 그리기엔 충분, 용량은 줄인다
        }
    }
}
