// 상점·보유 아이템 (GET /api/items, POST /api/items/{id}/purchase, GET /api/users/me/items). 형식: Executor-Server docs/api-spec.md 7장
using System;

namespace Executor.Data
{
    public static class ItemTypes
    {
        public const string Skin = "SKIN";
        public const string Theme = "THEME";
        public const string Hint = "HINT";

        public static string Label(string type)
        {
            switch (type)
            {
                case Skin: return "스킨";
                case Theme: return "테마";
                case Hint: return "힌트";
                default: return type;
            }
        }

        /// <summary>스킨·테마는 하나만 가질 수 있다 (힌트는 여러 개)</summary>
        public static bool IsUnique(string type) => type == Skin || type == Theme;
    }

    /// <summary>상점 아이템 (판매 중인 것만, 내 보유 여부 포함)</summary>
    public class ShopItem
    {
        public long ItemId { get; set; }
        public string Name { get; set; }
        public string Image { get; set; }
        public string Description { get; set; }
        /// <summary>ItemTypes 상수</summary>
        public string ItemType { get; set; }
        public int Price { get; set; }
        public bool Owned { get; set; }
        /// <summary>보유 수량 (힌트는 여러 개)</summary>
        public int Quantity { get; set; }
    }

    /// <summary>POST /items/{id}/purchase 본문. 스킨·테마는 무시하고 1개, 힌트는 1~99</summary>
    public class PurchaseRequest
    {
        public int Quantity { get; set; } = 1;
    }

    public class PurchaseResponse
    {
        public long ItemId { get; set; }
        /// <summary>구매한 뒤의 보유 수량</summary>
        public int Quantity { get; set; }
        public int SpentCoin { get; set; }
        public int RemainingCoin { get; set; }
    }

    /// <summary>내 보유 아이템 (GET /users/me/items). 다 쓴 힌트는 빠진다</summary>
    public class MyItem
    {
        public long ItemId { get; set; }
        public string Name { get; set; }
        public string ItemType { get; set; }
        public string Image { get; set; }
        public int Quantity { get; set; }
        /// <summary>지금 장착 중인 스킨·테마인지</summary>
        public bool Equipped { get; set; }
        public DateTime AcquiredAt { get; set; }
    }
}
