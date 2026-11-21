using System.Collections.Generic;
using Executor.Network;
using UnityEngine;

namespace Executor.Play
{
    /// <summary>
    /// 대화 인물 (관리자 웹 '인물', GET /characters). 이름표 색·일러스트 폴더·주인공 자리를 정한다.
    /// 서버에서 받기 전이나 받지 못하면(오프라인·서버 오류) 기본 인물 목록을 쓴다. 목록에 없는 인물은 기본 색
    /// </summary>
    public static class CharacterCatalog
    {
        public static readonly Color DefaultProtagonistColor = Theme.Hex(0xd4d6de);
        public static readonly Color DefaultOtherColor = Theme.Hex(0xff4fa3);

        /// <summary>서버 목록이 없을 때 쓰는 기본값 (서버 마이그레이션의 기본 인물과 같다)</summary>
        private static readonly List<GameCharacter> Defaults = new List<GameCharacter>
        {
            new GameCharacter { CharacterKey = "player", Name = "집행자", TagColor = "#D4D6DE", Protagonist = true },
            new GameCharacter { CharacterKey = "cassia", Name = "카시아", TagColor = "#FF4FA3" },
            new GameCharacter { CharacterKey = "delta", Name = "델타", TagColor = "#FF9A3C" },
            new GameCharacter { CharacterKey = "researcher", Name = "연구원", TagColor = "#C3A6FF" },
            new GameCharacter { CharacterKey = "supervisor", Name = "감독관", TagColor = "#C3A6FF" },
        };

        private static List<GameCharacter> _list = Defaults;
        private static bool _loading;

        /// <summary>서버에서 새로 받는다 (받는 중이면 건너뜀). 실패하면 지금 목록을 그대로 쓴다</summary>
        public static async void Refresh()
        {
            if (_loading) return;
            _loading = true;
            try
            {
                List<GameCharacter> list = await CharacterApi.GetAllAsync();
                if (list != null) _list = list;
            }
            catch (ApiException e)
            {
                Debug.LogWarning($"[CharacterCatalog] 인물 목록을 받지 못해 기본값을 씁니다: {e.Message}");
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>이름(대사의 speaker)으로 찾는다</summary>
        public static GameCharacter ByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (GameCharacter c in _list) if (c.Name == name) return c;
            return null;
        }

        /// <summary>인물 ID(portrait의 앞부분, 일러스트 폴더)로 찾는다. 대소문자 무시</summary>
        public static GameCharacter ByKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (GameCharacter c in _list)
                if (string.Equals(c.CharacterKey, key, System.StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        /// <summary>주인공 인물 (주인공 대사에 portrait가 없을 때 쓸 일러스트). 없으면 null</summary>
        public static GameCharacter Protagonist
        {
            get
            {
                foreach (GameCharacter c in _list) if (c.Protagonist) return c;
                return null;
            }
        }

        /// <summary>이름표 색. 인물이 없거나 색이 잘못되면 기본 색 (주인공 회색, 그 밖에 분홍)</summary>
        public static Color TagColor(GameCharacter c, bool protagonist)
        {
            if (c != null && !string.IsNullOrEmpty(c.TagColor) && ColorUtility.TryParseHtmlString(c.TagColor, out Color color)) return color;
            return protagonist ? DefaultProtagonistColor : DefaultOtherColor;
        }
    }
}
