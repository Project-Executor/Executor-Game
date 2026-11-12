using System;
using System.Text;
using System.Threading.Tasks;
using Executor.Data;
using UnityEngine.Networking;

namespace Executor.Network
{
    /// <summary>
    /// 서버 API 호출 공통 처리.
    /// <list type="bullet">
    ///   <item>auth가 true면 Authorization: Bearer {accessToken}을 붙인다</item>
    ///   <item>401 TOKEN_EXPIRED가 오면 Refresh Token으로 재발급하고 원래 요청을 한 번 다시 보낸다</item>
    ///   <item>재발급은 동시에 여러 요청이 만료돼도 한 번만 보낸다 (Refresh Token이 매번 바뀌므로 겹치면 로그아웃된다)</item>
    ///   <item>Refresh Token도 만료되면 토큰을 지우고 SessionExpired 이벤트를 낸다 -> 로그인 화면으로 보낼 것</item>
    /// </list>
    /// 실패하면 ApiException을 던진다. Unity 메인 스레드에서 호출해야 한다 (UnityWebRequest 제약).
    /// </summary>
    public static class ApiClient
    {
        /// <summary>다시 로그인해야 할 때 (Refresh Token 만료·무효). 로그인 화면 전환은 여기에 연결한다</summary>
        public static event Action SessionExpired;

        private static Task<bool> _refreshing;

        public static Task<T> GetAsync<T>(string path, bool auth = true) => SendAsync<T>("GET", path, null, auth);

        public static Task<T> PostAsync<T>(string path, object body = null, bool auth = true) =>
            SendAsync<T>("POST", path, body, auth);

        public static Task PostAsync(string path, object body = null, bool auth = true) =>
            SendAsync<object>("POST", path, body, auth);

        public static Task<T> PutAsync<T>(string path, object body, bool auth = true) =>
            SendAsync<T>("PUT", path, body, auth);

        public static Task<T> PatchAsync<T>(string path, object body, bool auth = true) =>
            SendAsync<T>("PATCH", path, body, auth);

        public static Task DeleteAsync(string path, object body = null, bool auth = true) =>
            SendAsync<object>("DELETE", path, body, auth);

        /// <summary>지금 바로 토큰을 재발급한다. 성공하면 true, 다시 로그인해야 하면 false</summary>
        public static Task<bool> ForceRefreshAsync() => RefreshOnceAsync(null);

        /// <summary>
        /// 요청을 보내고 응답 본문을 T로 바꿔 돌려준다. 본문이 없으면(204) default.
        /// </summary>
        /// <exception cref="ApiException">서버 오류, 네트워크 오류, 로그인 만료</exception>
        public static async Task<T> SendAsync<T>(string method, string path, object body, bool auth)
        {
            string json = body == null ? null : Json.Serialize(body);
            string token = null;

            if (auth)
            {
                token = TokenStore.AccessToken;
                if (string.IsNullOrEmpty(token))
                {
                    // Access Token이 없지만 Refresh Token이 남아 있으면 (게임을 다시 켠 경우 등) 재발급부터 한다
                    if (!await RefreshOnceAsync(null)) throw ApiException.SessionExpired();
                    token = TokenStore.AccessToken;
                }
            }

            ApiResult result = await RawSendAsync(method, path, json, token);

            if (auth && result.Status == 401 && result.ErrorCode == "TOKEN_EXPIRED")
            {
                if (!await RefreshOnceAsync(token)) throw ApiException.SessionExpired();
                result = await RawSendAsync(method, path, json, TokenStore.AccessToken);
            }

            if (!result.IsSuccess) throw result.ToException();
            if (typeof(T) == typeof(object) || string.IsNullOrEmpty(result.Body)) return default;
            return Json.Deserialize<T>(result.Body);
        }

        /// <param name="expiredToken">만료된 Access Token. 그 사이 다른 요청이 이미 재발급했으면 바로 true</param>
        private static async Task<bool> RefreshOnceAsync(string expiredToken)
        {
            string current = TokenStore.AccessToken;
            if (expiredToken != null && !string.IsNullOrEmpty(current) && current != expiredToken)
            {
                return true;
            }

            Task<bool> task = _refreshing ??= RefreshCoreAsync();
            try
            {
                return await task;
            }
            finally
            {
                if (_refreshing == task) _refreshing = null;
            }
        }

        private static async Task<bool> RefreshCoreAsync()
        {
            string refreshToken = TokenStore.RefreshToken;
            if (string.IsNullOrEmpty(refreshToken))
            {
                ExpireSession();
                return false;
            }

            ApiResult result = await RawSendAsync("POST", "/auth/reissue",
                Json.Serialize(new ReissueRequest { RefreshToken = refreshToken }), null);

            if (result.IsSuccess)
            {
                var tokens = Json.Deserialize<TokenResponse>(result.Body);
                TokenStore.Save(tokens.AccessToken, tokens.RefreshToken);
                return true;
            }

            if (result.Status == 401)
            {
                ExpireSession();
                return false;
            }

            // 네트워크 오류나 서버 오류는 로그아웃시키지 않고 그대로 알린다 (다시 시도하면 될 수 있음)
            throw result.ToException();
        }

        private static void ExpireSession()
        {
            TokenStore.Clear();
            SessionExpired?.Invoke();
        }

        private static async Task<ApiResult> RawSendAsync(string method, string path, string json, string token)
        {
            using var request = new UnityWebRequest(ApiConfig.BaseUrl + path, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            if (json != null)
            {
                // 한글이 깨지지 않도록 UTF-8 바이트로 보낸다
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            }
            request.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrEmpty(token))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);
            }
            request.timeout = ApiConfig.TimeoutSeconds;

            await request.SendWebRequest().AsTask();
            return ApiResult.From(request);
        }

        /// <summary>UnityWebRequest를 await할 수 있게 한다</summary>
        private static Task AsTask(this UnityWebRequestAsyncOperation operation)
        {
            var tcs = new TaskCompletionSource<bool>();
            if (operation.isDone) tcs.SetResult(true);
            else operation.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }

        private readonly struct ApiResult
        {
            public long Status { get; }
            public string Body { get; }
            public string NetworkError { get; }
            public string ErrorCode { get; }
            public string ErrorMessage { get; }

            public bool IsSuccess => NetworkError == null && Status >= 200 && Status < 300;

            private ApiResult(long status, string body, string networkError, string errorCode, string errorMessage)
            {
                Status = status;
                Body = body;
                NetworkError = networkError;
                ErrorCode = errorCode;
                ErrorMessage = errorMessage;
            }

            public static ApiResult From(UnityWebRequest request)
            {
                string body = request.downloadHandler?.text;

                // 서버에 닿지 못함 (인터넷 끊김, 주소 차단, 시간 초과 등)
                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    return new ApiResult(0, null, request.error, ApiException.NetworkError,
                        "서버에 연결할 수 없습니다. (" + request.error + ")");
                }

                string code = null, message = null;
                if (request.responseCode >= 400 && !string.IsNullOrEmpty(body))
                {
                    try
                    {
                        var error = Json.Deserialize<ErrorResponse>(body);
                        code = error?.Code;
                        message = error?.Message;
                    }
                    catch (Exception)
                    {
                        // 서버 형식이 아닌 오류 (프록시 오류 페이지 등)
                    }
                }

                return new ApiResult(request.responseCode, body, null,
                    code ?? (request.responseCode >= 400 ? "HTTP_" + request.responseCode : null),
                    message ?? request.error);
            }

            public ApiException ToException() => new ApiException(Status, ErrorCode, ErrorMessage);
        }
    }
}
