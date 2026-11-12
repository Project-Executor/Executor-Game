using System.Threading.Tasks;
using Executor.Data;
using UnityEngine.Networking;

namespace Executor.Network
{
    /// <summary>회원가입·로그인·로그아웃·탈퇴 (/api/auth, /api/users/me)</summary>
    public static class AuthApi
    {
        /// <exception cref="ApiException">400 INVALID_INPUT, 409 DUPLICATE_EMAIL, 409 DUPLICATE_NICKNAME</exception>
        public static Task<SignupResponse> SignupAsync(string email, string password, string nickname) =>
            ApiClient.PostAsync<SignupResponse>("/auth/signup",
                new SignupRequest { Email = email, Password = password, Nickname = nickname }, auth: false);

        /// <summary>로그인하고 토큰을 저장한다</summary>
        /// <exception cref="ApiException">401 INVALID_CREDENTIALS</exception>
        public static async Task<LoginResponse> LoginAsync(string email, string password)
        {
            var response = await ApiClient.PostAsync<LoginResponse>("/auth/login",
                new LoginRequest { Email = email, Password = password }, auth: false);
            TokenStore.Save(response.AccessToken, response.RefreshToken);
            return response;
        }

        /// <summary>서버의 Refresh Token을 지우고 저장된 토큰도 지운다. 서버 호출이 실패해도 로컬 토큰은 지운다</summary>
        public static async Task LogoutAsync()
        {
            try
            {
                if (TokenStore.HasSession) await ApiClient.PostAsync("/auth/logout");
            }
            catch (ApiException)
            {
                // 이미 만료됐거나 네트워크 오류여도 로그아웃은 진행한다
            }
            finally
            {
                TokenStore.Clear();
            }
        }

        public static async Task<bool> IsEmailAvailableAsync(string email)
        {
            var response = await ApiClient.GetAsync<EmailCheckResponse>(
                "/auth/check-email?email=" + UnityWebRequest.EscapeURL(email), auth: false);
            return response.Available;
        }

        /// <summary>회원 탈퇴. 성공하면 저장된 토큰도 지운다</summary>
        /// <exception cref="ApiException">401 INVALID_CREDENTIALS (비밀번호 틀림)</exception>
        public static async Task WithdrawAsync(string password)
        {
            await ApiClient.DeleteAsync("/users/me", new PasswordRequest { Password = password });
            TokenStore.Clear();
        }
    }
}
