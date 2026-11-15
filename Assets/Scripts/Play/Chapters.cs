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
        public int No;
        public string Title;
        /// <summary>턴 제한</summary>
        public int Par;
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
        public List<StageInfo> Stages = new List<StageInfo>();

        /// <summary>스테이지가 하나라도 있는지 (없으면 "준비 중")</summary>
        public bool IsAvailable => Stages.Count > 0;

        /// <summary>챕터 스토리 = 첫 스테이지의 도입 나레이션 (닉네임 입력 단계 제외)</summary>
        public List<StoryStep> Story => Stages.FirstOrDefault()?.Map.Story?.Intro?
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
                };
                foreach (TextAsset file in Resources.LoadAll<TextAsset>(c.Value<string>("maps")))
                {
                    var json = JObject.Parse(file.text);
                    chapter.Stages.Add(new StageInfo
                    {
                        No = json.Value<int>("stageNo"),
                        Title = json.Value<string>("title"),
                        Par = json.Value<int>("parMoves"),
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
    /// 스테이지 진행 기록 (계정·챕터별, PC 저장). 서버에 공식 스테이지가 등록되면 서버 기록(GET /operations)으로 바꾼다.
    /// 키: executor.{계정}.{챕터ID}.best.{스테이지 번호} (튜토리얼은 이전 버전과 같은 키)
    /// </summary>
    public static class StageProgress
    {
        private static string Key(string chapterId, int stageNo) =>
            $"executor.{PlayerSession.ProgressPrefix}.{chapterId}.best.{stageNo}";

        /// <summary>클리어한 판 중 가장 적게 쓴 턴. 기록이 없으면 0</summary>
        public static int BestMoves(string chapterId, int stageNo) => PlayerPrefs.GetInt(Key(chapterId, stageNo), 0);

        public static bool IsCleared(string chapterId, int stageNo) => BestMoves(chapterId, stageNo) > 0;

        /// <returns>최고 기록을 갱신했으면 true</returns>
        public static bool RecordClear(string chapterId, int stageNo, int moves)
        {
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
            int index = chapters.ToList().IndexOf(chapter);
            return index <= 0 || IsChapterCleared(chapters[index - 1]);
        }

        /// <summary>챕터 안에서: 첫 스테이지는 챕터가 열리면, 나머지는 앞 스테이지를 클리어하면 열린다</summary>
        public static bool IsStageUnlocked(ChapterInfo chapter, StageInfo stage)
        {
            int index = chapter.Stages.IndexOf(stage);
            return index <= 0 || IsCleared(chapter.Id, chapter.Stages[index - 1].No);
        }
    }
}
