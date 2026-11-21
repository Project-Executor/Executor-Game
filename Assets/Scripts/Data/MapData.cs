// 스테이지 map_data (v1). 구조와 규칙: Executor-Server docs/map-data.md
using System.Collections.Generic;
using Executor.Network;
using Newtonsoft.Json.Linq;

namespace Executor.Data
{
    public class MapData
    {
        public int Version { get; set; }
        /// <summary>맵 분위기. 튜토리얼은 hologram</summary>
        public string Theme { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        /// <summary>지형. Height개의 문자열, 각 Width글자. '#' 벽, '.' 바닥. Tiles[y][x]</summary>
        public List<string> Tiles { get; set; } = new List<string>();
        public GridPoint Player { get; set; }
        public List<MapObjectData> Objects { get; set; } = new List<MapObjectData>();
        public Dictionary<string, DialogueData> Dialogues { get; set; } = new Dictionary<string, DialogueData>();
        [Newtonsoft.Json.JsonConverter(typeof(SingleOrArrayConverter<GuideData>))]
        public List<GuideData> Guides { get; set; } = new List<GuideData>();
        public StoryData Story { get; set; } = new StoryData();
        /// <summary>별 조건 설정. 없으면 기본값 (docs/map-data.md 3.4)</summary>
        public StarsData Stars { get; set; } = new StarsData();
        /// <summary>카메라 설정. 없으면 자동 (docs/map-data.md 3.8)</summary>
        public CameraSettings Camera { get; set; }
        /// <summary>미니맵 설정. 없으면 기본값 (docs/map-data.md 3.8)</summary>
        public MinimapSettings Minimap { get; set; }

        /// <summary>스테이지 상세의 mapData(JObject)를 읽는다</summary>
        public static MapData From(JObject mapData) => Json.Convert<MapData>(mapData);
    }

    /// <summary>칸 좌표. x는 왼쪽에서 오른쪽, y는 위에서 아래 (0부터)</summary>
    public class GridPoint
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    /// <summary>맵 오브젝트. Type은 MapObjectTypes 상수 (모르는 type은 무시한다)</summary>
    public class MapObjectData
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        /// <summary>breakable: 부딪혀야 하는 횟수</summary>
        public int? Hp { get; set; }
        /// <summary>key, door: 서로 맞는 열쇠 종류</summary>
        public string KeyId { get; set; }
        /// <summary>npc: 표시 이름</summary>
        public string Name { get; set; }
        /// <summary>npc: Dialogues의 키</summary>
        public string DialogueId { get; set; }
        /// <summary>checkpoint: 구간 번호 (1부터, 플레이 순서)</summary>
        public int? Order { get; set; }
        /// <summary>checkpoint: 여기서 시작하는 구간의 턴</summary>
        public int? Turns { get; set; }
        /// <summary>anomaly: 만나면 해금되는 도감 ID</summary>
        public long? GuidebookId { get; set; }
        /// <summary>computer: 칠 단어 목록 ID (GET /word-lists/{id})</summary>
        public long? WordListId { get; set; }
        /// <summary>computer: 칠 단어 수 (기본 5)</summary>
        public int? WordCount { get; set; }
        /// <summary>computer: 제한 시간 초 (기본 30)</summary>
        public int? TimeLimit { get; set; }
        /// <summary>computer: 복구하면 실행할 장치 동작 (remove, giveKey)</summary>
        public List<DialogueEffect> Effects { get; set; }
    }

    /// <summary>별 조건 (docs/map-data.md 3.4)</summary>
    public class StarsData
    {
        /// <summary>이 스테이지의 별 수 (1~3). 없으면 3. 튜토리얼은 항상 1</summary>
        public int? Count { get; set; }
        /// <summary>별 1 (항상 클리어). Text만 쓴다</summary>
        public StarCondition Star1 { get; set; }
        /// <summary>별 2 조건. 없으면 maxUnstable(예전 형식)로 "불안정 n개 이하"</summary>
        public StarCondition Star2 { get; set; }
        /// <summary>별 3 조건. 없으면 bonus(예전 형식), 그것도 없으면 데이터 파편 모두</summary>
        public StarCondition Star3 { get; set; }

        /// <summary>예전 형식: 별 2 허용 불안정 수. star2가 없을 때만 쓴다</summary>
        public int MaxUnstable { get; set; }
        /// <summary>예전 형식: 별 3 조건. star3이 없을 때만 쓴다</summary>
        public StarBonus Bonus { get; set; }
    }

    /// <summary>
    /// 별 하나의 조건 (docs/map-data.md 3.4). Type: StarTypes 상수.
    /// Value: stable(불안정 허용 수)·moves(기록 턴)·rewinds(워프 횟수)의 기준값. Target·Seconds: hack.
    /// Text: 화면에 보일 문구 (없으면 조건으로 만든 기본 문구)
    /// </summary>
    public class StarCondition
    {
        public string Type { get; set; }
        public int? Value { get; set; }
        public string Target { get; set; }
        public float? Seconds { get; set; }
        public string Text { get; set; }
    }

