using System.Collections.Generic;
using Executor.Data;
using Executor.Game;
using UnityEngine;

namespace Executor.Play
{
    /// <summary>
    /// 그래픽 찾기. 정해진 Resources 경로에 이미지가 있으면 그 이미지를, 없으면 임시 색 상자를 쓴다.
    /// 이미지를 그 경로에 넣기만 하면 코드 수정 없이 바뀐다.
    /// <list type="bullet">
    ///   <item>도트 (16x16, Pixels Per Unit 16, Filter Point):
    ///     Resources/Pixel/tiles/floor, Resources/Pixel/tiles/wall,
    ///     Resources/Pixel/objects/{exit|crate|breakable|key|door|npc|checkpoint|hidden|anomaly|computer|computerRestored}, Resources/Pixel/player,
    ///     인물별 도트는 Resources/Pixel/npcs/{인물ID} (없으면 objects/npc)</item>
    ///   <item>스탠딩 일러스트: Resources/Portraits/{인물ID}/{표정} (예: Portraits/cassia/smile)</item>
    ///   <item>도감 그림: Resources/Guidebook/{이름} (도감 image 값의 파일 이름, 확장자 제외)</item>
    ///   <item>화면 일러스트: Resources/Illustrations/title (시작 화면 배경), title_logo (시작 화면 로고),
    ///     main (메인 화면 가운데 그림)</item>
    /// </list>
    /// </summary>
    public static class ArtCatalog
    {
        public const int TilePixels = 16;

        /// <summary>
        /// 그림 배율: 한 칸(16px 도트)을 4배로 그려 64px. 플레이어처럼 한 칸보다 큰 그림은 이 값으로 크기를 정한다
        /// (72x128 그림 = 가로 1.125칸, 세로 2칸). 타일·오브젝트는 크기와 상관없이 한 칸에 꼭 맞춘다
        /// </summary>
        public const int ArtPixelsPerTile = 64;

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
        /// <summary>복구된 컴퓨터 (Resources/Pixel/objects/computerRestored)</summary>
        public static Sprite ComputerRestored => Pixel("objects/computerRestored", new Color32(0x5f, 0xd1, 0xb0, 0xff), Shape.Frame);
        public static Sprite Player => Pixel("player", new Color32(0x4f, 0xd1, 0xff, 0xff), Shape.Diamond);

        private static readonly Dictionary<Direction, Sprite[]> PlayerCache = new Dictionary<Direction, Sprite[]>();
        private static Sprite[] _breakableStages;

        /// <summary>
        /// 방화벽(breakable) 단계 그림: Pixel/objects/breakable_0(그대로), _1(금 감), _2(거의 부서짐) ...
        /// 없으면 빈 배열 (그때는 breakable.png 한 장이나 임시 도트를 남은 내구도만큼 어둡게 칠한다)
        /// </summary>
        public static Sprite[] BreakableStages
        {
            get
            {
                if (_breakableStages != null) return _breakableStages;
                var list = new List<Sprite>();
                for (int i = 0; i < 8; i++)
                {
                    Sprite s = CellSprite($"Pixel/objects/breakable_{i}");
                    if (s == null) break;
                    list.Add(s);
                }
                _breakableStages = list.ToArray();
                return _breakableStages;
            }
        }

        /// <summary>
        /// 남은 내구도에 맞는 방화벽 단계: 처음(다치지 않음) 0, 한 번만 더 치면 부서질 때(hp 1) 마지막 단계, 그 사이는 가운데 단계들.
        /// 단계가 3개면 hp 3 -> 0, 2 -> 1, 1 -> 2 / hp 2 -> 0, 1 -> 2 / hp 1 -> 0
        /// </summary>
        public static int BreakableStage(int hp, int maxHp, int stages)
        {
            if (stages <= 1 || hp >= maxHp) return 0;
            if (hp <= 1) return stages - 1;
            // 가운데 단계: 친 횟수 비율로 1 ~ stages-2
            int middle = stages - 2;
            if (middle <= 0) return stages - 1;
            int hits = maxHp - hp;
            return 1 + Mathf.Min(middle - 1, (hits - 1) * middle / Mathf.Max(1, maxHp - 2));
        }

