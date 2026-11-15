using System.Threading.Tasks;
using Executor.Data;
using Executor.Network;
using UnityEngine;

namespace Executor.Play
{
    /// <summary>
    /// 지금 플레이하는 계정. 로그인하면 서버 프로필을 쓰고, 오프라인 테스트면 PC(PlayerPrefs)에만 저장한다.
    /// </summary>
    public static class PlayerSession
    {
        private const string OfflineNameKey = "executor.offline.characterName";

        /// <summary>로그인한 내 프로필. 오프라인·로그아웃이면 null</summary>
        public static MyProfileResponse Profile { get; private set; }

        public static bool IsOnline => Profile != null;

        /// <summary>오프라인 테스트로 들어왔는지</summary>
        public static bool IsOffline { get; private set; }

        public static bool CanPlay => IsOnline || IsOffline;

        public static string DisplayName => IsOnline ? Profile.Nickname : IsOffline ? "오프라인 테스트" : "로그인 안 됨";

        /// <summary>대사의 {characterName}에 들어가는 집행자 이름</summary>
        public static string CharacterName => IsOnline ? Profile.CharacterName ?? "" : PlayerPrefs.GetString(OfflineNameKey, "");

        /// <summary>진행 기록 키 앞부분 (계정마다 따로)</summary>
        public static string ProgressPrefix => IsOnline ? "u" + Profile.UserId : "offline";

        /// <summary>저장된 로그인이 있으면 프로필을 불러와 이어간다</summary>
        public static async Task<bool> TryRestoreAsync()
        {
            if (!TokenStore.HasSession) return false;
            try
            {
                Profile = await ProfileApi.GetMeAsync();
                IsOffline = false;
                return true;
            }
            catch (ApiException e)
            {
                Debug.LogWarning("[PlayerSession] 자동 로그인 실패: " + e);
                return false;
            }
        }

        /// <exception cref="ApiException">401 INVALID_CREDENTIALS 등</exception>
        public static async Task LoginAsync(string email, string password)
        {
            await AuthApi.LoginAsync(email, password);
            Profile = await ProfileApi.GetMeAsync();
            IsOffline = false;
        }

        /// <exception cref="ApiException">400 INVALID_INPUT, 409 DUPLICATE_EMAIL, 409 DUPLICATE_NICKNAME</exception>
        public static async Task SignupAsync(string email, string password, string nickname)
        {
            await AuthApi.SignupAsync(email, password, nickname);
            await LoginAsync(email, password);
        }

        public static async Task LogoutAsync()
        {
            if (IsOnline) await AuthApi.LogoutAsync();
            Profile = null;
            IsOffline = false;
        }

        public static void StartOffline()
        {
            Profile = null;
            IsOffline = true;
        }

        public static async Task RefreshProfileAsync()
        {
            if (IsOnline) Profile = await ProfileApi.GetMeAsync();
        }

        /// <summary>집행자 이름 저장. 로그인 상태면 서버 프로필에 저장한다</summary>
        /// <exception cref="ApiException">서버 저장 실패</exception>
        public static async Task SetCharacterNameAsync(string name)
        {
            if (IsOnline)
            {
                Profile = await ProfileApi.UpdateCharacterNameAsync(name);
            }
            else
            {
                PlayerPrefs.SetString(OfflineNameKey, name);
                PlayerPrefs.Save();
            }
        }

        /// <summary>오프라인 이름 초기화 (테스트용)</summary>
        public static void ClearOfflineName() => PlayerPrefs.DeleteKey(OfflineNameKey);
    }

    /// <summary>튜토리얼 진행 기록 (계정별, PC 저장). 서버에 튜토리얼이 등록되면 서버 기록으로 바꾼다</summary>
    public static class TutorialProgress
    {
        private static string Key(int stageNo) => $"executor.{PlayerSession.ProgressPrefix}.tutorial.best.{stageNo}";

        /// <summary>최소 사용 턴. 클리어 기록이 없으면 0</summary>
        public static int BestMoves(int stageNo) => PlayerPrefs.GetInt(Key(stageNo), 0);

        public static bool IsCleared(int stageNo) => BestMoves(stageNo) > 0;

        /// <summary>1스테이지는 처음부터, 나머지는 앞 스테이지를 클리어하면 열린다</summary>
        public static bool IsUnlocked(int stageNo) => stageNo <= 1 || IsCleared(stageNo - 1);

        /// <returns>최고 기록을 갱신했으면 true</returns>
        public static bool RecordClear(int stageNo, int moves)
        {
            int best = BestMoves(stageNo);
            if (best != 0 && best <= moves) return false;
            PlayerPrefs.SetInt(Key(stageNo), moves);
            PlayerPrefs.Save();
            return true;
        }
    }
}
