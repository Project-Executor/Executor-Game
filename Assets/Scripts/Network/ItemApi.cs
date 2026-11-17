using System.Collections.Generic;
using System.Threading.Tasks;
using Executor.Data;

namespace Executor.Network
{
    /// <summary>상점과 보유 아이템 (/api/items, /api/users/me/items). 로그인 필요</summary>
    public static class ItemApi
    {
        /// <summary>판매 중인 아이템 (아이템 ID 순). type이 null이면 전체</summary>
        public static Task<List<ShopItem>> GetShopItemsAsync(string type = null) =>
            ApiClient.GetAsync<List<ShopItem>>(type == null ? "/items" : $"/items?type={type}");

        /// <summary>구매. 스킨·테마는 quantity를 무시하고 1개, 힌트는 1~99</summary>
        /// <exception cref="ApiException">409 NOT_ENOUGH_COIN, 409 ALREADY_OWNED, 404 NOT_FOUND(판매 중단), 400 INVALID_INPUT</exception>
        public static Task<PurchaseResponse> PurchaseAsync(long itemId, int quantity = 1) =>
            ApiClient.PostAsync<PurchaseResponse>($"/items/{itemId}/purchase", new PurchaseRequest { Quantity = quantity });

        /// <summary>내 보유 아이템 (아이템 ID 순, 다 쓴 힌트 제외). type이 null이면 전체</summary>
        public static Task<List<MyItem>> GetMyItemsAsync(string type = null) =>
            ApiClient.GetAsync<List<MyItem>>(type == null ? "/users/me/items" : $"/users/me/items?type={type}");
    }
}
