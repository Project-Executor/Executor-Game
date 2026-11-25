using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Executor.Play;
using UnityEngine;

namespace Executor.Network
{
    /// <summary>
    /// 방명록 (/api/guestbook). 전시용이라 로그인 없이 보고 남긴다. 로그인했으면 토큰을 붙여 하루 첫 글 코인 보상을 받는다.
    /// </summary>
    public static class GuestbookApi
    {
        public static Task<GuestbookLatest> GetLatestAsync(int limit = 30) =>
            ApiClient.GetAsync<GuestbookLatest>($"/guestbook/latest?limit={limit}", auth: false);

        /// <exception cref="ApiException">400 INVALID_INPUT(길이), 429 MESSAGE_SPAM(같은 PC 5초 안에 다시)</exception>
        public static Task<GuestbookPostResult> PostAsync(string name, string message) =>
            ApiClient.PostAsync<GuestbookPostResult>("/guestbook",
                new GuestbookRequest { Name = name, Message = message, DeviceId = DeviceId }, auth: PlayerSession.IsOnline);

        /// <summary>이 PC를 나타내는 임의 값 (같은 PC 연속 작성 제한용). 처음 부를 때 만들어 저장한다</summary>
        public static string DeviceId
        {
            get
            {
                const string key = "executor.device";
                string id = PlayerPrefs.GetString(key, "");
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(key, id);
                    PlayerPrefs.Save();
                }
                return id;
            }
        }
    }

    public class GuestbookRequest
    {
        public string Name { get; set; }
        public string Message { get; set; }
        public string DeviceId { get; set; }
    }

    public class GuestbookEntry
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GuestbookLatest
    {
        /// <summary>최신 글을 오래된 것부터</summary>
        public List<GuestbookEntry> Entries { get; set; } = new List<GuestbookEntry>();
        /// <summary>오늘(한국 시간) 남긴 글 수</summary>
        public long Today { get; set; }
        public long Total { get; set; }
    }

    public class GuestbookPostResult
    {
        public GuestbookEntry Entry { get; set; }
        /// <summary>이번에 받은 코인 (로그인한 사용자의 그날 첫 글이면 10)</summary>
        public int RewardCoin { get; set; }
        public int? TotalCoin { get; set; }
    }
}
