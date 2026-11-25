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
    /// 작전 하나 = 챕터 하나 (id "op{operationId}"), 작전마다 소속 레이어(대주제)를 담는다. 해금·클리어·최고 기록은 서버 값을 StageProgress에 넣는다.
    /// 맵 데이터는 스테이지 상세(GET /stages/{id})에서 받는다. 잠긴 스테이지는 서버가 상세를 막으므로(403) 열린 것만 미리 받고,
    /// 나머지는 시작할 때 받는다 (LoadMapAsync).
    /// </summary>
    public static class ServerCatalog
    {
        public static string ChapterId(long operationId) => "op" + operationId;

        /// <param name="guest">게스트로 플레이: 로그인 없이 받고(GET /guest/operations), 진행·해금은 PC 기록(StageProgress)으로 정한다</param>
        /// <exception cref="ApiException">네트워크 오류 등</exception>
        public static async Task<List<ChapterInfo>> LoadAsync(bool guest = false)
        {
            List<OperationResponse> operations = guest ? await StageApi.GetGuestOperationsAsync() : await StageApi.GetOperationsAsync();
            StageProgress.ClearRemote();
            var chapters = new List<ChapterInfo>();
            var loads = new List<Task>();
            // 플레이 순서: 레이어 순서 -> 레이어 안 작전 순서 (서버 응답 순서와 같다)
            foreach (OperationResponse op in operations.OrderBy(o => o.Layer?.SortOrder ?? 0).ThenBy(o => o.SortOrder))
            {
                var chapter = new ChapterInfo
                {
                    Id = ChapterId(op.OperationId),
                    Title = op.Name,
                    Subtitle = op.Description ?? "",
                    LayerId = op.Layer == null ? "" : "layer" + op.Layer.LayerId,
                    LayerName = op.Layer?.Name ?? "",
                    LayerDescription = op.Layer?.Description ?? "",
                    LayerOrder = op.Layer?.SortOrder ?? 0,
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
                        Kind = s.StageType,
                        RewardCoin = s.RewardCoin,
                        BoardPos = s.BoardX.HasValue && s.BoardY.HasValue
                            ? new Vector2((float)s.BoardX.Value, (float)s.BoardY.Value)
                            : (Vector2?)null,
                    };
                    chapter.Stages.Add(stage);
                    if (guest)
                    {
                        loads.Add(TryLoadMapAsync(stage));  // 게스트는 해금을 PC에서 정하므로 맵을 모두 미리 받는다
                        continue;
                    }
                    StageProgress.SetRemote(chapter.Id, stage.No, new StageProgress.RemoteRecord
                    {
                        Unlocked = s.Unlocked,
                        Cleared = s.Cleared,
                        Best = s.BestMoves ?? 0,
                        Stars = s.Stars,
                    });
                    if (s.Unlocked) loads.Add(TryLoadMapAsync(stage));
                }
                chapters.Add(chapter);
            }
            await Task.WhenAll(loads);
            return chapters;
        }

        /// <summary>
        /// 목록을 불러올 때 쓰는 맵 받기. 맵 하나의 형식이 잘못돼도 나머지 스테이지는 불러오도록 그 맵만 비워 둔다
        /// (그 스테이지를 누르면 LoadMapAsync가 다시 시도하고 이유를 보여 준다)
        /// </summary>
        private static async Task TryLoadMapAsync(StageInfo stage)
        {
            try
            {
                await LoadMapAsync(stage);
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                UnityEngine.Debug.LogError($"[ServerCatalog] 스테이지 {stage.StageId} '{stage.Title}'의 맵 데이터 형식이 잘못되었습니다: {e.Message}");
            }
        }

        /// <summary>스테이지 상세에서 맵 데이터를 받아 stage.Map에 넣는다 (이미 있으면 그대로)</summary>
        /// <exception cref="ApiException">403 STAGE_LOCKED, 네트워크 오류 등</exception>
        /// <exception cref="Newtonsoft.Json.JsonException">맵 데이터 형식이 잘못됨 (관리자 웹에서 고쳐야 함)</exception>
        public static async Task LoadMapAsync(StageInfo stage)
        {
            if (stage.Map != null || stage.StageId == null) return;
            StageDetail detail = PlayerSession.IsOnline
                ? await StageApi.GetStageAsync(stage.StageId.Value)
                : await StageApi.GetGuestStageAsync(stage.StageId.Value);
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
