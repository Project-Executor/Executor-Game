using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Executor.Network
{
    /// <summary>서버 연결 확인 (GET /health). 로그인 창의 "$ ping api 서버 연결됨 · 32ms"</summary>
    public static class HealthApi
    {
        /// <summary>
        /// 서버에 닿으면 (true, 걸린 ms). 응답 코드와 상관없이 HTTP 응답이 오면 닿은 것으로 본다
        /// (예전 서버처럼 /health가 없어 404여도 서버는 살아 있다). 연결 자체가 안 되면 (false, ms)
        /// </summary>
        public static async Task<(bool reached, int ms)> PingAsync()
        {
            var watch = Stopwatch.StartNew();
            using var request = UnityWebRequest.Get(ApiConfig.BaseUrl + "/health");
            request.timeout = 5;
            var op = request.SendWebRequest();
            var tcs = new TaskCompletionSource<bool>();
            if (op.isDone) tcs.SetResult(true);
            else op.completed += _ => tcs.TrySetResult(true);
            await tcs.Task;
            watch.Stop();
            bool reached = request.result != UnityWebRequest.Result.ConnectionError && request.responseCode > 0;
            return (reached, (int)watch.ElapsedMilliseconds);
        }
    }
}
