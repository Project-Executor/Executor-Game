using System.Collections.Generic;
using System.Linq;
using Executor.Data;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Executor.Play
{
    /// <summary>스테이지 하나 (맵 JSON = 관리자 등록 형식에서 operationId만 빠진 것)</summary>
    public class StageInfo
    {
        /// <summary>서버 스테이지 ID. 로그인해서 서버에서 받은 스테이지만 있다 (오프라인 Resources 맵은 null)</summary>
        public long? StageId;
        public int No;
        public string Title;
        /// <summary>턴 제한 (시작 구간의 턴)</summary>
        public int Par;
        /// <summary>튜토리얼이면 별은 클리어(★1)만 있다</summary>
        public bool Tutorial;
        /// <summary>첫 클리어 보상 코인 (서버 스테이지만)</summary>
        public int RewardCoin;
        /// <summary>맵 데이터. 서버 스테이지는 잠겨 있는 동안 null이고 시작할 때 받는다 (ServerCatalog.LoadMapAsync)</summary>
        public MapData Map;
    }

    /// <summary>
    /// 챕터 하나. 서버의 작전(Operation)에 해당한다.
    /// 지금은 Resources/Maps/chapters.json + Resources/Maps/{챕터ID}/stage-N.json에서 읽는다.
    /// 서버에 공식 스테이지가 등록되면 GET /operations 응답으로 같은 구조를 채우면 된다.
    /// </summary>
    public class ChapterInfo
    {
        public string Id;
        public string Title;
        public string Subtitle;
        /// <summary>소속 레이어 (스토리 대주제). 시나리오 트리에서 이 값으로 묶는다</summary>
        public string LayerId = "";
        public string LayerName = "";
        public string LayerDescription = "";
        public int LayerOrder;

        /// <summary>"Layer 0 · Tutorial" (이름이 이미 "Layer N"이면 그대로)</summary>
        public string LayerLabel => LayerName == $"Layer {LayerOrder}" || string.IsNullOrEmpty(LayerName)
            ? $"Layer {LayerOrder}"
            : $"Layer {LayerOrder} · {LayerName}";
        public List<StageInfo> Stages = new List<StageInfo>();

        /// <summary>스테이지가 하나라도 있는지 (없으면 "준비 중")</summary>
        public bool IsAvailable => Stages.Count > 0;

        /// <summary>챕터 스토리 = 첫 스테이지의 도입 나레이션 (닉네임 입력 단계 제외)</summary>
        public List<StoryStep> Story => Stages.FirstOrDefault()?.Map?.Story?.Intro?
            .Where(s => !StoryTypes.IsNicknameInput(s.Type)).ToList() ?? new List<StoryStep>();
    }

    public static class StoryTypes
    {
        /// <summary>닉네임 입력 단계인지 (예전 맵의 inputCharacterName도 같은 단계로 본다)</summary>
        public static bool IsNicknameInput(string type) => type == "inputNickname" || type == "inputCharacterName";
    }

    public static class ChapterCatalog
    {
        /// <summary>
        /// 챕터 목록. chapters.json 순서가 곧 진행 순서다.
        /// 챕터를 추가하려면 chapters.json에 항목을 넣고 Maps/{챕터ID}/ 폴더에 stage-N.json을 넣는다.
        /// </summary>
        public static List<ChapterInfo> Load()
        {
            var chapters = new List<ChapterInfo>();
            var manifest = Resources.Load<TextAsset>("Maps/chapters");
            if (manifest == null)
            {
                Debug.LogError("[ChapterCatalog] Resources/Maps/chapters.json이 없습니다.");
                return chapters;
            }
            foreach (JObject c in JObject.Parse(manifest.text)["chapters"])
            {
                var chapter = new ChapterInfo
                {
                    Id = c.Value<string>("id"),
                    Title = c.Value<string>("title"),
                    Subtitle = c.Value<string>("subtitle"),
                    LayerId = c["layer"]?.Value<string>("id") ?? "",
                    LayerName = c["layer"]?.Value<string>("name") ?? "",
                    LayerDescription = c["layer"]?.Value<string>("description") ?? "",
                    LayerOrder = c["layer"]?.Value<int>("order") ?? 0,
                };
                foreach (TextAsset file in Resources.LoadAll<TextAsset>(c.Value<string>("maps")))
                {
                    var json = JObject.Parse(file.text);
                    chapter.Stages.Add(new StageInfo
                    {
                        No = json.Value<int>("stageNo"),
                        Title = json.Value<string>("title"),
                        Par = json.Value<int>("parMoves"),
                        Tutorial = json.Value<string>("stageType") == "TUTORIAL",
                        Map = MapData.From((JObject)json["mapData"]),
                    });
                }
                chapter.Stages.Sort((a, b) => a.No.CompareTo(b.No));
                chapters.Add(chapter);
            }
            return chapters;
        }
    }

    /// <summary>
    /// 스테이지 진행 기록. 로그인해서 서버에서 받은 스테이지는 서버 기록(GET /operations, SetRemote)을,
    /// 오프라인(Resources 맵)은 PC 저장 기록을 쓴다.
    /// PC 저장 키: executor.{계정}.{챕터ID}.best.{스테이지 번호} (튜토리얼은 이전 버전과 같은 키)
    /// </summary>
    public static class StageProgress
    {
        /// <summary>서버 기록 (해금·클리어·최고 기록·별)</summary>
        public class RemoteRecord
        {
            public bool Unlocked;
            public bool Cleared;
            /// <summary>최고 기록. 미클리어면 0</summary>
            public int Best;
            public Stars Stars;
        }

        private static readonly Dictionary<string, RemoteRecord> Remote = new Dictionary<string, RemoteRecord>();

        public static void ClearRemote() => Remote.Clear();

        public static void SetRemote(string chapterId, int stageNo, RemoteRecord record) => Remote[chapterId + "/" + stageNo] = record;

        /// <summary>서버 기록이 있으면 그것 (로그인 중일 때만)</summary>
        public static RemoteRecord RemoteOf(string chapterId, int stageNo) =>
            PlayerSession.IsOnline && Remote.TryGetValue(chapterId + "/" + stageNo, out RemoteRecord r) ? r : null;

        private static string Key(string chapterId, int stageNo) =>
            $"executor.{PlayerSession.ProgressPrefix}.{chapterId}.best.{stageNo}";

        /// <summary>클리어한 판 중 가장 적게 쓴 턴(기록). 기록이 없으면 0</summary>
        public static int BestMoves(string chapterId, int stageNo) =>
            RemoteOf(chapterId, stageNo)?.Best ?? PlayerPrefs.GetInt(Key(chapterId, stageNo), 0);

        public static bool IsCleared(string chapterId, int stageNo) =>
            RemoteOf(chapterId, stageNo)?.Cleared ?? BestMoves(chapterId, stageNo) > 0;

        /// <returns>최고 기록을 갱신했으면 true</returns>
        public static bool RecordClear(string chapterId, int stageNo, int moves)
        {
            RemoteRecord remote = RemoteOf(chapterId, stageNo);
            if (remote != null)
            {
                // 서버 스테이지: 서버 응답을 받기 전에 화면을 맞춰 둔다 (정확한 값은 응답·다시 불러오기로 덮어쓴다)
                bool better = !remote.Cleared || moves < remote.Best;
                remote.Cleared = true;
                if (better) remote.Best = moves;
                return better;
            }
            int best = BestMoves(chapterId, stageNo);
            if (best != 0 && best <= moves) return false;
            PlayerPrefs.SetInt(Key(chapterId, stageNo), moves);
            PlayerPrefs.Save();
            return true;
        }

        public static int ClearedCount(ChapterInfo chapter) => chapter.Stages.Count(s => IsCleared(chapter.Id, s.No));

        public static bool IsChapterCleared(ChapterInfo chapter) =>
            chapter.IsAvailable && ClearedCount(chapter) == chapter.Stages.Count;

        /// <summary>첫 챕터는 처음부터, 나머지는 앞 챕터를 모두 클리어하면 열린다</summary>
        public static bool IsChapterUnlocked(IReadOnlyList<ChapterInfo> chapters, ChapterInfo chapter)
        {
            // 서버 스테이지: 첫 스테이지가 열렸으면 챕터도 열린 것 (서버가 앞 작전 클리어로 판단)
            RemoteRecord first = chapter.Stages.Count > 0 ? RemoteOf(chapter.Id, chapter.Stages[0].No) : null;
            if (first != null) return first.Unlocked;
            int index = chapters.ToList().IndexOf(chapter);
            return index <= 0 || IsChapterCleared(chapters[index - 1]);
        }

        /// <summary>챕터 안에서: 첫 스테이지는 챕터가 열리면, 나머지는 앞 스테이지를 클리어하면 열린다</summary>
        public static bool IsStageUnlocked(ChapterInfo chapter, StageInfo stage)
        {
            RemoteRecord remote = RemoteOf(chapter.Id, stage.No);
            if (remote != null) return remote.Unlocked;
            int index = chapter.Stages.IndexOf(stage);
            return index <= 0 || IsCleared(chapter.Id, chapter.Stages[index - 1].No);
        }
    }
}
