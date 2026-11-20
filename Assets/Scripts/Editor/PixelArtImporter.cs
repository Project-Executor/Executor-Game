using UnityEditor;
using UnityEngine;

namespace Executor.EditorTools
{
    /// <summary>
    /// Assets/Resources/Pixel/ 아래 도트 그림의 가져오기 설정을 자동으로 맞춘다 (docs/art-guide.md).
    /// Point 필터(흐려지지 않게), 압축 없음, 밉맵 없음. 그림을 넣거나 바꿀 때마다 적용된다.
    /// 크기(한 칸에 맞추기, 플레이어 2칸 높이)는 코드(ArtCatalog)가 정하므로 Pixels Per Unit은 상관없다
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith("Assets/Resources/Pixel/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
