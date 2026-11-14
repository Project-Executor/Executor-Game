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
        public List<GuideData> Guides { get; set; } = new List<GuideData>();
        public StoryData Story { get; set; } = new StoryData();

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
    }

    public static class MapObjectTypes
    {
        public const string Exit = "exit";
        public const string Crate = "crate";
        public const string Breakable = "breakable";
        public const string Key = "key";
        public const string Door = "door";
        public const string Npc = "npc";
    }

    public class DialogueData
    {
        public string Start { get; set; }
        public Dictionary<string, DialogueNode> Nodes { get; set; } = new Dictionary<string, DialogueNode>();
    }

    public class DialogueNode
    {
        public string Speaker { get; set; }
        /// <summary>{characterName}은 집행자 이름으로 바꿔 보여 준다</summary>
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
        /// <summary>giveKey</summary>
        public string Type { get; set; }
        public string KeyId { get; set; }
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
        public List<StoryStep> Intro { get; set; } = new List<StoryStep>();
        /// <summary>클리어 후 결과 화면 전</summary>
        public List<StoryStep> Outro { get; set; } = new List<StoryStep>();
    }

    /// <summary>Type: narration (나레이션 한 줄) / inputCharacterName (집행자 이름 입력, 이미 있으면 건너뜀)</summary>
    public class StoryStep
    {
        public string Type { get; set; }
        public string Text { get; set; }
    }
}
