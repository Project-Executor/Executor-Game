using System;
using System.Threading.Tasks;
using Executor.Network;
using UnityEngine;

namespace Executor.Dev
{
    /// <summary>
    /// 서버 연동 점검용. 빈 GameObject에 붙이고 Play를 누르면 Console에 결과가 찍힌다.
    /// <para>
    /// 매번 zz-unity-test-...@example.com 테스트 계정을 새로 만들어 확인하고, 마지막에 탈퇴해서 지운다.
    /// 실행하면 이 PC에 저장된 로그인 토큰은 지워진다 (테스트 계정 토큰으로 덮어쓴 뒤 삭제).
    /// </para>
    /// 배포 서버(*.up.railway.app)에 접속이 안 되면 VPN을 켤 것.
    /// </summary>
    public class ApiSmokeTest : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private bool useLocalServer = false;

        private const string Password = "password123!";

        private async void Start()
        {
            if (runOnStart) await RunAsync();
        }

        [ContextMenu("Run API smoke test")]
        private async void RunFromMenu() => await RunAsync();

        private async Task RunAsync()
        {
            ApiConfig.BaseUrl = useLocalServer ? ApiConfig.LocalBaseUrl : ApiConfig.ProductionBaseUrl;
            string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string email = $"zz-unity-test-{stamp}@example.com";
            string nickname = "zzU" + stamp.Substring(4); // 2~20자
            bool loggedIn = false;
            int pass = 0, fail = 0;

            void Check(string name, bool ok, string detail = "")
            {
                if (ok) { pass++; Debug.Log($"[ApiSmokeTest] PASS {name} {detail}"); }
                else { fail++; Debug.LogError($"[ApiSmokeTest] FAIL {name} {detail}"); }
            }

            Debug.Log($"[ApiSmokeTest] 시작: {ApiConfig.BaseUrl} / {email}");
            try
            {
                Check("이메일 중복 확인 (사용 가능)", await AuthApi.IsEmailAvailableAsync(email));

                var signup = await AuthApi.SignupAsync(email, Password, nickname);
                Check("회원가입", signup.UserId > 0, $"userId={signup.UserId}");

                try
                {
                    await AuthApi.SignupAsync(email, Password, nickname + "x");
                    Check("같은 이메일 다시 가입 -> 409", false);
                }
                catch (ApiException e)
                {
                    Check("같은 이메일 다시 가입 -> 409", e.Code == "DUPLICATE_EMAIL", e.ToString());
                }

                try
                {
                    await AuthApi.LoginAsync(email, "wrong-password");
                    Check("틀린 비밀번호 -> 401", false);
                }
                catch (ApiException e)
                {
                    Check("틀린 비밀번호 -> 401", e.Code == "INVALID_CREDENTIALS", e.ToString());
                }

                var login = await AuthApi.LoginAsync(email, Password);
                loggedIn = true;
                Check("로그인", !string.IsNullOrEmpty(login.AccessToken) && TokenStore.HasSession,
                    $"hasCharacterName={login.User.HasCharacterName}, 만료 {login.AccessTokenExpiresIn}초");

                var me = await ProfileApi.GetMeAsync();
                Check("내 프로필 (한글 포함 응답)", me.Nickname == nickname && me.Email == email,
                    $"nickname={me.Nickname}, coin={me.Coin}");

                string oldRefresh = TokenStore.RefreshToken;
                Check("토큰 재발급", await ApiClient.ForceRefreshAsync() && TokenStore.RefreshToken != oldRefresh,
                    "Refresh Token도 새 값으로 바뀜");

                // 게임을 다시 켰을 때처럼 Access Token 없이 Refresh Token만 남은 상태 -> 자동 재발급 후 요청
                TokenStore.Save("", TokenStore.RefreshToken);
                me = await ProfileApi.GetMeAsync();
                Check("Access Token 없이 자동 재발급 후 조회", me.UserId == signup.UserId);

                // 동시에 여러 요청을 보내도 재발급은 한 번만 (겹치면 Refresh Token 재사용으로 로그아웃됨)
                TokenStore.Save("", TokenStore.RefreshToken);
                var results = await Task.WhenAll(ProfileApi.GetMeAsync(), ProfileApi.GetMeAsync(), ProfileApi.GetMeAsync());
                Check("동시 요청 3건 + 재발급 1번", Array.TrueForAll(results, r => r.UserId == signup.UserId));
            }
            catch (ApiException e)
            {
                Check("예상하지 못한 API 오류", false, e.ToString());
            }
            catch (Exception e)
            {
                Check("예상하지 못한 오류", false, e.ToString());
            }
            finally
            {
                // 이번 실행에서 로그인한 테스트 계정만 지운다
                if (loggedIn)
                {
                    try
                    {
                        await AuthApi.WithdrawAsync(Password);
                        Check("테스트 계정 탈퇴 (정리)", !TokenStore.HasSession);
                    }
                    catch (Exception e)
                    {
                        Check("테스트 계정 탈퇴 (정리)", false, e.ToString() + $" -> 서버에 {email} 이 남았으니 지울 것");
                    }
                }
            }

            string summary = $"[ApiSmokeTest] 결과: PASS {pass} / FAIL {fail}";
            if (fail == 0) Debug.Log(summary); else Debug.LogError(summary);
        }
    }
}
