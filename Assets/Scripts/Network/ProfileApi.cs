using System.Threading.Tasks;
using Executor.Data;

namespace Executor.Network
{
    /// <summary>프로필 (/api/profiles)</summary>
    public static class ProfileApi
    {
        public static Task<MyProfileResponse> GetMeAsync() => ApiClient.GetAsync<MyProfileResponse>("/profiles/me");

        /// <summary>집행자 이름(튜토리얼에서 입력, 1~20자, 중복 허용)을 바꾼다. PATCH는 보낸 필드만 수정한다</summary>
        /// <exception cref="ApiException">400 INVALID_INPUT</exception>
        public static Task<MyProfileResponse> UpdateCharacterNameAsync(string characterName) =>
            ApiClient.PatchAsync<MyProfileResponse>("/profiles/me", new { characterName });
    }
}
