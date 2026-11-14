// 게임 규칙(Assets/Scripts/Game) 점검. Unity 없이 실행한다: Tools/RulesCheck/run.sh <튜토리얼 맵 폴더>
// 1) 튜토리얼 맵을 서버 저장소의 생성 스크립트가 찾은 최소 풀이로 리플레이해 클리어·턴 수(parMoves)를 확인
// 2) 이동·대화·안내 규칙 단위 점검 (docs/map-data.md 3~5장)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Executor.Data;
using Executor.Game;
using Newtonsoft.Json.Linq;

public static class Program
{
    private static int _pass, _fail;

    private static void Check(string name, bool ok, string detail = "")
    {
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name} {detail}");
    }

    // docs/maps/build_tutorial.py가 출력한 최소 풀이 (T = 대화)
    private static readonly Dictionary<int, string> Solutions = new Dictionary<int, string>
    {
        [1] = "DDSSAASSDDDDWWWWDDSSSS",
        [2] = "SSSSDDDDDDDD",
        [3] = "SSDDDAAAWWDDDDDD",
        [4] = "SDTDDDDDD",
        [5] = "SSSSDDSSDDWWDDSSDDWW",
        [6] = "DDDDSSDDDDDDDDDSSATSAAWAAAAAAA",
    };

    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        string dir = args.Length > 0 ? args[0] : "../Executor-Server/docs/maps/tutorial";

        Console.WriteLine("== 튜토리얼 리플레이");
        for (int no = 1; no <= 6; no++)
        {
            var stage = JObject.Parse(File.ReadAllText(Path.Combine(dir, $"stage-{no}.json")));
            var map = MapData.From((JObject)stage["mapData"]);
            int par = stage.Value<int>("parMoves");
            var board = new Board(map);
            var guides = new GuideTracker(board, "오집행");
            int guideCount = guides.OnStart().Count;
            int talks = 0;
            foreach (char c in Solutions[no])
            {
                if (c == 'T')
                {
                    var session = new DialogueSession(board, board.TalkableNpc, "오집행");
                    while (!session.IsFinished)
                    {
                        if (session.HasChoices) session.Choose(0); else session.Advance();
                    }
                    talks++;
                    continue;
                }
                DirectionExtensions.TryFromKey(c, out Direction d);
                var r = board.Move(d);
                if (!r.UsedTurn) { Check($"stage {no}: 풀이 중 막힘", false, $"{c} at {board.PlayerPos}"); break; }
                guideCount += guides.AfterMove(r).Count;
            }
            Check($"stage {no} {stage.Value<string>("title")}", board.IsCleared && board.Turns == par,
                $"턴 {board.Turns}/par {par}, 대화 {talks}, 안내 {guideCount}/{map.Guides.Count}");
            var result = board.ToResultRequest();
            Check($"stage {no} 결과 제출 요청", result.Cleared && result.Moves == par);
        }

        Console.WriteLine("== 이동 규칙");
        RuleChecks();

        Console.WriteLine($"결과: PASS {_pass} / FAIL {_fail}");
        return _fail == 0 ? 0 : 1;
    }

    private static MapData Map(string[] grid, params MapObjectData[] objects) => new MapData
    {
        Version = 1, Width = grid[0].Length, Height = grid.Length,
        Tiles = grid.Select(r => new string(r.Select(ch => ch == '#' ? '#' : '.').ToArray())).ToList(),
        Player = new GridPoint { X = 1, Y = 1 },
        Objects = objects.ToList(),
    };

    private static MapObjectData Obj(string id, string type, int x, int y, int? hp = null, string keyId = null, string dialogueId = null) =>
        new MapObjectData { Id = id, Type = type, X = x, Y = y, Hp = hp, KeyId = keyId, DialogueId = dialogueId, Name = "연구원" };

    private static void RuleChecks()
    {
        // 벽
        var b = new Board(Map(new[] { "####", "#..#", "####" }));
        var r = b.Move(Direction.Up);
        Check("벽 -> 막힘, 턴 0", r.Outcome == MoveOutcome.Blocked && b.Turns == 0 && b.PlayerPos == new GridPos(1, 1));
        r = b.Move(Direction.Right);
        Check("바닥 -> 이동, 턴 1", r.Outcome == MoveOutcome.Moved && b.Turns == 1 && b.PlayerPos == new GridPos(2, 1));

        // 상자: 밀리면 플레이어 제자리, 막히면 턴 0
        b = new Board(Map(new[] { "#####", "#...#", "#####" }, Obj("c", MapObjectTypes.Crate, 2, 1)));
        r = b.Move(Direction.Right);
        Check("상자 밀기 -> 상자 이동, 플레이어 제자리, 턴 1",
            r.Outcome == MoveOutcome.Pushed && b.ObjectAt(new GridPos(3, 1))?.Id == "c" && b.PlayerPos == new GridPos(1, 1) && b.Turns == 1);
        b.Move(Direction.Right);
        r = b.Move(Direction.Right);
        Check("벽 앞 상자 -> 막힘, 턴 그대로", r.Outcome == MoveOutcome.Blocked && b.Turns == 2);

        b = new Board(Map(new[] { "######", "#....#", "######" },
            Obj("c", MapObjectTypes.Crate, 2, 1), Obj("k", MapObjectTypes.Key, 3, 1, keyId: "lab")));
        Check("상자 너머에 열쇠 -> 막힘", b.Move(Direction.Right).Outcome == MoveOutcome.Blocked && b.Turns == 0);

        // 블록
        b = new Board(Map(new[] { "#####", "#...#", "#####" }, Obj("b", MapObjectTypes.Breakable, 2, 1, hp: 2)));
        var r1 = b.Move(Direction.Right);
        var r2 = b.Move(Direction.Right);
        Check("블록 hp 2 -> 두 번째에 부서짐, 플레이어 제자리",
            r1.Outcome == MoveOutcome.Damaged && r2.Outcome == MoveOutcome.Broke && b.ObjectAt(new GridPos(2, 1)) == null
            && b.PlayerPos == new GridPos(1, 1) && b.Turns == 2);
        Check("부서진 자리로 이동", b.Move(Direction.Right).Outcome == MoveOutcome.Moved && b.PlayerPos == new GridPos(2, 1));

        // 열쇠·문
        b = new Board(Map(new[] { "######", "#....#", "######" },
            Obj("d", MapObjectTypes.Door, 2, 1, keyId: "lab"), Obj("k", MapObjectTypes.Key, 3, 1, keyId: "lab")));
        r = b.Move(Direction.Right);
        Check("열쇠 없이 문 -> 막힘, 턴 0", r.Outcome == MoveOutcome.Blocked && b.Turns == 0);
        b = new Board(new MapData
        {
            Width = 6, Height = 3, Tiles = new List<string> { "######", "#....#", "######" },
            Player = new GridPoint { X = 1, Y = 1 },
            Objects = new List<MapObjectData> { Obj("k", MapObjectTypes.Key, 2, 1, keyId: "lab"), Obj("d", MapObjectTypes.Door, 3, 1, keyId: "lab") },
        });
        r1 = b.Move(Direction.Right);
        Check("열쇠 줍기", r1.Outcome == MoveOutcome.PickedUpKey && b.KeyCount("lab") == 1);
        r2 = b.Move(Direction.Right);
        Check("문 열기 -> 그 칸으로 이동, 열쇠 소모", r2.Outcome == MoveOutcome.OpenedDoor && b.PlayerPos == new GridPos(3, 1)
                                                && b.KeyCount("lab") == 0 && b.Turns == 2);

        // 출구
        b = new Board(Map(new[] { "####", "#..#", "####" }, Obj("e", MapObjectTypes.Exit, 2, 1)));
        r = b.Move(Direction.Right);
        Check("출구 -> 클리어", r.Outcome == MoveOutcome.Cleared && b.IsCleared && b.Turns == 1);
        Check("클리어 후 입력 무시", b.Move(Direction.Left).Outcome == MoveOutcome.Blocked && b.Turns == 1);
        Check("클리어 전 결과 요청은 실패", !new Board(Map(new[] { "###", "#.#", "###" })).ToResultRequest().Cleared);

        // 인물·대화
        var map = Map(new[] { "#######", "#.....#", "#.....#", "#######" }, Obj("n", MapObjectTypes.Npc, 4, 2, dialogueId: "talk"));
        map.Dialogues["talk"] = new DialogueData
        {
            Start = "a",
            Nodes = new Dictionary<string, DialogueNode>
            {
                ["a"] = new DialogueNode { Text = "{characterName}님", Choices = new List<DialogueChoice>
                    { new DialogueChoice { Text = "1", Next = "b" }, new DialogueChoice { Text = "2", Next = "c" } } },
                ["b"] = new DialogueNode { Text = "열쇠", Effects = new List<DialogueEffect> { new DialogueEffect { Type = "giveKey", KeyId = "lab" } } },
                ["c"] = new DialogueNode { Text = "끝" },
            }
        };
        b = new Board(map);
        Check("3x3 밖 -> 대화 불가", b.TalkableNpc == null);
        b.Move(Direction.Right); b.Move(Direction.Right);
        Check("3x3 안 (대각선) -> 대화 가능", b.TalkableNpc?.Id == "n");
        var s = new DialogueSession(b, b.TalkableNpc, "오집행");
        Check("이름 치환, 화자 기본값 = 인물 이름", s.Text == "오집행님" && s.Speaker == "연구원" && s.HasChoices);
        s.Choose(0);
        Check("선택지 -> giveKey", b.KeyCount("lab") == 1 && s.GivenKeys.Count == 1);
        s.Advance();
        Check("마지막 노드 다음 -> 대화 끝", s.IsFinished);
        var s2 = new DialogueSession(b, b.TalkableNpc, "");
        s2.Choose(0);
        Check("같은 인물 두 번째 대화 -> 열쇠 다시 안 줌, 빈 이름은 '집행자'", b.KeyCount("lab") == 1 && s2.GivenKeys.Count == 0);
        Check("대화는 턴을 쓰지 않음", b.Turns == 2);
        r = b.Move(Direction.Down); // (3,1) -> (3,2), 인물은 (4,2)
        Check("인물 옆으로 이동", r.Outcome == MoveOutcome.Moved);
        Check("인물 칸 -> 막힘", b.Move(Direction.Right).Outcome == MoveOutcome.Blocked);

        // 안내
        map = Map(new[] { "######", "#....#", "######" }, Obj("k", MapObjectTypes.Key, 4, 1, keyId: "lab"));
        map.Guides = new List<GuideData>
        {
            new GuideData { On = "start", Text = "시작" },
            new GuideData { On = "near", Target = "k", Text = "근처" },
            new GuideData { On = "pickup", Target = "k", Text = "{characterName} 획득" },
        };
        b = new Board(map);
        var g = new GuideTracker(b, "오집행");
        Check("start 안내", g.OnStart().SequenceEqual(new[] { "시작" }));
        Check("멀면 near 안 나옴", g.AfterMove(b.Move(Direction.Right)).Count == 0);
        Check("3x3 안 -> near", g.AfterMove(b.Move(Direction.Right)).SequenceEqual(new[] { "근처" }));
        Check("pickup 안내 (이름 치환)", g.AfterMove(b.Move(Direction.Right)).SequenceEqual(new[] { "오집행 획득" }));
        b.Move(Direction.Left);
        Check("같은 안내는 다시 안 나옴", g.AfterMove(b.Move(Direction.Right)).Count == 0);

        // 모르는 type은 지나간다
        b = new Board(Map(new[] { "####", "#..#", "####" }, Obj("x", "bot", 2, 1)));
        Check("모르는 type -> 바닥처럼 이동", b.Move(Direction.Right).Outcome == MoveOutcome.Moved);
    }
}
