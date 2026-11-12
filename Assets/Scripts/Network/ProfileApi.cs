using System.Threading.Tasks;
using Executor.Data;

namespace Executor.Network
{
    /// <summary>프로필 (/api/profiles)</summary>
    public static class ProfileApi
    {
        public static Task<MyProfileResponse> GetMeAsync() => ApiClient.GetAsync<MyProfileResponse>("/profiles/me");
    }
}
