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

        /// <summary>자기소개 바꾸기 (255자 이하). 빈 문자열이면 지운다</summary>
        /// <exception cref="ApiException">400 INVALID_INPUT</exception>
        public static Task<MyProfileResponse> UpdateCommentAsync(string comment) =>
            ApiClient.PatchAsync<MyProfileResponse>("/profiles/me", new { comment });

        /// <summary>스킨·테마 장착. null이면 기본으로 (두 값을 모두 보낸다)</summary>
        /// <exception cref="ApiException">403 FORBIDDEN(가지고 있지 않음), 400 INVALID_INPUT(종류가 다름)</exception>
        /// <summary>네 종류를 모두 보낸다 (PUT). null이면 해제</summary>
        public static Task<EquipmentResponse> UpdateEquipmentAsync(long? skinItemId, long? themeItemId, long? cursorItemId, long? deltaItemId) =>
            ApiClient.PutAsync<EquipmentResponse>("/profiles/me/equipment",
                new EquipmentRequest { SkinItemId = skinItemId, ThemeItemId = themeItemId, CursorItemId = cursorItemId, DeltaItemId = deltaItemId });

        /// <summary>프로필 사진 올리기 (PNG·JPEG 바이트, 512KB 이하). 새 버전을 돌려준다</summary>
        /// <exception cref="ApiException">400 INVALID_INPUT</exception>
        public static async Task<long> UploadAvatarAsync(byte[] image)
        {
            var res = await ApiClient.PutAsync<AvatarVersionResponse>("/profiles/me/avatar",
                new { image = System.Convert.ToBase64String(image) });
            return res.AvatarVersion;
        }

        /// <summary>프로필 사진 지우기 (기본 사진으로)</summary>
        public static Task DeleteAvatarAsync() => ApiClient.DeleteAsync("/profiles/me/avatar");

        /// <summary>프로필 사진 그림 파일. 사진이 없으면 null</summary>
        public static Task<byte[]> GetAvatarAsync(long userId) => ApiClient.GetBytesAsync($"/profiles/{userId}/avatar");

        /// <summary>공개 프로필 (공식 스테이지 클리어 수·별 합계)</summary>
        public static Task<ProfileDetailResponse> GetProfileAsync(long userId) =>
            ApiClient.GetAsync<ProfileDetailResponse>($"/profiles/{userId}");

        /// <summary>닉네임 중복 확인 (로그인 불필요)</summary>
        public static async Task<bool> IsNicknameAvailableAsync(string nickname)
        {
            var response = await ApiClient.GetAsync<NicknameCheckResponse>(
                "/profiles/check-nickname?nickname=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(nickname), auth: false);
            return response.Available;
        }
    }
}
