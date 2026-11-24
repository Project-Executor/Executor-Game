using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Executor.Network;
using UnityEngine;

namespace Executor.Play
{
    /// <summary>
    /// 프로필 사진. 서버(GET /profiles/{userId}/avatar)에서 받아 (사용자, 버전)별로 기억한다.
    /// 버전(avatarVersion)은 사진을 바꾼 시각이라, 값이 같으면 다시 받지 않는다. 사진이 없으면 기본 실루엣(Resources/UI/profile/avatar_default).
    /// </summary>
    public static class AvatarStore
    {
        /// <summary>올릴 때 자르는 크기 (정사각형)</summary>
        public const int Size = 256;

        private static readonly Dictionary<long, (long version, Texture2D texture)> Cache = new Dictionary<long, (long, Texture2D)>();

        /// <summary>내 사진이 바뀌면 (작업 표시줄·프로필 창이 다시 그린다)</summary>
        public static event Action Changed;

        public static Texture2D Default => ArtCatalog.Ui("profile/avatar_default")?.texture;

        /// <summary>이미 받아 둔 사진 (없거나 버전이 다르면 null)</summary>
        public static Texture2D Cached(long userId, long? version) =>
            version != null && Cache.TryGetValue(userId, out var c) && c.version == version ? c.texture : null;

        /// <summary>사진을 받는다. 사진이 없거나 받지 못하면 null (기본 사진을 쓴다)</summary>
        public static async Task<Texture2D> GetAsync(long userId, long? version)
        {
            if (version == null) return null;
            Texture2D cached = Cached(userId, version);
            if (cached != null) return cached;
            try
            {
                byte[] bytes = await ProfileApi.GetAvatarAsync(userId);
                if (bytes == null) return null;
                Texture2D texture = Decode(bytes);
                if (texture != null) Put(userId, version.Value, texture);
                return texture;
            }
            catch (ApiException e)
            {
                Debug.LogWarning("[AvatarStore] 프로필 사진을 받지 못했습니다: " + e.Message);
                return null;
            }
        }

        /// <summary>방금 올린 내 사진을 바로 기억한다 (다시 받지 않게)</summary>
        public static void Put(long userId, long version, Texture2D texture)
        {
            Cache[userId] = (version, texture);
            Changed?.Invoke();
        }

        public static void Forget(long userId)
        {
            Cache.Remove(userId);
            Changed?.Invoke();
        }

        /// <summary>PNG·JPEG 바이트 -> 텍스처. 그림이 아니면 null</summary>
        public static Texture2D Decode(byte[] bytes)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            if (texture.LoadImage(bytes)) return texture;
            UnityEngine.Object.Destroy(texture);
            return null;
        }

        /// <summary>source에서 픽셀 사각형(왼쪽 아래 기준)을 잘라 size x size로 줄인 PNG 바이트</summary>
        public static byte[] CropToPng(Texture2D source, RectInt area, int size = Size)
        {
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Vector2 scale = new Vector2((float)area.width / source.width, (float)area.height / source.height);
            Vector2 offset = new Vector2((float)area.x / source.width, (float)area.y / source.height);
            Graphics.Blit(source, rt, scale, offset);
            RenderTexture.active = rt;
            var result = new Texture2D(size, size, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            byte[] png = result.EncodeToPNG();
            UnityEngine.Object.Destroy(result);
            return png;
        }
    }
}
