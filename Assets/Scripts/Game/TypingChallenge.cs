using System;
using System.Collections.Generic;
using System.Linq;

namespace Executor.Game
{
    /// <summary>
    /// 감염된 컴퓨터 타자 해킹 한 번 (docs/map-data.md 3.9). 단어를 무작위로 뽑아 순서대로 정확히 치게 하고 제한 시간을 잰다.
    /// 화면과 상관없는 순수 C#이라 Unity 없이 점검할 수 있다.
    /// </summary>
    public class TypingChallenge
    {
        public const int DefaultWordCount = 5, MinWordCount = 1, MaxWordCount = 20;
        public const int DefaultTimeLimit = 30, MinTimeLimit = 5, MaxTimeLimit = 300;

        /// <summary>단어 목록을 받지 못했을 때 쓰는 기본 단어</summary>
        public static readonly IReadOnlyList<string> DefaultWords = new[]
        {
            "sudo", "ls", "cd", "pwd", "grep", "ssh", "ping", "kill", "chmod", "exit",
            "git push", "git pull", "git commit", "npm install", "reboot", "patch", "debug", "compile", "deploy", "restore",
        };

        /// <summary>칠 단어 (순서대로)</summary>
        public IReadOnlyList<string> Words { get; }

        /// <summary>지금 칠 단어 번호 (0부터). Words.Count면 다 쳤다</summary>
        public int Index { get; private set; }

        public string Current => Index < Words.Count ? Words[Index] : null;

        public float TimeLimit { get; }

        /// <summary>남은 시간(초). 0이 되면 실패</summary>
        public float TimeLeft { get; private set; }

        /// <summary>모두 쳤다 (복구 성공)</summary>
        public bool IsSucceeded => Index >= Words.Count;

        /// <summary>시간이 다 되었다</summary>
        public bool IsTimedOut => !IsSucceeded && TimeLeft <= 0;

        public bool IsOver => IsSucceeded || IsTimedOut;

        /// <param name="pool">단어 목록. 비어 있으면 기본 단어</param>
        /// <param name="count">칠 단어 수 (1~20로 맞춘다)</param>
        /// <param name="timeLimit">제한 시간 초 (5~300으로 맞춘다)</param>
        public TypingChallenge(IEnumerable<string> pool, int count, int timeLimit, Random random = null)
            : this(pool, Math.Max(MinWordCount, Math.Min(MaxWordCount, count)), Math.Max(MinTimeLimit, Math.Min(MaxTimeLimit, timeLimit)), random, true)
        {
        }

        /// <summary>타자 이벤트용: 단어가 사실상 끝없이 나오고(1000개) 제한 시간 안에 친 단어 수(Index)를 겨룬다. 시간은 10~600초</summary>
        public static TypingChallenge Endless(IEnumerable<string> pool, int timeLimit, Random random = null) =>
            new TypingChallenge(pool, EndlessWordCount, Math.Max(10, Math.Min(600, timeLimit)), random, true);

        public const int EndlessWordCount = 1000;

        private TypingChallenge(IEnumerable<string> pool, int count, int timeLimit, Random random, bool _)
        {
            random ??= new Random();
            List<string> words = (pool ?? Enumerable.Empty<string>())
                .Where(w => !string.IsNullOrWhiteSpace(w)).Select(w => w.Trim()).Distinct().ToList();
            if (words.Count == 0) words = DefaultWords.ToList();

            var picked = new List<string>();
            while (picked.Count < count)
            {
                // 한 바퀴 안에서는 겹치지 않게 섞어서 뽑는다 (목록이 count보다 적으면 다음 바퀴에서 다시)
                foreach (string w in words.OrderBy(_ => random.Next()))
                {
                    if (picked.Count >= count) break;
                    picked.Add(w);
                }
            }
            Words = picked;
            TimeLimit = timeLimit;
            TimeLeft = TimeLimit;
        }

        /// <summary>시간을 흘린다</summary>
        public void Tick(float seconds)
        {
            if (IsOver) return;
            TimeLeft = Math.Max(0, TimeLeft - seconds);
        }

        /// <summary>입력 하나 (앞뒤 공백 무시, 대소문자 구분). 맞으면 다음 단어로</summary>
        /// <returns>맞았으면 true</returns>
        public bool Submit(string input)
        {
            if (IsOver) return false;
            if ((input ?? "").Trim() != Current) return false;
            Index++;
            return true;
        }
    }
}
