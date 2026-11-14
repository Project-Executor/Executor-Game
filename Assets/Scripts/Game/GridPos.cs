using System;

namespace Executor.Game
{
    /// <summary>칸 좌표. x는 왼쪽에서 오른쪽, y는 위에서 아래 (map_data와 같음)</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public int X { get; }
        public int Y { get; }

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static GridPos operator +(GridPos a, GridPos b) => new GridPos(a.X + b.X, a.Y + b.Y);

        /// <summary>가로·세로·대각선으로 1칸 이내 (자기 칸 포함) = 3x3 범위</summary>
        public bool IsWithin3x3(GridPos other) => Math.Abs(X - other.X) <= 1 && Math.Abs(Y - other.Y) <= 1;

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);
        public override string ToString() => $"({X}, {Y})";
    }

    /// <summary>W 위, A 왼쪽, S 아래, D 오른쪽</summary>
    public enum Direction
    {
        Up,
        Left,
        Down,
        Right
    }

    public static class DirectionExtensions
    {
        public static GridPos Delta(this Direction direction) => direction switch
        {
            Direction.Up => new GridPos(0, -1),
            Direction.Left => new GridPos(-1, 0),
            Direction.Down => new GridPos(0, 1),
            Direction.Right => new GridPos(1, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };

        /// <summary>'W','A','S','D' (대소문자 무관) -> 방향. 다른 글자면 false</summary>
        public static bool TryFromKey(char key, out Direction direction)
        {
            switch (char.ToUpperInvariant(key))
            {
                case 'W': direction = Direction.Up; return true;
                case 'A': direction = Direction.Left; return true;
                case 'S': direction = Direction.Down; return true;
                case 'D': direction = Direction.Right; return true;
                default: direction = default; return false;
            }
        }
    }
}
