/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2024 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */

/* Derived from code by the Mono.Xna Team (Copyright 2006).
 * Released under the MIT License. See monoxna.LICENSE for details.
 */

#nullable disable

using System;

namespace Microsoft.Xna.Framework
{
    public class CurveKey : IEquatable<CurveKey>, IComparable<CurveKey>
    {
        public CurveContinuity Continuity
        {
            get;
            set;
        }

        public float Position
        {
            get;
            private set;
        }

        public float TangentIn
        {
            get;
            set;
        }

        public float TangentOut
        {
            get;
            set;
        }

        public float Value
        {
            get;
            set;
        }

        public CurveKey(
            float position,
            float value
        ) : this(
            position,
            value,
            0,
            0,
            CurveContinuity.Smooth
        ) {
        }

        public CurveKey(
            float position,
            float value,
            float tangentIn,
            float tangentOut
        ) : this(
            position,
            value,
            tangentIn,
            tangentOut,
            CurveContinuity.Smooth
        ) {
        }

        public CurveKey(
            float position,
            float value,
            float tangentIn,
            float tangentOut,
            CurveContinuity continuity
        ) {
            Position = position;
            Value = value;
            TangentIn = tangentIn;
            TangentOut = tangentOut;
            Continuity = continuity;
        }

        public CurveKey Clone()
        {
            return new CurveKey(
                Position,
                Value,
                TangentIn,
                TangentOut,
                Continuity
            );
        }

        public int CompareTo(CurveKey other)
        {
            return Position.CompareTo(other.Position);
        }

        public bool Equals(CurveKey other)
        {
            return (this == other);
        }

        public static bool operator !=(CurveKey a, CurveKey b)
        {
            return !(a == b);
        }

        public static bool operator ==(CurveKey a, CurveKey b)
        {
            if (object.Equals(a, null))
            {
                return object.Equals(b, null);
            }

            if (object.Equals(b, null))
            {
                return object.Equals(a, null);
            }

            return (    (a.Position == b.Position) &&
                    (a.Value == b.Value) &&
                    (a.TangentIn == b.TangentIn) &&
                    (a.TangentOut == b.TangentOut) &&
                    (a.Continuity == b.Continuity)    );
        }

        public override bool Equals(object obj)
        {
            return (obj as CurveKey) == this;
        }

        public override int GetHashCode()
        {
            return (
                Position.GetHashCode() ^
                Value.GetHashCode() ^
                TangentIn.GetHashCode() ^
                TangentOut.GetHashCode() ^
                Continuity.GetHashCode()
            );
        }
    }
}
