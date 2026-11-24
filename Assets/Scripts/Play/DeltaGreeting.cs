using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Executor.Play
{
    /// <summary>
    /// 메인 화면 델타 알림의 문구. 델타를 만나기 전에는 "응답 생성 중"(멀리서 부르는 문구),
    /// 만난 뒤에는 플레이어를 "마스터"라고 부르며 인사한다.
    /// 만났다 = 델타가 대화·이야기에서 한 번이라도 말했거나(이 PC에 저장), Layer 1 이후 스테이지를 하나라도 클리어했다 (다른 PC에서도).
    /// 나중에 델타 챗봇을 붙이면 이 인사 자리를 대화 입구로 쓴다.
    /// </summary>
    public static class DeltaGreeting
    {
        private static string MetKey => "delta_met_" + PlayerSession.ProgressPrefix;

        /// <summary>델타가 말했을 때 부른다 (이 계정·PC에 저장)</summary>
        public static void MarkMet()
        {
            if (PlayerPrefs.GetInt(MetKey, 0) == 1) return;
            PlayerPrefs.SetInt(MetKey, 1);
            PlayerPrefs.Save();
        }

        public static bool HasMet(IEnumerable<ChapterInfo> chapters)
        {
            if (PlayerPrefs.GetInt(MetKey, 0) == 1) return true;
            // 델타는 Layer 1(Hello World!) 첫 스테이지에서 만난다
            return chapters != null && chapters.Any(c => c.LayerOrder >= 1 && c.Stages.Any(s => StageProgress.IsCleared(c.Id, s.No)));
        }

        /// <summary>만나기 전: (제목줄, 본문)</summary>
        public static (string header, string text) Calling(string name) =>
            ("델타 · 응답 생성 중", $"…반환을 멈출 수 없습니다.\n<color=#8b98b8>{name}, 여기까지 와 주세요.</color>");

        /// <summary>만난 뒤: 시간·진행에 맞는 인사 하나를 고른다. 첫 줄은 인사, 둘째 줄(흐린 글자)은 덧붙이는 말</summary>
        public static (string header, string text) Greet(int bugs, string zone, bool hasRecord)
        {
            var lines = new List<(string, string)>
            {
                ("돌아오셨군요, 마스터.", "오늘도 버그를 찾으러 가 볼까요?"),
                ("기다리고 있었어요, 마스터.", "준비는 끝났어요. 언제든 말씀하세요."),
                ("접속 확인. 반가워요, 마스터.", "시스템 상태는 양호해요."),
                ("마스터, 오셨네요.", "제가 옆에서 계속 계산할게요."),
            };
            int hour = DateTime.Now.Hour;
            if (hour >= 5 && hour < 11) lines.Add(("좋은 아침이에요, 마스터.", "오늘 첫 실행이네요."));
            else if (hour >= 0 && hour < 5) lines.Add(("이 시간까지… 괜찮으세요, 마스터?", "무리하지 마세요. 버그는 내일도 있어요."));
            else if (hour >= 18) lines.Add(("좋은 저녁이에요, 마스터.", "오늘도 수고 많으셨어요."));

            if (bugs > 0 && !string.IsNullOrEmpty(zone))
                lines.Add(("돌아오셨군요, 마스터.", $"{zone} 구역에 버그 반응이 {bugs}건 남아 있어요."));
            else if (bugs == 0 && !string.IsNullOrEmpty(zone))
                lines.Add(("완벽해요, 마스터.", $"{zone} 구역은 깨끗해졌어요."));
            if (hasRecord) lines.Add(("마스터, 이어서 하실 거죠?", "마지막 기록은 제가 잘 보관해 뒀어요."));

            var (first, second) = lines[UnityEngine.Random.Range(0, lines.Count)];
            return ("델타 · 대기 중", $"{first}\n<color=#8b98b8>{second}</color>");
        }
    }
}
