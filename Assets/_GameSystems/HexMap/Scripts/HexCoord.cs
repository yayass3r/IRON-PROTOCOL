// ============================================================================================
// IRON PROTOCOL - HexCoord.cs
// Immutable struct representing a hex cell position using axial/cube coordinates.
// Convention: cube coordinates (q, r, s) where s = -q - r.
// Uses flat-topped hex layout for world-space conversion.
// ============================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronProtocol.HexMap
{
    /// <summary>
    /// Immutable value type representing a position on a hex grid using axial coordinates
    /// (q, r) with the implicit third cube coordinate s = -q - r.
    /// <para>
    /// Provides neighbor lookup, distance calculation, direction finding, and
    /// conversion to/from world-space positions for flat-topped hex layouts.
    /// </para>
    /// </summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        // ----------------------------------------------------------------------------------------
        // Constants
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// The six hex neighbor direction offsets in cube coordinates (q, r).
        /// <para>
        /// Index: 0=East, 1=NE, 2=NW, 3=West, 4=SW, 5=SE  (flat-topped)
        /// </para>
        /// </summary>
        public static readonly (int q, int r)[] NEIGHBORS = new (int q, int r)[]
        {
            (+1,  0), // 0 - East
            (+1, -1), // 1 - NE
            ( 0, -1), // 2 - NW
            (-1,  0), // 3 - West
            (-1, +1), // 4 - SW
            ( 0, +1)  // 5 - SE
        };

        /// <summary>
        /// Angle in degrees for each neighbor direction, used for <see cref="GetDirectionTo"/>.
        /// Flat-topped: direction 0 (East) = 0°, increasing counter-clockwise.
        /// </summary>
        private static readonly float[] DirectionAngles =
        {
            0f,    // 0 - East
            60f,   // 1 - NE
            120f,  // 2 - NW
            180f,  // 3 - West
            240f,  // 4 - SW
            300f   // 5 - SE
        };

        /// <summary>
        /// Factor for converting between hex size and the horizontal distance between
        /// hex centers in a flat-topped layout:  horiz = hexSize * 2.0.
        /// </summary>
        private const float FlatTopHorizontalSpacing = 2.0f;

        /// <summary>
        /// Factor for converting between hex size and the vertical distance between
        /// hex centers in a flat-topped layout: vert = hexSize * sqrt(3).
        /// </summary>
        private const float FlatTopVerticalSpacing = 1.7320508075688772f; // Mathf.Sqrt(3)

        // ----------------------------------------------------------------------------------------
        // Fields
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// The q (column) axial coordinate.
        /// </summary>
        public readonly int Q;

        /// <summary>
        /// The r (row) axial coordinate.
        /// </summary>
        public readonly int R;

        /// <summary>
        /// The implicit s cube coordinate, always equal to -Q - R.
        /// </summary>
        public int S => -Q - R;

        // ----------------------------------------------------------------------------------------
        // Constructors
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Creates a new hex coordinate from axial components.
        /// The third cube coordinate s is computed as -q - r.
        /// </summary>
        /// <param name="q">The q (column) component.</param>
        /// <param name="r">The r (row) component.</param>
        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        // ----------------------------------------------------------------------------------------
        // Neighbor & Direction
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Returns the hex coordinate of the neighbor in the specified direction (0–5).
        /// <para>Directions (flat-topped): 0=East, 1=NE, 2=NW, 3=West, 4=SW, 5=SE.</para>
        /// </summary>
        /// <param name="direction">Direction index 0 through 5.</param>
        /// <returns>The neighboring <see cref="HexCoord"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="direction"/> is not in range [0, 5].
        /// </exception>
        public HexCoord GetNeighbor(int direction)
        {
            if (direction < 0 || direction > 5)
                throw new ArgumentOutOfRangeException(nameof(direction), "Direction must be 0–5.");

            return new HexCoord(Q + NEIGHBORS[direction].q, R + NEIGHBORS[direction].r);
        }

        /// <summary>
        /// Returns all six neighbor coordinates.
        /// </summary>
        /// <returns>An array of six <see cref="HexCoord"/> values.</returns>
        public HexCoord[] GetAllNeighbors()
        {
            var neighbors = new HexCoord[6];
            for (int i = 0; i < 6; i++)
            {
                neighbors[i] = GetNeighbor(i);
            }
            return neighbors;
        }

        /// <summary>
        /// Computes the shortest hex-grid distance between this coordinate and another.
        /// </summary>
        /// <param name="other">The target hex coordinate.</param>
        /// <returns>The distance in hex cells (always non-negative).</returns>
        public int DistanceTo(HexCoord other)
        {
            return (Math.Abs(Q - other.Q) + Math.Abs(R - other.R) + Math.Abs(S - other.S)) / 2;
        }

        /// <summary>
        /// Determines the primary direction (0–5) from this hex toward <paramref name="target"/>.
        /// <para>
        /// The direction is determined by computing the angle from this cell to the target
        /// and snapping it to the nearest 60° sector. Returns the closest neighbor index.
        /// </para>
        /// </summary>
        /// <param name="target">The destination hex coordinate.</param>
        /// <returns>An integer 0–5 representing the direction toward the target.</returns>
        public int GetDirectionTo(HexCoord target)
        {
            // Convert displacement to world-space angle for flat-topped hexes.
            float dq = target.Q - Q;
            float dr = target.R - R;

            // World-space offset (flat-topped: x = size * (3/2 * q), y = size * (sqrt(3)/2 * q + sqrt(3) * r))
            float wx = 1.5f * dq;
            float wy = (FlatTopVerticalSpacing * 0.5f) * dq + FlatTopVerticalSpacing * dr;

            // Angle in degrees: 0° = East, counter-clockwise positive (Unity Y-up).
            float angle = Mathf.Atan2(wy, wx) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            // Snap to nearest 60° sector.
            float sectorAngle = (angle + 30f) % 360f;
            int sector = Mathf.RoundToInt(sectorAngle / 60f) % 6;

            // Remap sector indices to our NEIGHBORS ordering:
            // Sector 0 (East)     -> neighbor 0
            // Sector 1 (60° CCW)  -> neighbor 1
            // Sector 2 (120° CCW) -> neighbor 2
            // Sector 3 (West)     -> neighbor 3
            // Sector 4 (240° CCW) -> neighbor 4
            // Sector 5 (300° CCW) -> neighbor 5
            return sector;
        }

        // ----------------------------------------------------------------------------------------
        // Coordinate Conversion (Flat-Topped)
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Converts this hex coordinate to a world-space position.
        /// Uses the flat-topped hex layout.
        /// </summary>
        /// <param name="origin">The world-space position of the hex at (0, 0).</param>
        /// <param name="hexSize">The outer radius (center to vertex) of a single hex.</param>
        /// <returns>The world-space position of this hex's center.</returns>
        public Vector3 HexToWorld(Vector3 origin, float hexSize)
        {
            float x = hexSize * (FlatTopHorizontalSpacing * Q + 0.5f * FlatTopHorizontalSpacing * R);
            float y = hexSize * (FlatTopVerticalSpacing * R);
            return new Vector3(x, 0f, y) + origin;
        }

        /// <summary>
        /// Converts a world-space position to the nearest hex coordinate.
        /// Uses the flat-topped hex layout.
        /// </summary>
        /// <param name="worldPos">The world-space position to convert.</param>
        /// <param name="origin">The world-space position of hex (0, 0).</param>
        /// <param name="hexSize">The outer radius (center to vertex) of a single hex.</param>
        /// <returns>The nearest <see cref="HexCoord"/> to the given world position.</returns>
        public static HexCoord WorldToHex(Vector3 worldPos, Vector3 origin, float hexSize)
        {
            Vector3 local = worldPos - origin;

            // Convert world position to fractional axial coordinates (flat-topped).
            float fq = (FlatTopHorizontalSpacing / 3.0f * local.x - FlatTopVerticalSpacing / 3.0f * local.z) / hexSize;
            float fr = (FlatTopVerticalSpacing * 2.0f / 3.0f * local.z) / hexSize;

            // Round fractional hex to nearest integer hex.
            return HexRound(fq, fr);
        }

        /// <summary>
        /// Rounds fractional axial coordinates to the nearest integer hex coordinate
        /// using cube-coordinate rounding.
        /// </summary>
        private static HexCoord HexRound(float fq, float fr)
        {
            float fs = -fq - fr;

            int rq = Mathf.RoundToInt(fq);
            int rr = Mathf.RoundToInt(fr);
            int rs = Mathf.RoundToInt(fs);

            float dq = Mathf.Abs(rq - fq);
            float dr = Mathf.Abs(rr - fr);
            float ds = Mathf.Abs(rs - fs);

            // Recompute the coordinate with the largest rounding error.
            if (dq > dr && dq > ds)
            {
                rq = -rr - rs;
            }
            else if (dr > ds)
            {
                rr = -rq - rs;
            }
            else
            {
                rs = -rq - rr;
                // rs is not stored; s is always derived as -q - r, so we only need q and r.
            }

            return new HexCoord(rq, rr);
        }

        // ----------------------------------------------------------------------------------------
        // Equality & Hashing
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Indicates whether this hex coordinate is equal to another.
        /// </summary>
        public bool Equals(HexCoord other)
        {
            return Q == other.Q && R == other.R;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is HexCoord other && Equals(other);
        }

        /// <summary>
        /// Returns the hash code for this hex coordinate. Combines Q and R into
        /// a unique integer using a prime-based hash.
        /// </summary>
        public override int GetHashCode()
        {
            // FNV-1a inspired hash mixing for two int32 values.
            unchecked
            {
                int hash = 2166136261;
                hash = hash * 16777619 ^ Q;
                hash = hash * 16777619 ^ R;
                return hash;
            }
        }

        /// <summary>
        /// Tests equality between two <see cref="HexCoord"/> values.
        /// </summary>
        public static bool operator ==(HexCoord left, HexCoord right) => left.Equals(right);

        /// <summary>
        /// Tests inequality between two <see cref="HexCoord"/> values.
        /// </summary>
        public static bool operator !=(HexCoord left, HexCoord right) => !left.Equals(right);

        /// <summary>
        /// Returns a human-readable string representation: "(q, r)".
        /// </summary>
        public override string ToString() => $"({Q}, {R})";

        /// <summary>
        /// Returns a string that includes all three cube coordinates: "(q, r, s)".
        /// </summary>
        public string ToCubeString() => $"({Q}, {R}, {S})";
    }
}
