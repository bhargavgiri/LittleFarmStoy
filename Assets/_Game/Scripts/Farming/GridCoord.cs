using System;

namespace LittleFarmStory.Farming
{
    /// <summary>
    /// Integer plot coordinate inside a single <see cref="FarmGrid"/>.
    /// Kept as a struct so future save data and dictionaries stay allocation free.
    /// </summary>
    [Serializable]
    public struct GridCoord : IEquatable<GridCoord>
    {
        public int X;
        public int Z;

        public GridCoord(int x, int z)
        {
            X = x;
            Z = z;
        }

        public static GridCoord Invalid => new GridCoord(int.MinValue, int.MinValue);

        public bool IsValid => X != int.MinValue && Z != int.MinValue;

        public bool Equals(GridCoord other)
        {
            return X == other.X && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is GridCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Z;
            }
        }

        public override string ToString()
        {
            return "(" + X + "," + Z + ")";
        }

        public static bool operator ==(GridCoord a, GridCoord b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(GridCoord a, GridCoord b)
        {
            return !a.Equals(b);
        }
    }
}
