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
    public struct Ray : IEquatable<Ray>
    {
        public Vector3 Position;
        public Vector3 Direction;

        public Ray(Vector3 position, Vector3 direction)
        {
            Position = position;
            Direction = direction;
        }

        public override bool Equals(object obj)
        {
            return (obj is Ray) && Equals((Ray) obj);
        }

        public bool Equals(Ray other)
        {
            return (    this.Position.Equals(other.Position) &&
                    this.Direction.Equals(other.Direction)    );
        }

        public override int GetHashCode()
        {
            return Position.GetHashCode() ^ Direction.GetHashCode();
        }

        public float? Intersects(BoundingBox box)
        {
            float? tMin = null, tMax = null;

            if (FnaMath.WithinEpsilon(Direction.X, 0.0f))
            {
                if (Position.X < box.Min.X || Position.X > box.Max.X)
                {
                    return null;
                }
            }
            else
            {
                tMin = (box.Min.X - Position.X) / Direction.X;
                tMax = (box.Max.X - Position.X) / Direction.X;

                if (tMin > tMax)
                {
                    float? temp = tMin;
                    tMin = tMax;
                    tMax = temp;
                }
            }

            if (FnaMath.WithinEpsilon(Direction.Y, 0.0f))
            {
                if (Position.Y < box.Min.Y || Position.Y > box.Max.Y)
                {
                    return null;
                }
            }
            else
            {
                float tMinY = (box.Min.Y - Position.Y) / Direction.Y;
                float tMaxY = (box.Max.Y - Position.Y) / Direction.Y;

                if (tMinY > tMaxY)
                {
                    float temp = tMinY;
                    tMinY = tMaxY;
                    tMaxY = temp;
                }

                if (    (tMin.HasValue && tMin > tMaxY) ||
                    (tMax.HasValue && tMinY > tMax)    )
                {
                    return null;
                }

                if (!tMin.HasValue || tMinY > tMin) tMin = tMinY;
                if (!tMax.HasValue || tMaxY < tMax) tMax = tMaxY;
            }

            if (FnaMath.WithinEpsilon(Direction.Z, 0.0f))
            {
                if (Position.Z < box.Min.Z || Position.Z > box.Max.Z)
                {
                    return null;
                }
            }
            else
            {
                float tMinZ = (box.Min.Z - Position.Z) / Direction.Z;
                float tMaxZ = (box.Max.Z - Position.Z) / Direction.Z;

                if (tMinZ > tMaxZ)
                {
                    float temp = tMinZ;
                    tMinZ = tMaxZ;
                    tMaxZ = temp;
                }

                if (    (tMin.HasValue && tMin > tMaxZ) ||
                    (tMax.HasValue && tMinZ > tMax)    )
                {
                    return null;
                }

                if (!tMin.HasValue || tMinZ > tMin) tMin = tMinZ;
                if (!tMax.HasValue || tMaxZ < tMax) tMax = tMaxZ;
            }

            if ((tMin.HasValue && tMin < 0) && tMax > 0) return 0;

            if (tMin < 0) return null;

            return tMin;
        }

        public void Intersects(ref BoundingBox box, out float? result)
        {
            result = Intersects(box);
        }

        public float? Intersects(BoundingSphere sphere)
        {
            float? result;
            Intersects(ref sphere, out result);
            return result;
        }

        public float? Intersects(Plane plane)
        {
            float? result;
            Intersects(ref plane, out result);
            return result;
        }

        public float? Intersects(BoundingFrustum frustum)
        {
            float? result;
            frustum.Intersects(ref this, out result);
            return result;
        }

        public void Intersects(ref Plane plane, out float? result)
        {
            float den = FnaMath.Dot(Direction, plane.Normal);
            if (Math.Abs(den) < 0.00001f)
            {
                result = null;
                return;
            }

            result = (-plane.D - FnaMath.Dot(plane.Normal, Position)) / den;

            if (result < 0.0f)
            {
                if (result < -0.00001f)
                {
                    result = null;
                    return;
                }

                result = 0.0f;
            }
        }

        public void Intersects(ref BoundingSphere sphere, out float? result)
        {
            Vector3 difference = sphere.Center - this.Position;

            float differenceLengthSquared = FnaMath.LengthSquared(difference);
            float sphereRadiusSquared = sphere.Radius * sphere.Radius;

            float distanceAlongRay;

            if (differenceLengthSquared < sphereRadiusSquared)
            {
                result = 0.0f;
                return;
            }

            FnaMath.Dot(ref this.Direction, ref difference, out distanceAlongRay);

            if (distanceAlongRay < 0)
            {
                result = null;
                return;
            }

            float dist = (
                sphereRadiusSquared +
                (distanceAlongRay * distanceAlongRay) -
                differenceLengthSquared
            );

            result = (dist < 0) ? null : distanceAlongRay - (float?) Math.Sqrt(dist);
        }

        public static bool operator !=(Ray a, Ray b)
        {
            return !a.Equals(b);
        }

        public static bool operator ==(Ray a, Ray b)
        {
            return a.Equals(b);
        }

        public override string ToString()
        {
            return (
                "{{Position:" + Position.ToString() +
                " Direction:" + Direction.ToString() +
                "}}"
            );
        }
    }
}
