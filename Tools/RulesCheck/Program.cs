// 게임 규칙(Assets/Scripts/Game) 점검. Unity 없이 실행한다: Tools/RulesCheck/run.sh <튜토리얼 맵 폴더>
// 1) 튜토리얼 맵을 서버 저장소의 생성 스크립트가 찾은 최소 풀이로 리플레이해 클리어·턴 수(parMoves)를 확인
// 2) 이동·턴·체크포인트·워프·대화·안내 규칙 단위 점검 (docs/map-data.md 3~5장)
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
            var board = new Board(map, par);
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
            int min = Solutions[no].Count(ch => ch != 'T');
            Check($"stage {no} {stage.Value<string>("title")}", board.IsCleared && board.Turns == min && board.RemainingTurns == par - min,
                $"최소 {board.Turns}턴, 제한 {par}, 남은 턴 {board.RemainingTurns}, 대화 {talks}, 안내 {guideCount}/{map.Guides.Count}");
            var result = board.ToResultRequest();
            Check($"stage {no} 결과 제출 요청", result.Cleared && result.Moves == min);
        }

        Console.WriteLine("== 체크포인트 연습 맵 (Resources/Maps/chapter1/stage-1.json)");
        PracticeMapCheck("Assets/Resources/Maps/chapter1/stage-1.json");

        Console.WriteLine("== 이동 규칙");
        RuleChecks();

        Console.WriteLine($"결과: PASS {_pass} / FAIL {_fail}");
        return _fail == 0 ? 0 : 1;
    }

    /// <summary>체크포인트 연습 맵: 최소 풀이(구간 4·9·9)와 상자를 잘못 밀었을 때 워프로 되돌리기</summary>
    private static void PracticeMapCheck(string path)
    {
        if (!File.Exists(path)) { Check("연습 맵 파일", false, path); return; }
        var stage = JObject.Parse(File.ReadAllText(path));
        var map = MapData.From((JObject)stage["mapData"]);
        int par = stage.Value<int>("parMoves");

        var b = new Board(map, par);
        Replay(b, "RRDR");
        Check("구간 1: 체크포인트 1 안정 도착 (4턴)", b.CurrentOrder == 1 && b.StabilityOf(1) == true && b.RemainingTurns == 9);
        Replay(b, "RURRDDRRR");
        Check("구간 2: 상자를 홈에 넣고 체크포인트 2 안정 도착 (9턴)", b.CurrentOrder == 2 && b.StabilityOf(2) == true,
            $"남은 턴 {b.RemainingTurns}, 위치 {b.PlayerPos}");
        Replay(b, "RRDRURRRR");
        var req = b.ToResultRequest();
        Check("구간 3: 열쇠·문·출구, 기록 22, 불안정 0", b.IsCleared && req.Moves == 22 && req.UnstableCount == 0 && b.MeetsStableStar,
            $"moves {req.Moves}, unstable {req.UnstableCount}");
        Check("숨겨진 요소를 안 모으면 별 3 없음, 이상 개체는 지나가며 만남", !req.HiddenFound && b.HiddenCollected == 0
            && req.EncounteredGuidebookIds.SequenceEqual(new long[] { 1 }), $"hidden {b.HiddenCollected}/{b.HiddenTotal}");

        // 숨겨진 요소 2개를 모두 모으는 풀이 (구간 1: 6턴, 구간 3: 11턴 = 둘 다 턴 안)
        b = new Board(map, par);
        var hidden1 = b.Objects.First(o => o.Id == "hidden1");
        Check("숨겨진 요소는 처음엔 안 보임", !hidden1.IsRevealed && b.HiddenTotal == 2);
        Replay(b, "D");
        Check("3x3 안에 들어오면 드러남", hidden1.IsRevealed && !hidden1.IsRemoved);
        Replay(b, "DR");
        Check("들어가면 수집 (1/2)", hidden1.IsRemoved && b.HiddenCollected == 1);
        Replay(b, "RUR");
        Check("구간 1 안정 (6턴)", b.CurrentOrder == 1 && b.StabilityOf(1) == true);
        Replay(b, "RURRDDRRR");
        Replay(b, "RRDRUUDRRRR");
        req = b.ToResultRequest();
        Check("숨겨진 요소 2/2 + 클리어 -> 별 3, 모두 안정", b.IsCleared && req.HiddenFound && b.HiddenCollected == 2
            && req.UnstableCount == 0, $"hidden {b.HiddenCollected}/2, unstable {req.UnstableCount}, moves {req.Moves}");

        b = new Board(map, par);
        Replay(b, "RRDR");
        Replay(b, "RRRRRR");
        var crate = b.Objects.First(o => o.Id == "crate1");
        Check("상자를 오른쪽으로 끝까지 밀면 체크포인트 2 앞에서 막힘", crate.Pos == new GridPos(9, 2)
            && b.Move(Direction.Right).Outcome == MoveOutcome.Blocked);
        Check("체크포인트 1로 워프 -> 상자 원래 자리", b.WarpTo(1) && crate.Pos == new GridPos(7, 2) && b.PlayerPos == new GridPos(4, 2));
        Replay(b, "RURRDDRRR");
        Check("워프 후 다시 풀면 안정 도착", b.CurrentOrder == 2 && b.StabilityOf(2) == true && b.Rewinds == 1);
    }

    private static void Replay(Board b, string moves)
    {
        foreach (char c in moves)
        {
            DirectionExtensions.TryFromKey(c == 'R' ? 'D' : c == 'L' ? 'A' : c == 'U' ? 'W' : 'S', out Direction d);
            b.Move(d);
        }
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

    private static MapObjectData Cp(string id, int x, int y, int order, int turns) =>
        new MapObjectData { Id = id, Type = MapObjectTypes.Checkpoint, X = x, Y = y, Order = order, Turns = turns };

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
                ["a"] = new DialogueNode { Text = "{nickname}님", Choices = new List<DialogueChoice>
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
            new GuideData { On = "pickup", Target = "k", Text = "{nickname} 획득" },
        };
        b = new Board(map);
        var g = new GuideTracker(b, "오집행");
        Check("start 안내", g.OnStart().SequenceEqual(new[] { "시작" }));
        Check("멀면 near 안 나옴", g.AfterMove(b.Move(Direction.Right)).Count == 0);
        Check("3x3 안 -> near", g.AfterMove(b.Move(Direction.Right)).SequenceEqual(new[] { "근처" }));
        Check("pickup 안내 (이름 치환)", g.AfterMove(b.Move(Direction.Right)).SequenceEqual(new[] { "오집행 획득" }));
        b.Move(Direction.Left);
        Check("같은 안내는 다시 안 나옴", g.AfterMove(b.Move(Direction.Right)).Count == 0);

        // 턴 제한: 넘겨도 실패하지 않고, 도착이 불안정으로 기록된다 (docs/map-data.md 3.2)
        b = new Board(Map(new[] { "#######", "#.....#", "#######" }, Obj("e", MapObjectTypes.Exit, 5, 1)), 2);
        b.Move(Direction.Right); b.Move(Direction.Up);
        Check("막힌 입력은 남은 턴이 줄지 않음", b.RemainingTurns == 1 && b.Turns == 1);
        b.Move(Direction.Right);
        Check("남은 턴 0 -> 계속", b.RemainingTurns == 0 && !b.IsOverLimit && !b.IsOver);
        b.Move(Direction.Right);
        Check("남은 턴 -1 -> 실패 아님, 초과 표시", b.RemainingTurns == -1 && b.IsOverLimit && !b.IsOver);
        r = b.Move(Direction.Right);
        Check("초과한 채 출구 -> 클리어, 불안정", r.Outcome == MoveOutcome.Cleared && b.IsCleared && !b.LastArrivalStable
            && b.UnstableCount == 1 && b.StabilityOf(Board.ExitOrder) == false);
        var req = b.ToResultRequest();
        Check("결과: 클리어, 기록 4, 불안정 1", req.Cleared && req.Moves == 4 && req.UnstableCount == 1 && req.Rewinds == 0);
        Check("별 2: 허용 0개면 불가", !b.MeetsStableStar);
        var lenient = Map(new[] { "####", "#..#", "####" }, Obj("e", MapObjectTypes.Exit, 2, 1));
        lenient.Stars = new StarsData { MaxUnstable = 1 };
        b = new Board(lenient, 0);
        b.Move(Direction.Right);
        Check("별 2: stars.maxUnstable 1이면 불안정 1개 허용", b.IsCleared && b.UnstableCount == 1 && b.MeetsStableStar);
        b = new Board(Map(new[] { "#####", "#...#", "#####" }, Obj("e", MapObjectTypes.Exit, 3, 1)), 2);
        b.Move(Direction.Right); b.Move(Direction.Right);
        Check("남은 턴 0으로 출구 -> 안정", b.IsCleared && b.LastArrivalStable && b.UnstableCount == 0 && b.MeetsStableStar);
        Check("제한 없는 판은 남은 턴 null", new Board(Map(new[] { "###", "#.#", "###" })).RemainingTurns == null);

        // 체크포인트: 구간, 안정·불안정, 워프, 기록 (docs/map-data.md 3.3)
        b = new Board(Map(new[] { "#########", "#.......#", "#########" },
            Cp("cp1", 3, 1, order: 1, turns: 3), Cp("cp2", 6, 1, order: 2, turns: 5), Obj("e", MapObjectTypes.Exit, 7, 1)), 2);
        b.Move(Direction.Right);
        r = b.Move(Direction.Right);
        Check("체크포인트 도착 (턴 안) -> 안정, 다음 구간 시작", r.Outcome == MoveOutcome.ReachedCheckpoint && b.LastArrivalStable
            && b.CurrentOrder == 1 && b.SegmentTurns == 0 && b.RemainingTurns == 3 && b.WarpPoints.SequenceEqual(new[] { 0, 1 }));
        b.Move(Direction.Left);
        r = b.Move(Direction.Right);
        Check("지나온 체크포인트는 다시 도착하지 않음", r.Outcome == MoveOutcome.Moved && b.CurrentOrder == 1 && b.SegmentTurns == 2);
        b.Move(Direction.Right); b.Move(Direction.Right);
        Check("구간 턴을 넘김 -> 계속 진행", b.RemainingTurns == -1 && b.IsOverLimit);
        r = b.Move(Direction.Right);
        Check("턴을 넘겨 도착 -> 불안정", r.Outcome == MoveOutcome.ReachedCheckpoint && !b.LastArrivalStable
            && b.UnstableCount == 1 && b.StabilityOf(2) == false && b.RemainingTurns == 5);
        Check("워프: 체크포인트 1로", b.WarpTo(1) && b.PlayerPos == new GridPos(3, 1) && b.CurrentOrder == 1
            && b.SegmentTurns == 0 && b.RemainingTurns == 3 && b.Rewinds == 1);
        b.Move(Direction.Right); b.Move(Direction.Right); b.Move(Direction.Right);
        Check("다시 턴 안에 도착 -> 안정으로 바뀜", b.LastArrivalStable && b.StabilityOf(2) == true && b.UnstableCount == 0);
        Check("도착하지 않은 곳으로는 워프 불가", !b.WarpTo(9));
        Check("워프: 시작 위치로", b.WarpTo(0) && b.PlayerPos == new GridPos(1, 1) && b.RemainingTurns == 2);
        Check("워프: 앞쪽 체크포인트로도", b.WarpTo(2) && b.PlayerPos == new GridPos(6, 1) && b.RemainingTurns == 5);
        r = b.Move(Direction.Right);
        Check("출구 -> 클리어", r.Outcome == MoveOutcome.Cleared && b.LastArrivalStable && b.UnstableCount == 0);
        req = b.ToResultRequest();
        Check("기록 = 구간 최고 기록의 합 (2+3+1), 누른 턴은 따로", req.Moves == 6 && b.Turns == 11 && req.UnstableCount == 0 && req.Rewinds == 3,
            $"moves {req.Moves}, turns {b.Turns}, rewinds {req.Rewinds}");
        Check("클리어 후 워프 불가", !b.WarpTo(1));

        // 워프는 그 체크포인트에 마지막으로 도착했을 때의 상태로 되돌린다
        b = new Board(Map(new[] { "#######", "#.....#", "#######" },
            Obj("k", MapObjectTypes.Key, 2, 1, keyId: "a"), Cp("cp1", 3, 1, order: 1, turns: 10), Obj("c", MapObjectTypes.Crate, 4, 1)), 10);
        b.Move(Direction.Right); b.Move(Direction.Right);
        b.Move(Direction.Right);
        var crate = b.Objects.First(o => o.Id == "c");
        Check("상자를 밀었다", crate.Pos == new GridPos(5, 1));
        b.WarpTo(1);
        Check("워프 -> 상자 원래 자리, 열쇠는 도착 때처럼 가짐", crate.Pos == new GridPos(4, 1) && b.KeyCount("a") == 1);
        b.WarpTo(0);
        Check("시작으로 워프 -> 열쇠 없음, 열쇠가 다시 놓임", b.KeyCount("a") == 0 && b.ObjectAt(new GridPos(2, 1))?.Id == "k"
            && b.PlayerPos == new GridPos(1, 1));

        // 숨겨진 요소와 이상 개체 (docs/map-data.md 3.6)
        b = new Board(Map(new[] { "########", "#......#", "#......#", "########" },
            Cp("cp1", 2, 1, order: 1, turns: 10),
            Obj("h", MapObjectTypes.Hidden, 4, 1),
            new MapObjectData { Id = "npe", Type = MapObjectTypes.Anomaly, X = 6, Y = 2, GuidebookId = 3, Name = "NPE" }), 10);
        var hid = b.Objects.First(o => o.Id == "h");
        Check("시작: 멀리 있는 숨겨진 요소는 안 보임, 만난 개체 없음", !hid.IsRevealed && b.LastEncounters.Count == 0);
        b.Move(Direction.Right);
        Check("체크포인트 도착", b.CurrentOrder == 1);
        b.Move(Direction.Right);
        Check("3x3 안 -> 드러남", hid.IsRevealed);
        r = b.Move(Direction.Right);
        Check("숨겨진 요소 칸 -> 수집, 턴 1", r.Outcome == MoveOutcome.Collected && hid.IsRemoved && b.HiddenCollected == 1 && b.AllHiddenFound);
        r = b.Move(Direction.Right);
        Check("이상 개체 3x3 -> 처음 만남 기록", b.LastEncounters.Count == 1 && b.LastEncounters[0].Id == "npe"
            && b.EncounteredGuidebookIds.SequenceEqual(new long[] { 3 }));
        b.Move(Direction.Down);
        Check("이상 개체 칸 -> 막힘, 다시 만남 알림 없음", b.Move(Direction.Right).Outcome == MoveOutcome.Blocked && b.LastEncounters.Count == 0);
        Check("워프해도 수집·만남 유지", b.WarpTo(1) && hid.IsRemoved && b.HiddenCollected == 1 && b.EncounteredGuidebookIds.Count == 1
            && b.PlayerPos == new GridPos(2, 1));
        req = b.ToResultRequest();
        Check("클리어 전 결과: 별 3 아님, 도감 ID는 보냄", !req.Cleared && !req.HiddenFound && req.EncounteredGuidebookIds.SequenceEqual(new long[] { 3 }));
        Check("숨겨진 요소 없는 맵은 별 3 불가", !new Board(Map(new[] { "###", "#.#", "###" })).AllHiddenFound);

        // 가짜 벽 (docs/map-data.md 3.7)
        b = new Board(Map(new[] { "#######", "#.....#", "#######" },
            Obj("c", MapObjectTypes.Crate, 2, 1), Obj("f", MapObjectTypes.FakeWall, 3, 1)));
        var fake = b.Objects.First(o => o.Id == "f");
        Check("가짜 벽: 처음엔 드러나지 않음", !fake.IsRevealed);
        Check("가짜 벽 쪽으로 상자 -> 밀리지 않음, 턴 0", b.Move(Direction.Right).Outcome == MoveOutcome.Blocked && b.Turns == 0);
        b = new Board(Map(new[] { "#######", "#.....#", "#######" },
            Cp("cp1", 2, 1, order: 1, turns: 10), Obj("f", MapObjectTypes.FakeWall, 3, 1)), 10);
        fake = b.Objects.First(o => o.Id == "f");
        b.Move(Direction.Right);
        Check("가짜 벽 바로 옆(3x3 안)이어도 드러나지 않음", !fake.IsRevealed && b.CurrentOrder == 1);
        r = b.Move(Direction.Right);
        Check("가짜 벽 칸 -> 지나감, 드러남, 턴 1", r.Outcome == MoveOutcome.FoundFakeWall && fake.IsRevealed
            && b.PlayerPos == new GridPos(3, 1) && b.SegmentTurns == 1);
        r = b.Move(Direction.Right);
        Check("가짜 벽 너머로 이동", r.Outcome == MoveOutcome.Moved && b.PlayerPos == new GridPos(4, 1));
        Check("워프해도 드러난 채 유지", b.WarpTo(1) && fake.IsRevealed && b.PlayerPos == new GridPos(2, 1));
        r = b.Move(Direction.Right);
        Check("드러난 가짜 벽 -> 그냥 이동", r.Outcome == MoveOutcome.Moved && b.PlayerPos == new GridPos(3, 1));

        // 모르는 type은 지나간다
        b = new Board(Map(new[] { "####", "#..#", "####" }, Obj("x", "bot", 2, 1)));
        Check("모르는 type -> 바닥처럼 이동", b.Move(Direction.Right).Outcome == MoveOutcome.Moved);
    }
}
