// 상점·보유 아이템 (GET /api/items, POST /api/items/{id}/purchase, GET /api/users/me/items). 형식: Executor-Server docs/api-spec.md 7장
using System;

namespace Executor.Data
{
    public static class ItemTypes
    {
        public const string Skin = "SKIN";
        public const string Theme = "THEME";
        /// <summary>메인 터미널 커서 모양 (image = 모양 키, 예: underline-slow:#ff4fa3)</summary>
        public const string Cursor = "CURSOR";
        /// <summary>델타 꾸미기 (image = 꾸미기 키, 예: antenna)</summary>
        public const string Delta = "DELTA";

        public static string Label(string type)
        {
            switch (type)
            {
                case Skin: return "스킨";
                case Theme: return "테마";
                case Cursor: return "커서";
                case Delta: return "델타";
                default: return type;
            }
        }

        /// <summary>상점 탭 (종류, 이름). 종류 null = 전체</summary>
        public static readonly (string type, string label)[] Tabs =
            { (null, "전체"), (Skin, "스킨"), (Theme, "테마"), (Cursor, "커서"), (Delta, "델타 꾸미기") };
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
        /// <summary>보유 수량 (0 또는 1)</summary>
        public int Quantity { get; set; }
    }

    public class PurchaseResponse
    {
        public long ItemId { get; set; }
        /// <summary>구매한 뒤의 보유 수량</summary>
        public int Quantity { get; set; }
        public int SpentCoin { get; set; }
        public int RemainingCoin { get; set; }
    }

    /// <summary>내 보유 아이템 (GET /users/me/items)</summary>
    public class MyItem
    {
        public long ItemId { get; set; }
        public string Name { get; set; }
        public string ItemType { get; set; }
        public string Image { get; set; }
        public int Quantity { get; set; }
        /// <summary>지금 장착 중인지</summary>
        public bool Equipped { get; set; }
        public DateTime AcquiredAt { get; set; }
    }
}
