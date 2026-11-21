using UnityEngine;

namespace Executor.Network
{
    /// <summary>
    /// 토큰 보관. "로그인 유지"(KeepLogin)가 켜져 있으면 게임을 다시 켜도 로그인이 유지되도록 PlayerPrefs에 저장한다
    /// (Windows에서는 레지스트리 HKCU\Software\{회사}\{제품}). 꺼져 있으면 이번 실행 동안 메모리에만 둔다.
    /// </summary>
    public static class TokenStore
    {
        private const string AccessKey = "executor.accessToken";
        private const string RefreshKey = "executor.refreshToken";
        private const string KeepKey = "executor.keepLogin";

        /// <summary>
        /// 로그인 유지 (로그인 창의 체크, 기본 켬). 끄면 저장해 둔 토큰을 지우고 이후 토큰은 메모리에만 둔다
        /// </summary>
        public static bool KeepLogin
        {
            get => PlayerPrefs.GetInt(KeepKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(KeepKey, value ? 1 : 0);
                if (!value)
                {
                    PlayerPrefs.DeleteKey(AccessKey);
                    PlayerPrefs.DeleteKey(RefreshKey);
                }
                else if (_loaded && !string.IsNullOrEmpty(_refresh))
                {
                    PlayerPrefs.SetString(AccessKey, _access);
                    PlayerPrefs.SetString(RefreshKey, _refresh);
                }
                PlayerPrefs.Save();
            }
        }

        private static string _access;
        private static string _refresh;
        private static bool _loaded;

        public static string AccessToken
        {
            get { Load(); return _access; }
        }

        public static string RefreshToken
        {
            get { Load(); return _refresh; }
        }

        /// <summary>저장된 로그인이 있는지 (Refresh Token이 있으면 재발급으로 이어갈 수 있다)</summary>
        public static bool HasSession => !string.IsNullOrEmpty(RefreshToken);

        public static void Save(string accessToken, string refreshToken)
        {
            _access = accessToken;
            _refresh = refreshToken;
            _loaded = true;
            if (!KeepLogin) return; // 이번 실행 동안만
            PlayerPrefs.SetString(AccessKey, accessToken);
            PlayerPrefs.SetString(RefreshKey, refreshToken);
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            _access = null;
            _refresh = null;
            _loaded = true;
            PlayerPrefs.DeleteKey(AccessKey);
            PlayerPrefs.DeleteKey(RefreshKey);
            PlayerPrefs.Save();
        }

        private static void Load()
        {
            if (_loaded) return;
            _access = PlayerPrefs.GetString(AccessKey, null);
            _refresh = PlayerPrefs.GetString(RefreshKey, null);
            _loaded = true;
        }
    }
}
