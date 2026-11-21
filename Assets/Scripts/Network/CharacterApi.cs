using System.Collections.Generic;
using System.Threading.Tasks;

namespace Executor.Network
{
    /// <summary>인물 (GET /characters). 대사의 speaker = Name, portrait의 인물 ID = CharacterKey</summary>
    public class GameCharacter
    {
        public long CharacterId { get; set; }
        /// <summary>일러스트 폴더 ID (Resources/Portraits/{CharacterKey})</summary>
        public string CharacterKey { get; set; }
        public string Name { get; set; }
        /// <summary>이름표 바탕색 #RRGGBB</summary>
        public string TagColor { get; set; }
        /// <summary>대화창 왼쪽(주인공 자리)</summary>
        public bool Protagonist { get; set; }
    }

    /// <summary>인물 목록 (/api/characters). 로그인 없이 받을 수 있어서 오프라인 테스트 중에도 서버에 닿으면 불러온다</summary>
    public static class CharacterApi
    {
        public static Task<List<GameCharacter>> GetAllAsync() =>
            ApiClient.GetAsync<List<GameCharacter>>("/characters", auth: false);
    }
}