    /// <summary>예전 형식의 별 3 조건. Type: hidden / hack (Target 컴퓨터를 Seconds초 안에 복구)</summary>
    public class StarBonus
    {
        public string Type { get; set; }
        /// <summary>hack: 컴퓨터 오브젝트 id</summary>
        public string Target { get; set; }
        /// <summary>hack: 이 시간(초) 안에 복구하면 별 3</summary>
        public float? Seconds { get; set; }
    }

    /// <summary>구간 보기에서 화면에 보일 칸 수. 비운 값은 자동 (둘 다 비우면 가장 넓은 구간에 맞춤)</summary>
    public class CameraSettings
    {
        public int? Width { get; set; }
        public int? Height { get; set; }
    }

    public class MinimapSettings
    {
        /// <summary>미니맵이 밝혀지는 범위: 플레이어 둘레 몇 칸 (기본 2 = 5x5)</summary>
        public int? RevealRadius { get; set; }
    }

    public static class MapObjectTypes
    {
        public const string Exit = "exit";
        public const string Crate = "crate";
        public const string Breakable = "breakable";
        public const string Key = "key";
        public const string Door = "door";
        public const string Npc = "npc";
        public const string Checkpoint = "checkpoint";
        /// <summary>데이터 파편. 모두 모으면 별 3</summary>
        public const string Hidden = "hidden";
        /// <summary>이상 개체. 만나면 도감 해금</summary>
        public const string Anomaly = "anomaly";
        /// <summary>가짜 벽. 벽처럼 보이지만 지나갈 수 있고, 처음 들어가면 드러난다</summary>
        public const string FakeWall = "fakeWall";
        /// <summary>감염된 컴퓨터. E로 타자 해킹, 복구하면 장치 동작</summary>
        public const string Computer = "computer";
    }

    public class DialogueData
    {
        public string Start { get; set; }
        public Dictionary<string, DialogueNode> Nodes { get; set; } = new Dictionary<string, DialogueNode>();
    }

    public class DialogueNode
    {
        public string Speaker { get; set; }
        /// <summary>
        /// 스탠딩 일러스트 "인물ID/표정" 또는 "인물ID" (예: "cassia/smile"). 선택 사항.
        /// 없으면 Speaker 이름으로 인물을 찾고 표정은 default (Resources/Portraits/{인물ID}/{표정}.png)
        /// </summary>
        public string Portrait { get; set; }
        /// <summary>{nickname}은 내 닉네임으로 바꿔 보여 준다</summary>
        public string Text { get; set; }
        /// <summary>선택지가 없을 때 다음 노드. null이면 대화 끝</summary>
        public string Next { get; set; }
        public List<DialogueChoice> Choices { get; set; } = new List<DialogueChoice>();
        /// <summary>이 노드를 보여 줄 때 실행</summary>
        public List<DialogueEffect> Effects { get; set; } = new List<DialogueEffect>();
    }

    public class DialogueChoice
    {
        public string Text { get; set; }
        public string Next { get; set; }
    }

    public class DialogueEffect
    {
        /// <summary>giveKey (대화·컴퓨터), remove (컴퓨터)</summary>
        public string Type { get; set; }
        public string KeyId { get; set; }
        /// <summary>remove: 없앨 오브젝트 id</summary>
        public string Target { get; set; }
    }

    /// <summary>안내 문구. On: start / near / pickup</summary>
    public class GuideData
    {
        public string On { get; set; }
        /// <summary>near: 오브젝트 id, pickup: 열쇠 id</summary>
        public string Target { get; set; }
        public string Text { get; set; }
    }

    public class StoryData
    {
        /// <summary>플레이 전</summary>
        [Newtonsoft.Json.JsonConverter(typeof(SingleOrArrayConverter<StoryStep>))]
        public List<StoryStep> Intro { get; set; } = new List<StoryStep>();
        /// <summary>클리어 후 결과 화면 전</summary>
        [Newtonsoft.Json.JsonConverter(typeof(SingleOrArrayConverter<StoryStep>))]
        public List<StoryStep> Outro { get; set; } = new List<StoryStep>();
    }

    /// <summary>
    /// Type: narration (나레이션 한 줄) / line (인물 대사: 대화창에 이름표·일러스트와 함께) /
    /// inputNickname (닉네임 입력 + 중복 확인, 이미 있으면 건너뜀) /
    /// dialogue (맵 dialogues의 대화 하나를 선택지까지 진행하고, 끝나면 다음 단계)
    /// </summary>
    public class StoryStep
    {
        public string Type { get; set; }
        public string Text { get; set; }
        /// <summary>line: 이름표 ({nickname} 치환). 대화 노드의 speaker와 같다</summary>
        public string Speaker { get; set; }
        /// <summary>line: 스탠딩 일러스트 "인물ID/표정" (선택). 대화 노드의 portrait와 같다</summary>
        public string Portrait { get; set; }
        /// <summary>narration: 배경 "error"(오류 로그, 기본) 또는 "glitch"(일그러진 델타 창)</summary>
        public string Background { get; set; }
        /// <summary>dialogue: 맵 dialogues의 키. 없는 대화면 이 단계를 건너뛴다</summary>
        public string DialogueId { get; set; }
    }
}
