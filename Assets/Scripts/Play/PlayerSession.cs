using System.Threading.Tasks;
using Executor.Data;
using Executor.Network;
using UnityEngine;

namespace Executor.Play
{
    /// <summary>
    /// 지금 플레이하는 계정. 로그인하면 서버 프로필을 쓰고, 게스트로 플레이하면 PC(PlayerPrefs)에만 저장한다.
    /// 게스트는 전시용: 서버에 닿으면 관리자가 등록한 공식 스테이지(GET /guest/...)를 받고, 기록·결과는 서버에 보내지 않는다.
    /// [게스트로 플레이]를 누를 때마다 새 게스트(진행·닉네임이 비어 있음)로 시작한다.
    /// </summary>
    public static class PlayerSession
    {
        private const string GuestRunKey = "executor.guest.run";

        /// <summary>로그인한 내 프로필. 게스트·로그아웃이면 null</summary>
        public static MyProfileResponse Profile { get; private set; }

        public static bool IsOnline => Profile != null;

        /// <summary>게스트로 플레이 중인지</summary>
        public static bool IsGuest { get; private set; }

        public static bool CanPlay => IsOnline || IsGuest;

        /// <summary>지금 게스트 번호 (게스트를 시작할 때마다 1씩 늘어 진행·닉네임 저장 칸이 새로 생긴다)</summary>
        private static int GuestRun => PlayerPrefs.GetInt(GuestRunKey, 0);

        private static string GuestNameKey => $"executor.guest{GuestRun}.nickname";

        public static string DisplayName => IsOnline ? Profile.Nickname ?? "닉네임 미정"
                                          : IsGuest ? "게스트" : "로그인 안 됨";

        /// <summary>내 닉네임 (대사의 {nickname}). 가입 후 아직 정하지 않았으면 빈 문자열</summary>
        public static string Nickname => IsOnline ? Profile.Nickname ?? "" : PlayerPrefs.GetString(GuestNameKey, "");

        /// <summary>닉네임을 정했는지. 로그인했는데 false면 튜토리얼 1스테이지로 보낸다</summary>
        public static bool HasNickname => !string.IsNullOrEmpty(Nickname);

        /// <summary>진행 기록 키 앞부분 (계정마다 따로)</summary>
        public static string ProgressPrefix => IsOnline ? "u" + Profile.UserId : "guest" + GuestRun;

        /// <summary>저장된 로그인이 있으면 프로필을 불러와 이어간다</summary>
        public static async Task<bool> TryRestoreAsync()
        {
            if (!TokenStore.HasSession) return false;
            try
            {
                Profile = await ProfileApi.GetMeAsync();
                IsGuest = false;
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
            IsGuest = false;
        }

        /// <summary>회원가입 후 바로 로그인한다. 닉네임은 튜토리얼에서 정한다</summary>
        /// <exception cref="ApiException">400 INVALID_INPUT, 409 DUPLICATE_EMAIL</exception>
        public static async Task SignupAsync(string email, string password)
        {
            await AuthApi.SignupAsync(email, password);
            await LoginAsync(email, password);
        }

        public static async Task LogoutAsync()
        {
            if (IsOnline) await AuthApi.LogoutAsync();
            Profile = null;
            IsGuest = false;
        }

        /// <summary>게스트로 플레이 시작: 새 게스트 번호로 바꿔서 앞 게스트의 진행·닉네임을 이어받지 않는다</summary>
        public static void StartGuest()
        {
            Profile = null;
            IsGuest = true;
            PlayerPrefs.SetInt(GuestRunKey, GuestRun + 1);
            PlayerPrefs.Save();
        }

        /// <summary>프로필 수정 응답으로 바꾼다 (자기소개 등)</summary>
        public static void SetProfile(MyProfileResponse profile)
        {
            if (IsOnline && profile != null) Profile = profile;
        }

        public static async Task RefreshProfileAsync()
        {
            if (IsOnline) Profile = await ProfileApi.GetMeAsync();
        }

        /// <summary>닉네임 중복 확인. 게스트면 항상 사용 가능</summary>
        public static async Task<bool> IsNicknameAvailableAsync(string name)
        {
            if (!IsOnline) return true;
            return await ProfileApi.IsNicknameAvailableAsync(name);
        }

        /// <summary>닉네임 저장. 로그인 상태면 서버 프로필에 저장한다</summary>
        /// <exception cref="ApiException">409 DUPLICATE_NICKNAME, 400 INVALID_INPUT</exception>
        public static async Task SetNicknameAsync(string name)
        {
            if (IsOnline)
            {
                Profile = await ProfileApi.UpdateNicknameAsync(name);
            }
            else
            {
                PlayerPrefs.SetString(GuestNameKey, name);
                PlayerPrefs.Save();
            }
        }

        /// <summary>게스트 이름 초기화 (테스트용)</summary>
        public static void ClearGuestName() => PlayerPrefs.DeleteKey(GuestNameKey);
    }
}
