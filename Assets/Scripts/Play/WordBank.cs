using System.Collections.Generic;
using System.Threading.Tasks;
using Executor.Network;
using UnityEngine;

namespace Executor.Play
{
    /// <summary>
    /// 타자 해킹 단어 목록 캐시. 스테이지를 시작할 때 맵의 컴퓨터가 쓰는 목록을 미리 받아 두고,
    /// 받지 못한 목록(없는 ID, 오프라인, 서버 오류)은 빈 목록으로 돌려줘서 TypingChallenge가 기본 단어를 쓰게 한다.
    /// </summary>
    public static class WordBank
    {
        private static readonly Dictionary<long, List<string>> Cache = new Dictionary<long, List<string>>();
        private static readonly HashSet<long> Loading = new HashSet<long>();

        /// <summary>그 목록의 단어. 아직 못 받았거나 ID가 없으면 빈 목록 (= 기본 단어)</summary>
        public static IReadOnlyList<string> Get(long? wordListId) =>
            wordListId != null && Cache.TryGetValue(wordListId.Value, out List<string> words) ? words : new List<string>();

        /// <summary>목록을 미리 받는다 (이미 받았거나 받는 중이면 건너뜀). 실패는 기본 단어로 넘어가므로 로그만 남긴다</summary>
        public static async void Prefetch(IEnumerable<long> wordListIds)
        {
            var tasks = new List<Task>();
            foreach (long id in wordListIds)
            {
                if (Cache.ContainsKey(id) || !Loading.Add(id)) continue;
                tasks.Add(Load(id));
            }
            await Task.WhenAll(tasks);
        }

        private static async Task Load(long id)
        {
            try
            {
                WordListResponse res = await WordListApi.GetAsync(id);
                Cache[id] = res?.Words ?? new List<string>();
            }
            catch (ApiException e)
            {
                Debug.LogWarning($"[WordBank] 단어 목록 {id}을(를) 받지 못해 기본 단어를 씁니다: {e.Message}");
            }
            finally
            {
                Loading.Remove(id);
            }
        }
    }
}
