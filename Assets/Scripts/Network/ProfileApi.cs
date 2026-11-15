using System.Threading.Tasks;
using Executor.Data;

namespace Executor.Network
{
    /// <summary>프로필 (/api/profiles)</summary>
    public static class ProfileApi
    {
        public static Task<MyProfileResponse> GetMeAsync() => ApiClient.GetAsync<MyProfileResponse>("/profiles/me");

        /// <summary>닉네임 정하기·바꾸기 (2~20자, 중복 불가). PATCH는 보낸 필드만 수정한다</summary>
        /// <exception cref="ApiException">400 INVALID_INPUT, 409 DUPLICATE_NICKNAME</exception>
        public static Task<MyProfileResponse> UpdateNicknameAsync(string nickname) =>
            ApiClient.PatchAsync<MyProfileResponse>("/profiles/me", new { nickname });

        /// <summary>닉네임 중복 확인 (로그인 불필요)</summary>
        public static async Task<bool> IsNicknameAvailableAsync(string nickname)
        {
            var response = await ApiClient.GetAsync<NicknameCheckResponse>(
                "/profiles/check-nickname?nickname=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(nickname), auth: false);
            return response.Available;
        }
    }
}
