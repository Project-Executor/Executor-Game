using System;
using System.Linq;
using System.Threading.Tasks;
using Executor.Data;
using Executor.Network;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Executor.Dev
{
    /// <summary>
    /// 스테이지·사용자 맵 API 점검용. 빈 GameObject에 붙이고 Play를 누르면 Console에 결과가 찍힌다.
    /// <para>
    /// zz-unity-stage-...@example.com 테스트 계정을 만들고, 그 계정으로 사용자 맵을 만들어
    /// 상세 조회 -> 결과 제출 -> 순위 -> 힌트 -> 공개 전환 -> 수정 -> 삭제를 확인한 뒤 탈퇴로 정리한다.
    /// (운영 DB에는 아직 공식 스테이지가 없어서 플레이 확인은 사용자 맵으로 한다)
    /// </para>
    /// 실행하면 이 PC에 저장된 로그인 토큰은 지워진다. 배포 서버 접속이 안 되면 VPN을 켤 것.
    /// </summary>
    public class StageApiSmokeTest : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private bool useLocalServer = false;

        private const string Password = "password123!";

        private int _pass, _fail;

        private async void Start()
        {
            if (runOnStart) await RunAsync();
        }

        [ContextMenu("Run stage API smoke test")]
        private async void RunFromMenu() => await RunAsync();

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; Debug.Log($"[StageApiSmokeTest] PASS {name} {detail}"); }
            else { _fail++; Debug.LogError($"[StageApiSmokeTest] FAIL {name} {detail}"); }
        }

        /// <summary>action이 지정한 오류 코드로 실패하는지 확인한다</summary>
        private async Task ExpectError(string name, string code, Func<Task> action)
        {
            try
            {
                await action();
                Check(name, false, "오류 없이 성공함");
            }
            catch (ApiException e)
            {
                Check(name, e.Code == code, e.ToString());
            }
        }

        private async Task RunAsync()
        {
            _pass = 0;
            _fail = 0;
            ApiConfig.BaseUrl = useLocalServer ? ApiConfig.LocalBaseUrl : ApiConfig.ProductionBaseUrl;
            string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string email = $"zz-unity-stage-{stamp}@example.com";
            string nickname = "zzS" + stamp.Substring(4);
            bool loggedIn = false;

            Debug.Log($"[StageApiSmokeTest] 시작: {ApiConfig.BaseUrl} / {email}");
            try
            {
                await AuthApi.SignupAsync(email, Password, nickname);
                var login = await AuthApi.LoginAsync(email, Password);
                loggedIn = true;
                long myId = login.User.UserId;

                // ---------- 공식 작전 ----------
                var operations = await StageApi.GetOperationsAsync();
                Check("작전 목록", operations.Any(o => o.Name == "Tutorial"),
                    $"작전 {operations.Count}개, 스테이지 {operations.Sum(o => o.Stages.Count)}개");
                await ExpectError("없는 스테이지 -> 404", "NOT_FOUND", () => StageApi.GetStageAsync(999999999));

                // ---------- 사용자 맵 저장 ----------
                var mapData = JObject.Parse(@"{
                    ""width"": 7, ""height"": 5,
                    ""tiles"": [[0,0,1],[1,0,0]],
                    ""hints"": [""열쇠는 오른쪽 벽 너머에 있다."", { ""text"": ""봇"", ""x"": 2 }]
                }");
                var created = await CustomStageApi.CreateAsync("zz-유니티 점검 맵", 25, mapData);
                long mapId = created.StageId;
                Check("맵 저장 (비공개, 미검증)", mapId > 0 && !created.IsPublic && !created.Verified, $"stageId={mapId}");

                var mine = await CustomStageApi.GetMineAsync();
                Check("내 맵 목록", mine.Any(m => m.StageId == mapId && m.Title == "zz-유니티 점검 맵"));

                await ExpectError("클리어 전 공개 -> 409", "NOT_VERIFIED",
                    () => CustomStageApi.SetVisibilityAsync(mapId, true));

                // ---------- 상세 ----------
                var detail = await StageApi.GetStageAsync(mapId);
                Check("상세: 사용자 맵, 제작자 = 나", !detail.IsOfficial && detail.Creator.UserId == myId
                                                      && detail.StageType == StageType.Normal,
                    $"type={detail.StageType}");
                Check("상세: hints는 빠지고 개수만", detail.HintCount == 2 && !detail.MapData.ContainsKey("hints")
                                                   && detail.MapData.Value<int>("width") == 7);
                Check("상세: 열람한 힌트 없음, 기록 없음",
                    detail.ViewedHintIndexes.Count == 0 && !detail.MyRecord.Cleared && detail.MyRecord.Stars.Max == 3);
                var typed = detail.GetMapData<TestMap>();
                Check("mapData를 C# 클래스로 변환", typed.Width == 7 && typed.Height == 5);

                // ---------- 힌트 (테스트 계정은 힌트 아이템이 없음) ----------
                await ExpectError("힌트 아이템 없음 -> 409", "NOT_ENOUGH_ITEM", () => StageApi.UseHintAsync(mapId, 0));
                await ExpectError("범위 밖 힌트 번호 -> 400", "INVALID_INPUT", () => StageApi.UseHintAsync(mapId, 5));

                // ---------- 결과 제출 ----------
                var fail = await StageApi.SubmitFailAsync(mapId);
                Check("게임 오버 제출", !fail.Cleared && !fail.FirstClear && fail.BestMoves == null);

                var clear = await StageApi.SubmitClearAsync(mapId, moves: 20, hiddenFound: true);
                Check("첫 클리어 (별 3개, 사용자 맵 보상 0)",
                    clear.FirstClear && clear.IsNewBest && clear.Stars.Count == 3 && clear.RewardCoin == 0
                    && clear.ThisRun.Par && clear.NextStageId == null,
                    $"best={clear.BestMoves}, stars={clear.Stars.Count}/{clear.Stars.Max}");

                var again = await StageApi.SubmitClearAsync(mapId, moves: 30, hiddenFound: false);
                Check("더 느린 클리어 (최고 기록 유지, 이번 판 par 아님)",
                    !again.FirstClear && !again.IsNewBest && again.BestMoves == 20 && !again.ThisRun.Par
                    && again.Stars.Count == 3);

                await ExpectError("moves 0 -> 400", "INVALID_INPUT",
                    () => StageApi.SubmitResultAsync(mapId, new StageResultRequest { Cleared = true, Moves = 0 }));

                detail = await StageApi.GetStageAsync(mapId);
                Check("상세: 내 기록 반영", detail.MyRecord.Cleared && detail.MyRecord.BestMoves == 20
                                          && detail.MyRecord.PlayCount == 3,
                    $"playCount={detail.MyRecord.PlayCount}");

                // ---------- 순위 ----------
                var all = await StageApi.GetRankingsAsync(mapId);
                Check("순위 (전체)", all.Rankings.Count == 1 && all.Rankings[0].User.Nickname == nickname
                                    && all.Rankings[0].BestMoves == 20 && all.Me?.Rank == 1);
                var friends = await StageApi.GetRankingsAsync(mapId, RankingScope.Friends, 10);
                Check("순위 (친구, 나 포함)", friends.Me?.Rank == 1);

                // ---------- 공개 / 수정 / 삭제 ----------
                var published = await CustomStageApi.SetVisibilityAsync(mapId, true);
                Check("클리어 후 공개", published.IsPublic && published.Verified);

                var publicPage = await CustomStageApi.GetPublicAsync(0, 50);
                var listed = publicPage.Content.FirstOrDefault(m => m.StageId == mapId);
                Check("공개 목록에 표시", listed != null && listed.MyCleared && listed.ClearCount == 1
                                       && listed.Creator.UserId == myId,
                    $"공개 맵 {publicPage.TotalElements}개");

                await ExpectError("공개 중 수정 -> 409", "PUBLISHED_STAGE",
                    () => CustomStageApi.UpdateAsync(mapId, "zz-수정", 25, mapData));

                await CustomStageApi.SetVisibilityAsync(mapId, false);
                var updated = await CustomStageApi.UpdateAsync(mapId, "zz-수정된 맵", 30,
                    JObject.Parse(@"{ ""width"": 3, ""hints"": [""새 힌트""] }"));
                detail = await StageApi.GetStageAsync(mapId);
                Check("비공개 후 수정 -> 기록 초기화",
                    !updated.Verified && detail.Title == "zz-수정된 맵" && detail.HintCount == 1
                    && !detail.MyRecord.Cleared && detail.MyRecord.PlayCount == 0);

                await CustomStageApi.DeleteAsync(mapId);
                await ExpectError("삭제 후 조회 -> 404", "NOT_FOUND", () => StageApi.GetStageAsync(mapId));
            }
            catch (Exception e)
            {
                Check("예상하지 못한 오류", false, e.ToString());
            }
            finally
            {
                // 이번 실행에서 만든 테스트 계정만 지운다 (만든 맵도 함께 삭제됨)
                if (loggedIn)
                {
                    try
                    {
                        await AuthApi.WithdrawAsync(Password);
                        Check("테스트 계정 탈퇴 (정리)", !TokenStore.HasSession);
                    }
                    catch (Exception e)
                    {
                        Check("테스트 계정 탈퇴 (정리)", false, e + $" -> 서버에 {email} 이 남았으니 지울 것");
                    }
                }
            }

            string summary = $"[StageApiSmokeTest] 결과: PASS {_pass} / FAIL {_fail}";
            if (_fail == 0) Debug.Log(summary); else Debug.LogError(summary);
        }

        /// <summary>mapData 변환 확인용 (실제 맵 구조는 Unity 쪽에서 정한다)</summary>
        private class TestMap
        {
            public int Width { get; set; }
            public int Height { get; set; }
        }
    }
}
