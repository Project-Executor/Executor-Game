using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Executor.Data;
using Executor.Network;
using UnityEngine;

namespace Executor.Play
{
    /// <summary>
    /// 로그인했을 때 챕터·스테이지를 서버에서 만든다 (GET /operations = 작전 순서, 스테이지 번호 순).
    /// 작전 하나 = 챕터 하나 (id "op{operationId}"). 해금·클리어·최고 기록은 서버 값을 StageProgress에 넣는다.
    /// 맵 데이터는 스테이지 상세(GET /stages/{id})에서 받는다. 잠긴 스테이지는 서버가 상세를 막으므로(403) 열린 것만 미리 받고,
    /// 나머지는 시작할 때 받는다 (LoadMapAsync).
    /// </summary>
    public static class ServerCatalog
    {
        public static string ChapterId(long operationId) => "op" + operationId;

        /// <exception cref="ApiException">네트워크 오류 등</exception>
        public static async Task<List<ChapterInfo>> LoadAsync()
        {
            List<OperationResponse> operations = await StageApi.GetOperationsAsync();
            StageProgress.ClearRemote();
            var chapters = new List<ChapterInfo>();
            var loads = new List<Task>();
            foreach (OperationResponse op in operations.OrderBy(o => o.SortOrder))
            {
                var chapter = new ChapterInfo
                {
                    Id = ChapterId(op.OperationId),
                    Title = op.Name,
                    Subtitle = op.Description ?? "",
                };
                foreach (StageSummary s in op.Stages.OrderBy(s => s.StageNo ?? 0))
                {
                    var stage = new StageInfo
                    {
                        StageId = s.StageId,
                        No = s.StageNo ?? 0,
                        Title = s.Title,
                        Par = s.ParMoves,
                        Tutorial = s.StageType == StageType.Tutorial,
                        RewardCoin = s.RewardCoin,
                    };
                    chapter.Stages.Add(stage);
                    StageProgress.SetRemote(chapter.Id, stage.No, new StageProgress.RemoteRecord
                    {
                        Unlocked = s.Unlocked,
                        Cleared = s.Cleared,
                        Best = s.BestMoves ?? 0,
                        Stars = s.Stars,
                    });
                    if (s.Unlocked) loads.Add(LoadMapAsync(stage));
                }
                chapters.Add(chapter);
            }
            await Task.WhenAll(loads);
            return chapters;
        }

        /// <summary>스테이지 상세에서 맵 데이터를 받아 stage.Map에 넣는다 (이미 있으면 그대로)</summary>
        /// <exception cref="ApiException">403 STAGE_LOCKED, 네트워크 오류 등</exception>
        public static async Task LoadMapAsync(StageInfo stage)
        {
            if (stage.Map != null || stage.StageId == null) return;
            StageDetail detail = await StageApi.GetStageAsync(stage.StageId.Value);
            stage.Map = MapData.From(detail.MapData);
            stage.Par = detail.ParMoves;
        }
    }

    /// <summary>
    /// 보내지 못한 결과 (네트워크 오류). 계정별로 PC에 보관했다가 다음에 서버에 닿을 때 다시 보낸다.
    /// 같은 결과를 다시 보내도 서버는 보상을 한 번만 준다.
    /// </summary>
    public static class PendingResults
    {
        private class Item
        {
            public long StageId { get; set; }
            public StageResultRequest Request { get; set; }
        }

        private static string Key => $"executor.{PlayerSession.ProgressPrefix}.pendingResults";

        private static List<Item> Read()
        {
            try { return Json.Deserialize<List<Item>>(PlayerPrefs.GetString(Key, "[]")) ?? new List<Item>(); }
            catch (Exception) { return new List<Item>(); }
        }

        private static void Write(List<Item> items)
        {
            PlayerPrefs.SetString(Key, Json.Serialize(items));
            PlayerPrefs.Save();
        }

        public static int Count => PlayerSession.IsOnline ? Read().Count : 0;

        public static void Add(long stageId, StageResultRequest request)
        {
            if (!PlayerSession.IsOnline) return;
            var items = Read();
            items.Add(new Item { StageId = stageId, Request = request });
            Write(items);
        }

        /// <summary>보관한 결과를 순서대로 다시 보낸다. 네트워크 오류가 나면 거기서 멈추고 남은 것은 다음에</summary>
        /// <returns>보낸 개수</returns>
        public static async Task<int> FlushAsync()
        {
            if (!PlayerSession.IsOnline) return 0;
            var items = Read();
            int sent = 0;
            while (items.Count > 0)
            {
                try
                {
                    await StageApi.SubmitResultAsync(items[0].StageId, items[0].Request);
                }
                catch (ApiException e) when (e.IsNetworkError)
                {
                    break;
                }
                catch (ApiException e)
                {
                    // 서버가 거부한 결과(잠김·삭제된 스테이지 등)는 다시 보내도 안 되므로 버린다
                    Debug.LogWarning($"[PendingResults] 버림 stage {items[0].StageId}: {e.Message}");
                }
                items.RemoveAt(0);
                sent++;
                Write(items);
            }
            return sent;
        }
    }
}
