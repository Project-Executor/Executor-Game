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
        Check("데이터 파편을 안 모으면 별 3 없음, 이상 개체는 지나가며 만남", !req.HiddenFound && b.HiddenCollected == 0
            && req.EncounteredGuidebookIds.SequenceEqual(new long[] { 1 }), $"hidden {b.HiddenCollected}/{b.HiddenTotal}");

        // 데이터 파편 2개를 모두 모으는 풀이 (구간 1: 6턴, 구간 3: 11턴 = 둘 다 턴 안)
        b = new Board(map, par);
        var hidden1 = b.Objects.First(o => o.Id == "hidden1");
        Check("데이터 파편은 처음엔 안 보임", !hidden1.IsRevealed && b.HiddenTotal == 2);
        Replay(b, "D");
        Check("3x3 안에 들어오면 드러남", hidden1.IsRevealed && !hidden1.IsRemoved);
        Replay(b, "DR");
        Check("들어가면 수집 (1/2)", hidden1.IsRemoved && b.HiddenCollected == 1);
        Replay(b, "RUR");
        Check("구간 1 안정 (6턴)", b.CurrentOrder == 1 && b.StabilityOf(1) == true);
        Replay(b, "RURRDDRRR");
        Replay(b, "RRDRUUDRRRR");
        req = b.ToResultRequest();
        Check("데이터 파편 2/2 + 클리어 -> 별 3, 모두 안정", b.IsCleared && req.HiddenFound && b.HiddenCollected == 2
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

        // 방화벽 (breakable)
        b = new Board(Map(new[] { "#####", "#...#", "#####" }, Obj("b", MapObjectTypes.Breakable, 2, 1, hp: 2)));
        var r1 = b.Move(Direction.Right);
        var r2 = b.Move(Direction.Right);
        Check("방화벽 hp 2 -> 두 번째에 부서짐, 플레이어 제자리",
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
        var s3 = new DialogueSession(map.Dialogues["talk"], "오집행");
        s3.Choose(0);
        Check("이야기 속 대화 (인물 없음): 선택지 진행, 열쇠 효과 무시", s3.Npc == null && s3.Text == "열쇠" && s3.GivenKeys.Count == 0
                                                                && b.KeyCount("lab") == 1);
        s3.Advance();
        Check("이야기 속 대화 끝", s3.IsFinished);
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
        Check("시작으로 워프해도 열쇠 유지, 주운 열쇠는 다시 놓이지 않음", b.KeyCount("a") == 1 && b.ObjectAt(new GridPos(2, 1)) == null
            && b.PlayerPos == new GridPos(1, 1));
        Check("워프 후 같은 길을 가도 열쇠가 늘지 않음", b.Move(Direction.Right).Outcome == MoveOutcome.Moved && b.KeyCount("a") == 1);

        // 열쇠로 연 문도 워프해도 열린 채로 (열쇠를 쓴 뒤 문만 닫히면 진행할 수 없으므로)
        b = new Board(Map(new[] { "#######", "#.....#", "#######" },
            Obj("k", MapObjectTypes.Key, 2, 1, keyId: "a"), Cp("cp1", 3, 1, order: 1, turns: 10), Obj("d", MapObjectTypes.Door, 4, 1, keyId: "a")), 10);
        b.Move(Direction.Right); b.Move(Direction.Right);
        var doorMove = b.Move(Direction.Right);
        var doorA = b.Objects.First(o => o.Id == "d");
        Check("열쇠로 문 열기", doorMove.Outcome == MoveOutcome.OpenedDoor && doorA.IsRemoved && b.KeyCount("a") == 0);
        b.WarpTo(1);
        Check("워프 -> 문은 열린 채, 열쇠는 쓴 그대로", doorA.IsRemoved && b.KeyCount("a") == 0 && b.PlayerPos == new GridPos(3, 1));
        b.WarpTo(0);
        Check("시작으로 워프해도 같음 (열쇠 다시 안 놓임)", doorA.IsRemoved && b.KeyCount("a") == 0 && b.ObjectAt(new GridPos(2, 1)) == null);

        // 데이터 파편과 이상 개체 (docs/map-data.md 3.6)
        b = new Board(Map(new[] { "########", "#......#", "#......#", "########" },
            Cp("cp1", 2, 1, order: 1, turns: 10),
            Obj("h", MapObjectTypes.Hidden, 4, 1),
            new MapObjectData { Id = "npe", Type = MapObjectTypes.Anomaly, X = 6, Y = 2, GuidebookId = 3, Name = "NPE" }), 10);
        var hid = b.Objects.First(o => o.Id == "h");
        Check("시작: 멀리 있는 데이터 파편은 안 보임, 만난 개체 없음", !hid.IsRevealed && b.LastEncounters.Count == 0);
        b.Move(Direction.Right);
        Check("체크포인트 도착", b.CurrentOrder == 1);
        b.Move(Direction.Right);
        Check("3x3 안 -> 드러남", hid.IsRevealed);
        r = b.Move(Direction.Right);
        Check("데이터 파편 칸 -> 수집, 턴 1", r.Outcome == MoveOutcome.Collected && hid.IsRemoved && b.HiddenCollected == 1 && b.AllHiddenFound);
        r = b.Move(Direction.Right);
        Check("이상 개체 3x3 -> 처음 만남 기록", b.LastEncounters.Count == 1 && b.LastEncounters[0].Id == "npe"
            && b.EncounteredGuidebookIds.SequenceEqual(new long[] { 3 }));
        b.Move(Direction.Down);
        Check("이상 개체 칸 -> 막힘, 다시 만남 알림 없음", b.Move(Direction.Right).Outcome == MoveOutcome.Blocked && b.LastEncounters.Count == 0);
        Check("워프해도 수집·만남 유지", b.WarpTo(1) && hid.IsRemoved && b.HiddenCollected == 1 && b.EncounteredGuidebookIds.Count == 1
            && b.PlayerPos == new GridPos(2, 1));
        req = b.ToResultRequest();
        Check("클리어 전 결과: 별 3 아님, 도감 ID는 보냄", !req.Cleared && !req.HiddenFound && req.EncounteredGuidebookIds.SequenceEqual(new long[] { 3 }));
        Check("데이터 파편 없는 맵은 별 3 불가", !new Board(Map(new[] { "###", "#.#", "###" })).AllHiddenFound);

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

        // 미니맵 밝힘 (docs/map-data.md 3.8)
        b = new Board(Map(new[] { "##########", "#........#", "##########" }, Cp("cp1", 2, 1, order: 1, turns: 10)), 10);
        Check("미니맵: 시작 둘레 2칸만 밝혀짐 (기본)", b.RevealRadius == 2 && b.IsExplored(new GridPos(3, 1))
            && !b.IsExplored(new GridPos(4, 1)) && b.IsExplored(new GridPos(0, 0)));
        b.Move(Direction.Right); b.Move(Direction.Right); b.Move(Direction.Right);
        Check("미니맵: 이동하면 더 밝혀짐", b.IsExplored(new GridPos(6, 1)) && !b.IsExplored(new GridPos(7, 1)));
        b.WarpTo(1);
        Check("미니맵: 워프해도 밝힌 곳 유지", b.IsExplored(new GridPos(6, 1)) && b.PlayerPos == new GridPos(2, 1));
        var wide = Map(new[] { "##########", "#........#", "##########" });
        wide.Minimap = new MinimapSettings { RevealRadius = 4 };
        b = new Board(wide);
        Check("미니맵: revealRadius 4", b.RevealRadius == 4 && b.IsExplored(new GridPos(5, 1)) && !b.IsExplored(new GridPos(6, 1)));
        var json = Executor.Network.Json.Deserialize<MapData>("{\"width\":3,\"height\":3,\"camera\":{\"width\":15},\"minimap\":{\"revealRadius\":3}}");
        Check("camera·minimap JSON 읽기", json.Camera?.Width == 15 && json.Camera.Height == null && json.Minimap?.RevealRadius == 3);

        // 대괄호를 빠뜨린 guides·story도 읽는다 (운영 이방인 Stage 2에서 실제로 생긴 형식)
        var loose = MapData.From(Newtonsoft.Json.Linq.JObject.Parse(
            "{\"width\":3,\"height\":3,\"guides\":{\"on\":\"start\",\"text\":\"E로 복구\"},\"story\":{\"intro\":{\"type\":\"narration\",\"text\":\"a\"},\"outro\":null}}"));
        Check("guides가 객체 하나여도 목록으로", loose.Guides.Count == 1 && loose.Guides[0].On == "start" && loose.Guides[0].Text == "E로 복구");
        Check("story.intro가 객체 하나여도, outro가 null이어도", loose.Story.Intro.Count == 1 && loose.Story.Outro != null && loose.Story.Outro.Count == 0);
        var normal = MapData.From(Newtonsoft.Json.Linq.JObject.Parse("{\"guides\":[{\"on\":\"start\",\"text\":\"a\"},{\"on\":\"near\",\"target\":\"x\",\"text\":\"b\"}]}"));
        Check("guides 배열은 그대로", normal.Guides.Count == 2 && normal.Guides[1].Target == "x");

        // 별 3 조건 (docs/map-data.md 3.4): 컴퓨터를 시간 안에 복구. 시작 (1,1), 컴퓨터 (2,2), 체크포인트 (3,1), 출구 (5,1)
        const string bonusMap = "{\"width\":7,\"height\":4,\"tiles\":[\"#######\",\"#.....#\",\"#.....#\",\"#######\"],\"player\":{\"x\":1,\"y\":1},"
            + "\"objects\":[{\"id\":\"pc\",\"type\":\"computer\",\"x\":2,\"y\":2,\"timeLimit\":30},{\"id\":\"cp1\",\"type\":\"checkpoint\",\"x\":3,\"y\":1,\"order\":1,\"turns\":10},"
            + "{\"id\":\"h\",\"type\":\"hidden\",\"x\":1,\"y\":2},{\"id\":\"exit1\",\"type\":\"exit\",\"x\":5,\"y\":1}],"
            + "\"stars\":{\"bonus\":{\"type\":\"hack\",\"target\":\"pc\",\"seconds\":10}}}";
        b = new Board(MapData.From(Newtonsoft.Json.Linq.JObject.Parse(bonusMap)), 10);
        var bpc = b.Objects.First(o => o.Id == "pc");
        Check("별 3 = 컴퓨터 시간: 조건 읽기", b.BonusType == Board.BonusHack && b.BonusComputer == bpc && b.BonusSeconds == 10 && b.BonusAvailable && !b.BonusAchieved);
        b.BeginHack(bpc); b.RecordHackTime(bpc, 12.5f); b.CompleteHack(bpc);
        Check("12.5초 -> 아직", !b.BonusAchieved && b.BestHackSeconds("pc") == 12.5f);
        b.Move(Direction.Right); b.Move(Direction.Right);
        Check("체크포인트 1 도착", b.CurrentOrder == 1);
        b.WarpTo(0);
        Check("시작으로 워프 -> 컴퓨터 다시 감염, 최고 기록은 유지", !bpc.IsRestored && b.BestHackSeconds("pc") == 12.5f);
        b.BeginHack(bpc); b.RecordHackTime(bpc, 9.2f); b.CompleteHack(bpc);
        b.RecordHackTime(bpc, 11f);
        Check("9.2초 -> 달성, 느린 기록은 무시", b.BonusAchieved && b.BestHackSeconds("pc") == 9.2f);
        b.Move(Direction.Right); b.Move(Direction.Right); b.Move(Direction.Right); b.Move(Direction.Right);
        var bonusReq = b.ToResultRequest();
        Check("클리어 결과: 별 3(hiddenFound=true), 데이터 파편은 안 모아도 됨", b.IsCleared && bonusReq.HiddenFound && b.HiddenCollected == 0,
            $"cleared={b.IsCleared}");
        var noBonus = MapData.From(Newtonsoft.Json.Linq.JObject.Parse(bonusMap.Replace("\"target\":\"pc\"", "\"target\":\"nope\"")));
        Check("없는 컴퓨터를 지정하면 별 3 불가", !new Board(noBonus).BonusAvailable);
        Check("bonus가 없으면 데이터 파편 기준 (기본)", new Board(Map(new[] { "###", "#.#", "###" })).BonusType == Board.BonusHidden);

        // 별 설정 (stars.count, star1~3: 조건 종류·문구). 시작 (1,1), 이상 개체 (3,2), 출구 (5,1), 데이터 파편 없음
        const string starMap = "{\"width\":7,\"height\":4,\"tiles\":[\"#######\",\"#.....#\",\"#.....#\",\"#######\"],\"player\":{\"x\":1,\"y\":1},"
            + "\"objects\":[{\"id\":\"npe\",\"type\":\"anomaly\",\"x\":3,\"y\":2,\"guidebookId\":1},{\"id\":\"exit1\",\"type\":\"exit\",\"x\":5,\"y\":1}],"
            + "\"stars\":{\"count\":3,\"star1\":{\"text\":\"탈출!\"},\"star2\":{\"type\":\"moves\",\"value\":4,\"text\":\"4턴 컷\"},\"star3\":{\"type\":\"anomalies\"}}}";
        b = new Board(MapData.From(Newtonsoft.Json.Linq.JObject.Parse(starMap)), 10);
        Check("별 문구: 관리자가 쓴 문구, 없으면 기본 문구", StarTypes.Text(b, 1) == "탈출!" && StarTypes.Text(b, 2) == "4턴 컷"
            && StarTypes.Text(b, 3) == "이상 개체 모두 만나기", $"{StarTypes.Text(b, 1)} / {StarTypes.Text(b, 2)} / {StarTypes.Text(b, 3)}");
        Check("별 2 기록 조건: 클리어 전에는 미달성", !b.IsStarMet(2) && b.IsStarAvailable(2));
        Check("별 3 이상 개체: 아직 못 만남 (0/1)", !b.IsStarMet(3) && StarTypes.Progress(b, 3) == "0/1");
        b.Move(Direction.Right); b.Move(Direction.Right); b.Move(Direction.Right); b.Move(Direction.Right);
        var starReq = b.ToResultRequest();
        Check("4턴 클리어 -> 별 2 달성, 지나가며 이상 개체 만남 -> 별 3 달성", b.IsCleared && b.IsStarMet(2) && b.IsStarMet(3)
            && starReq.ParAchieved && starReq.HiddenFound, $"moves {b.RecordMoves}");
        var slow = MapData.From(Newtonsoft.Json.Linq.JObject.Parse(starMap.Replace("\"value\":4", "\"value\":3")));
        b = new Board(slow, 10);
        b.Move(Direction.Right); b.Move(Direction.Right); b.Move(Direction.Right); b.Move(Direction.Right);
        Check("3턴 기준인데 4턴 -> 별 2 미달성 (parAchieved=false)", b.IsCleared && !b.ToResultRequest().ParAchieved);
        var twoStars = MapData.From(Newtonsoft.Json.Linq.JObject.Parse(starMap.Replace("\"count\":3", "\"count\":2")));
        b = new Board(twoStars, 10);
        Check("별 수 2 -> 별 3은 받을 수 없음", b.StarCount == 2 && !b.IsStarAvailable(3));
        b.Move(Direction.Right); b.Move(Direction.Right); b.Move(Direction.Right); b.Move(Direction.Right);
        Check("별 수 2: 별 3 조건을 채워도 hiddenFound=false", !b.ToResultRequest().HiddenFound && b.ToResultRequest().ParAchieved);
        var rew = MapData.From(Newtonsoft.Json.Linq.JObject.Parse(
            "{\"width\":5,\"height\":3,\"tiles\":[\"#####\",\"#...#\",\"#####\"],\"player\":{\"x\":1,\"y\":1},\"objects\":[{\"id\":\"cp1\",\"type\":\"checkpoint\",\"x\":2,\"y\":1,\"order\":1,\"turns\":5},{\"id\":\"exit1\",\"type\":\"exit\",\"x\":3,\"y\":1}],"
            + "\"stars\":{\"star2\":{\"type\":\"rewinds\",\"value\":0},\"star3\":{\"type\":\"hidden\"}}}"));
        b = new Board(rew, 10);
        Check("워프 0번 조건: 기본 문구, 데이터 파편 없는 맵의 별 3은 불가 (hidden = fragments)", StarTypes.Text(b, 2) == "워프 없이 클리어"
            && !b.IsStarAvailable(3) && StarTypes.Unavailable(b, 3).Contains("데이터 파편"));
        b.Move(Direction.Right); b.WarpTo(1);
        Check("워프하면 별 2 조건 깨짐", !b.IsStarMet(2) && b.Rewinds == 1);
        var legacy = new Board(MapData.From(Newtonsoft.Json.Linq.JObject.Parse("{\"width\":3,\"height\":3,\"tiles\":[\"###\",\"#.#\",\"###\"],\"player\":{\"x\":1,\"y\":1},\"stars\":{\"maxUnstable\":2}}")));
        Check("예전 형식 maxUnstable -> 별 2 = 불안정 2개 이하", StarTypes.Normalize(legacy.StarConditionOf(2).Type) == StarTypes.Stable
            && legacy.StarConditionOf(2).Value == 2 && StarTypes.Text(legacy, 2) == "불안정 2개 이하");

        // 감염된 컴퓨터 (docs/map-data.md 3.9): 시작 (1,1), 체크포인트 (2,1), 컴퓨터 (3,2), 문 (5,1)
        var pcMap = Executor.Network.Json.Deserialize<MapData>(
            "{\"width\":8,\"height\":4,\"tiles\":[\"########\",\"#......#\",\"#......#\",\"########\"],\"player\":{\"x\":1,\"y\":1},"
            + "\"objects\":[{\"id\":\"cp1\",\"type\":\"checkpoint\",\"x\":2,\"y\":1,\"order\":1,\"turns\":10},"
            + "{\"id\":\"pc\",\"type\":\"computer\",\"x\":3,\"y\":2,\"wordListId\":7,\"wordCount\":3,\"timeLimit\":999,"
            + "\"effects\":[{\"type\":\"remove\",\"target\":\"door1\"},{\"type\":\"giveKey\",\"keyId\":\"lab\"},{\"type\":\"remove\",\"target\":\"cp1\"}]},"
            + "{\"id\":\"door1\",\"type\":\"door\",\"x\":5,\"y\":1,\"keyId\":\"none\"}]}");
        b = new Board(pcMap, 10);
        var pc = b.Objects.First(o => o.Id == "pc");
        Check("컴퓨터 JSON 읽기 (timeLimit은 300으로 맞춤)", pc.WordListId == 7 && pc.WordCount == 3 && pc.TimeLimit == 300 && pc.Effects.Count == 3);
        Check("컴퓨터: 3x3 밖이면 해킹 불가", b.Interactable == null && !b.BeginHack(pc) && b.Turns == 0);
        b.Move(Direction.Right);
        Check("컴퓨터: 체크포인트 도착, 3x3 안 -> 상대할 대상", b.CurrentOrder == 1 && b.Interactable?.Id == "pc");
        Check("컴퓨터 칸 -> 막힘", b.Move(Direction.Down).Outcome == MoveOutcome.Moved && b.Move(Direction.Right).Outcome == MoveOutcome.Blocked);
        int before = b.SegmentTurns;
        Check("해킹 시작 -> 1턴", b.BeginHack(pc) && b.SegmentTurns == before + 1);
        Check("실패해도 다시 시작 가능, 또 1턴", b.BeginHack(pc) && b.SegmentTurns == before + 2);
        var applied = b.CompleteHack(pc);
        var door = b.Objects.First(o => o.Id == "door1");
        Check("복구 -> 문 없어짐, 열쇠 받음, 체크포인트는 못 없앰", pc.IsRestored && door.IsRemoved && b.KeyCount("lab") == 1
            && applied.Count == 2 && !b.Objects.First(o => o.Id == "cp1").IsRemoved);
        Check("복구된 컴퓨터는 다시 해킹 불가, 효과도 한 번만", !b.BeginHack(pc) && b.CompleteHack(pc).Count == 0 && b.KeyCount("lab") == 1);
        Check("워프 -> 컴퓨터는 다시 감염, 해킹으로 없앤 문은 돌아옴, 받은 열쇠는 유지", b.WarpTo(1) && !pc.IsRestored && !door.IsRemoved && b.KeyCount("lab") == 1);
        b.Move(Direction.Down);
        b.BeginHack(pc);
        applied = b.CompleteHack(pc);
        Check("다시 해킹 -> 문은 다시 없어지지만 열쇠는 한 번만", pc.IsRestored && door.IsRemoved && b.KeyCount("lab") == 1 && applied.Count == 1);

        // 타자 해킹
        var tc = new TypingChallenge(new[] { "ls", "cd", "pwd", "ls", " " }, 3, 30, new Random(1));
        Check("타자: 단어 3개, 겹치지 않음 (빈 단어·중복 제외)", tc.Words.Count == 3 && tc.Words.Distinct().Count() == 3);
        Check("타자: 틀리면 그대로", !tc.Submit("nope") && tc.Index == 0);
        Check("타자: 맞으면 다음 (앞뒤 공백 무시)", tc.Submit(" " + tc.Current + " ") && tc.Index == 1);
        tc.Submit(tc.Current); tc.Submit(tc.Current);
        Check("타자: 모두 치면 성공", tc.IsSucceeded && tc.IsOver && !tc.IsTimedOut);
        tc = new TypingChallenge(new[] { "a", "b" }, 5, 1);
        Check("타자: 목록보다 많이 뽑으면 겹쳐서 채움, 제한 시간 최소 5초", tc.Words.Count == 5 && tc.TimeLimit == 5);
        tc.Tick(4.9f);
        Check("타자: 시간 남음", !tc.IsOver);
        tc.Tick(0.2f);
        Check("타자: 시간이 다 되면 실패, 입력 무시", tc.IsTimedOut && !tc.Submit(tc.Current));
        Check("타자: 목록이 비면 기본 단어", new TypingChallenge(null, 2, 30).Words.All(w => TypingChallenge.DefaultWords.Contains(w)));

        // 모르는 type은 지나간다
        b = new Board(Map(new[] { "####", "#..#", "####" }, Obj("x", "bot", 2, 1)));
        Check("모르는 type -> 바닥처럼 이동", b.Move(Direction.Right).Outcome == MoveOutcome.Moved);
    }
}