        /// <summary>
        /// 플레이어 걷기 프레임 (Resources/Pixel/player/{front|back|left|right}_{0,1,2...}.png, 0번이 멈춘 자세).
        /// Art/player의 GIF를 Tools/gif_to_frames.py로 나눈 것이다. 발이 칸 아래쪽에 닿게 세우고, 위쪽은 윗칸까지 올라간다.
        /// 프레임이 없으면 Pixel/player.png 한 장, 그것도 없으면 임시 도트
        /// </summary>
        public static Sprite[] PlayerFrames(Direction facing)
        {
            if (PlayerCache.TryGetValue(facing, out Sprite[] cached)) return cached;
            string name = facing switch
            {
                Direction.Up => "back",
                Direction.Left => "left",
                Direction.Right => "right",
                _ => "front",
            };
            var frames = new List<Sprite>();
            for (int i = 0; i < 32; i++)
            {
                var texture = Resources.Load<Texture2D>($"Pixel/player/{name}_{i}");
                if (texture == null) break;
                texture.filterMode = FilterMode.Point;
                float pivotY = ArtPixelsPerTile / 2f / texture.height; // 칸 가운데에서 반 칸 아래 = 발
                frames.Add(Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, pivotY), ArtPixelsPerTile));
            }
            if (frames.Count == 0) frames.Add(Player);
            Sprite[] result = frames.ToArray();
            PlayerCache[facing] = result;
            return result;
        }

        /// <summary>오브젝트 그래픽. npc는 인물별 도트가 있으면 그것을 쓴다</summary>
        public static Sprite ForObject(string type, string characterId = null)
        {
            if (type == MapObjectTypes.Npc && characterId != null)
            {
                Sprite own = CellSprite("Pixel/npcs/" + characterId);
                if (own != null) return own;
            }
            switch (type)
            {
                case MapObjectTypes.Exit: return Pixel("objects/exit", new Color32(0x3c, 0xcf, 0x7a, 0xff), Shape.Frame);
                case MapObjectTypes.Crate: return Pixel("objects/crate", new Color32(0xb0, 0x7a, 0x3c, 0xff), Shape.Cross);
                case MapObjectTypes.Breakable:
                    return BreakableStages.Length > 0 ? BreakableStages[0] : Pixel("objects/breakable", new Color32(0x9a, 0x9a, 0xa8, 0xff), Shape.Crack);
                case MapObjectTypes.Key: return Pixel("objects/key", new Color32(0xff, 0xd2, 0x3f, 0xff), Shape.Small);
                case MapObjectTypes.Door: return Pixel("objects/door", new Color32(0xc0, 0x45, 0x55, 0xff), Shape.Bar);
                case MapObjectTypes.Npc: return Pixel("objects/npc", new Color32(0xb0, 0x7c, 0xf0, 0xff), Shape.Diamond);
                case MapObjectTypes.Checkpoint: return Pixel("objects/checkpoint", new Color32(0x2c, 0xd5, 0xe8, 0xff), Shape.Small);
                case MapObjectTypes.Hidden: return Pixel("objects/hidden", new Color32(0xff, 0x5f, 0xb0, 0xff), Shape.Small);
                case MapObjectTypes.Anomaly: return Pixel("objects/anomaly", new Color32(0xe0, 0x36, 0x2c, 0xff), Shape.Diamond);
                // 가짜 벽은 드러난 뒤에도 벽 그림을 쓴다 (따로 그림 없음)
                case MapObjectTypes.FakeWall: return Wall;
                case MapObjectTypes.Computer: return Pixel("objects/computer", new Color32(0xc2, 0x36, 0x5a, 0xff), Shape.Frame);
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

        /// <summary>
        /// 도감 그림. 도감의 image 값(예: "/guide/npe.png", "npe")에서 파일 이름만 떼어 Resources/Guidebook/{이름}을 찾는다.
        /// 없으면 null (화면은 "이미지 없음" 상자)
        /// </summary>
        public static Sprite Guidebook(string image)
        {
            if (string.IsNullOrWhiteSpace(image)) return null;
            string name = image.Trim().Replace('\\', '/');
            name = name.Substring(name.LastIndexOf('/') + 1);
            int dot = name.LastIndexOf('.');
            if (dot > 0) name = name.Substring(0, dot);
            return name.Length == 0 ? null : Load<Sprite>("Guidebook/" + name);
        }

        private enum Shape { Solid, Frame, Cross, Crack, Small, Bar, Diamond }

        private static Sprite Pixel(string path, Color32 color, Shape shape)
        {
            return CellSprite("Pixel/" + path) ?? Placeholder(path, color, shape);
        }

        /// <summary>
        /// 칸에 놓는 그림 (타일·오브젝트). 크기(16x16, 64x64, 749px ...)와 상관없이 **너비 = 한 칸**으로 맞춘다.
        /// 세로로 긴 그림(컴퓨터 등)은 아래쪽이 칸 바닥에 닿고 위로 솟아 윗칸까지 올라간다 (입체감).
        /// 정사각형·가로로 긴 그림은 칸 가운데. 가져오기 설정(Pixels Per Unit, Sprite 여부)과 상관없이 텍스처로 읽어 만든다. 없으면 null
        /// </summary>
        private static Sprite CellSprite(string path)
        {
            if (Cache.TryGetValue(path, out Sprite cached)) return cached;
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;
            // 작은 도트만 Point (큰 그림은 가져오기 설정의 Bilinear로 부드럽게 줄인다, PixelArtImporter)
            if (Mathf.Max(texture.width, texture.height) <= 256) texture.filterMode = FilterMode.Point;
            // 세로로 긴 그림: 칸 가운데(피벗)가 그림 아래에서 반 칸(= 너비의 절반) 위 -> 그림 아래 = 칸 바닥
            float pivotY = texture.height > texture.width ? texture.width / 2f / texture.height : 0.5f;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, pivotY),
                texture.width);
            Cache[path] = sprite;
            return sprite;
        }

        /// <summary>
        /// 화면 일러스트 (Resources/Illustrations/{이름}). 없으면 null (임시 상자를 그대로 둔다).
        /// 가져오기 설정의 Sprite Mode가 Multiple(자동으로 조각냄)이어도 조각 하나가 아니라 이미지 전체를 쓰도록
        /// 텍스처를 읽어 전체 크기 스프라이트를 만든다
        /// </summary>
        public static Sprite Illustration(string name)
        {
            string path = $"Illustrations/{name}";
            if (Cache.TryGetValue(path, out Sprite cached)) return cached;
            var texture = Resources.Load<Texture2D>(path);
            Sprite sprite = texture == null ? null
                : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100);
            if (sprite != null) Cache[path] = sprite;
            return sprite;
        }

        /// <summary>
        /// UI 그림 (Resources/UI/{이름}, 피그마에서 2배로 내보낸 것). 크기는 코드가 정하므로 Pixels Per Unit은 상관없다. 없으면 null
        /// </summary>
        public static Sprite Ui(string name)
        {
            string path = $"UI/{name}";
            if (Cache.TryGetValue(path, out Sprite cached)) return cached;
            var texture = Resources.Load<Texture2D>(path);
            Sprite sprite = texture == null ? null
                : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100);
            if (sprite != null) Cache[path] = sprite;
            return sprite;
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
