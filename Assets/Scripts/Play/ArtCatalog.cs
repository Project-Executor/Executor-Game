using System.Collections.Generic;
using Executor.Data;
using UnityEngine;

namespace Executor.Play
{
    /// <summary>
    /// 그래픽 찾기. 정해진 Resources 경로에 이미지가 있으면 그 이미지를, 없으면 임시 색 상자를 쓴다.
    /// 이미지를 그 경로에 넣기만 하면 코드 수정 없이 바뀐다.
    /// <list type="bullet">
    ///   <item>도트 (16x16, Pixels Per Unit 16, Filter Point):
    ///     Resources/Pixel/tiles/floor, Resources/Pixel/tiles/wall,
    ///     Resources/Pixel/objects/{exit|crate|breakable|key|door|npc}, Resources/Pixel/player,
    ///     인물별 도트는 Resources/Pixel/npcs/{인물ID} (없으면 objects/npc)</item>
    ///   <item>스탠딩 일러스트: Resources/Portraits/{인물ID}/{표정} (예: Portraits/cassia/smile)</item>
    /// </list>
    /// </summary>
    public static class ArtCatalog
    {
        public const int TilePixels = 16;

        /// <summary>대화의 speaker 이름 -> 인물 ID (일러스트·도트 폴더 이름)</summary>
        public static readonly Dictionary<string, string> SpeakerToCharacter = new Dictionary<string, string>
        {
            ["집행자"] = "player",
            ["연구원"] = "researcher",
            ["감독관"] = "supervisor",
            ["카시아"] = "cassia",
        };

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Floor => Pixel("tiles/floor", new Color32(0x1b, 0x2a, 0x3a, 0xff), Shape.Frame);
        public static Sprite Wall => Pixel("tiles/wall", new Color32(0x56, 0x68, 0x80, 0xff), Shape.Solid);
        public static Sprite Player => Pixel("player", new Color32(0x4f, 0xd1, 0xff, 0xff), Shape.Diamond);

        /// <summary>오브젝트 그래픽. npc는 인물별 도트가 있으면 그것을 쓴다</summary>
        public static Sprite ForObject(string type, string characterId = null)
        {
            if (type == MapObjectTypes.Npc && characterId != null)
            {
                Sprite own = Load<Sprite>("Pixel/npcs/" + characterId);
                if (own != null) return own;
            }
            switch (type)
            {
                case MapObjectTypes.Exit: return Pixel("objects/exit", new Color32(0x3c, 0xcf, 0x7a, 0xff), Shape.Frame);
                case MapObjectTypes.Crate: return Pixel("objects/crate", new Color32(0xb0, 0x7a, 0x3c, 0xff), Shape.Cross);
                case MapObjectTypes.Breakable: return Pixel("objects/breakable", new Color32(0x9a, 0x9a, 0xa8, 0xff), Shape.Crack);
                case MapObjectTypes.Key: return Pixel("objects/key", new Color32(0xff, 0xd2, 0x3f, 0xff), Shape.Small);
                case MapObjectTypes.Door: return Pixel("objects/door", new Color32(0xc0, 0x45, 0x55, 0xff), Shape.Bar);
                case MapObjectTypes.Npc: return Pixel("objects/npc", new Color32(0xb0, 0x7c, 0xf0, 0xff), Shape.Diamond);
                default: return Pixel("objects/" + type, new Color32(0xff, 0x00, 0xff, 0xff), Shape.Small);
            }
        }

        /// <summary>
        /// 대화 노드의 스탠딩 일러스트. portrait("인물/표정")가 있으면 그것, 없으면 speaker로 인물을 찾고 표정은 default.
        /// 이미지가 없으면 null (화면은 이름표만 있는 임시 상자를 보여 준다)
        /// </summary>
        public static Sprite Portrait(string portrait, string speaker, out string characterId)
        {
            string expression = "default";
            characterId = null;
            if (!string.IsNullOrEmpty(portrait))
            {
                string[] parts = portrait.Split('/');
                characterId = parts[0];
                if (parts.Length > 1) expression = parts[1];
            }
            else if (speaker != null && SpeakerToCharacter.TryGetValue(speaker, out string id))
            {
                characterId = id;
            }
            if (characterId == null) return null;
            return Load<Sprite>($"Portraits/{characterId}/{expression}")
                   ?? (expression != "default" ? Load<Sprite>($"Portraits/{characterId}/default") : null);
        }

        private enum Shape { Solid, Frame, Cross, Crack, Small, Bar, Diamond }

        private static Sprite Pixel(string path, Color32 color, Shape shape)
        {
            return Load<Sprite>("Pixel/" + path) ?? Placeholder(path, color, shape);
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = Resources.Load<T>(path);
            if (asset != null || typeof(T) != typeof(Sprite)) return asset;
            // 텍스처 타입이 Sprite가 아닌 이미지도 받아 준다
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                texture.width >= 64 ? 100 : TilePixels) as T;
        }

        /// <summary>16x16 임시 도트. 테두리와 모양으로 종류를 구분한다</summary>
        private static Sprite Placeholder(string key, Color32 color, Shape shape)
        {
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;
            const int n = TilePixels;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var dark = new Color32((byte)(color.r / 2), (byte)(color.g / 2), (byte)(color.b / 2), 255);
            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool edge = x == 0 || y == 0 || x == n - 1 || y == n - 1;
                int cx = x - n / 2, cy = y - n / 2;
                Color32 c = shape switch
                {
                    Shape.Solid => edge ? dark : color,
                    Shape.Frame => edge ? color : dark,
                    Shape.Cross => edge || x == y || x == n - 1 - y ? dark : color,
                    Shape.Crack => edge || (x == n / 2 && y > 3) || (y == n / 2 && x < 10) ? dark : color,
                    Shape.Small => System.Math.Abs(cx + 0.5f) < 4 && System.Math.Abs(cy + 0.5f) < 4 ? color : clear,
                    Shape.Bar => edge ? dark : (x == n / 2 || x == n / 2 - 1) && y > 3 && y < 12 ? dark : color,
                    Shape.Diamond => System.Math.Abs(cx + 0.5f) + System.Math.Abs(cy + 0.5f) < 7.5f ? color : clear,
                    _ => color
                };
                pixels[y * n + x] = c;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), TilePixels);
            sprite.name = "placeholder:" + key;
            Cache[key] = sprite;
            return sprite;
        }
    }
}
