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
using System.Collections.Generic;

namespace Microsoft.Xna.Framework
{
    public struct BoundingSphere : IEquatable<BoundingSphere>
    {
        public Vector3 Center;

        public float Radius;

        public BoundingSphere(Vector3 center, float radius)
        {
            this.Center = center;
            this.Radius = radius;
        }

        public BoundingSphere Transform(Matrix matrix)
        {
            BoundingSphere sphere = new BoundingSphere();
            sphere.Center = FnaMath.Transform(this.Center, matrix);
            sphere.Radius = this.Radius *
                (
                    (float) Math.Sqrt((double) Math.Max(
                        ((matrix.M11 * matrix.M11) + (matrix.M12 * matrix.M12)) + (matrix.M13 * matrix.M13),
                        Math.Max(
                            ((matrix.M21 * matrix.M21) + (matrix.M22 * matrix.M22)) + (matrix.M23 * matrix.M23),
                            ((matrix.M31 * matrix.M31) + (matrix.M32 * matrix.M32)) + (matrix.M33 * matrix.M33))
                        )
                    )
                );
            return sphere;
        }

        public void Transform(ref Matrix matrix, out BoundingSphere result)
        {
            result.Center = FnaMath.Transform(this.Center, matrix);
            result.Radius = this.Radius *
                (
                    (float) Math.Sqrt((double) Math.Max(
                        ((matrix.M11 * matrix.M11) + (matrix.M12 * matrix.M12)) + (matrix.M13 * matrix.M13),
                        Math.Max(
                            ((matrix.M21 * matrix.M21) + (matrix.M22 * matrix.M22)) + (matrix.M23 * matrix.M23),
                            ((matrix.M31 * matrix.M31) + (matrix.M32 * matrix.M32)) + (matrix.M33 * matrix.M33))
                        )
                    )
                );
        }

        public void Contains(ref BoundingBox box, out ContainmentType result)
        {
            result = this.Contains(box);
        }

        public void Contains(ref BoundingSphere sphere, out ContainmentType result)
        {
            result = Contains(sphere);
        }

        public void Contains(ref Vector3 point, out ContainmentType result)
        {
            result = Contains(point);
        }

        public ContainmentType Contains(BoundingBox box)
        {
            float farX = Math.Max((Center.X - box.Min.X) * (Center.X - box.Min.X), (Center.X - box.Max.X) * (Center.X - box.Max.X));
            float farY = Math.Max((Center.Y - box.Min.Y) * (Center.Y - box.Min.Y), (Center.Y - box.Max.Y) * (Center.Y - box.Max.Y));
            float farZ = Math.Max((Center.Z - box.Min.Z) * (Center.Z - box.Min.Z), (Center.Z - box.Max.Z) * (Center.Z - box.Max.Z));
            bool inside = farX + farY + farZ <= Radius * Radius;

            if (inside)
            {
                return ContainmentType.Contains;
            }

            double dmin = 0;

            if (Center.X < box.Min.X)
            {
                dmin += (Center.X - box.Min.X) * (Center.X - box.Min.X);
            }
            else if (Center.X > box.Max.X)
            {
                dmin += (Center.X - box.Max.X) * (Center.X - box.Max.X);
            }

            if (Center.Y < box.Min.Y)
            {
                dmin += (Center.Y - box.Min.Y) * (Center.Y - box.Min.Y);
            }
            else if (Center.Y > box.Max.Y)
            {
                dmin += (Center.Y - box.Max.Y) * (Center.Y - box.Max.Y);
            }

            if (Center.Z < box.Min.Z)
            {
                dmin += (Center.Z - box.Min.Z) * (Center.Z - box.Min.Z);
            }
            else if (Center.Z > box.Max.Z)
            {
                dmin += (Center.Z - box.Max.Z) * (Center.Z - box.Max.Z);
            }

            if (dmin <= Radius * Radius)
            {
                return ContainmentType.Intersects;
            }

            return ContainmentType.Disjoint;
        }

        public ContainmentType Contains(BoundingFrustum frustum)
        {
            bool inside = true;

            Vector3[] corners = frustum.corners;
            foreach (Vector3 corner in corners)
            {
                if (this.Contains(corner) == ContainmentType.Disjoint)
                {
                    inside = false;
                    break;
                }
            }
            if (inside)
            {
                return ContainmentType.Contains;
            }

            double dmin = 0;

            if (dmin <= Radius * Radius)
            {
                return ContainmentType.Intersects;
            }

            return ContainmentType.Disjoint;
        }

        public ContainmentType Contains(BoundingSphere sphere)
        {
            float sqDistance;
            FnaMath.DistanceSquared(ref sphere.Center, ref Center, out sqDistance);

            if (sqDistance > (sphere.Radius + Radius) * (sphere.Radius + Radius))
            {
                return ContainmentType.Disjoint;
            }
            else if (sqDistance <= (Radius - sphere.Radius) * (Radius - sphere.Radius))
            {
                return ContainmentType.Contains;
            }
            return ContainmentType.Intersects;
        }

        public ContainmentType Contains(Vector3 point)
        {
            float sqRadius = Radius * Radius;
            float sqDistance;
            FnaMath.DistanceSquared(ref point, ref Center, out sqDistance);

            if (sqDistance > sqRadius)
            {
                return ContainmentType.Disjoint;
            }
            else if (sqDistance < sqRadius)
            {
                return ContainmentType.Contains;
            }
            return ContainmentType.Intersects;
        }

        public bool Equals(BoundingSphere other)
        {
            return (    Center == other.Center &&
                    Radius == other.Radius    );
        }

        public static BoundingSphere CreateFromBoundingBox(BoundingBox box)
        {
            BoundingSphere result;
            CreateFromBoundingBox(ref box, out result);
            return result;
        }

        public static void CreateFromBoundingBox(ref BoundingBox box, out BoundingSphere result)
        {
            Vector3 center = new Vector3(
                (box.Min.X + box.Max.X) / 2.0f,
                (box.Min.Y + box.Max.Y) / 2.0f,
                (box.Min.Z + box.Max.Z) / 2.0f
            );

            float radius = FnaMath.Distance(center, box.Max);

            result = new BoundingSphere(center, radius);
        }

        public static BoundingSphere CreateFromFrustum(BoundingFrustum frustum)
        {
            return CreateFromPoints(frustum.corners);
        }

        public static BoundingSphere CreateFromPoints(IEnumerable<Vector3> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException("points");
            }

            Vector3 minx = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 maxx = -minx;
            Vector3 miny = minx;
            Vector3 maxy = -minx;
            Vector3 minz = minx;
            Vector3 maxz = -minx;

            int numPoints = 0;
            foreach (Vector3 pt in points)
            {
                numPoints += 1;

                if (pt.X < minx.X)
                {
                    minx = pt;
                }
                if (pt.X > maxx.X)
                {
                    maxx = pt;
                }
                if (pt.Y < miny.Y)
                {
                    miny = pt;
                }
                if (pt.Y > maxy.Y)
                {
                    maxy = pt;
                }
                if (pt.Z < minz.Z)
                {
                    minz = pt;
                }
                if (pt.Z > maxz.Z)
                {
                    maxz = pt;
                }
            }

            if (numPoints == 0)
            {
                throw new ArgumentException(
                    "You should have at least one point in points."
                );
            }

            float sqDistX = FnaMath.DistanceSquared(maxx, minx);
            float sqDistY = FnaMath.DistanceSquared(maxy, miny);
            float sqDistZ = FnaMath.DistanceSquared(maxz, minz);

            Vector3 min = minx;
            Vector3 max = maxx;
            if (sqDistY > sqDistX && sqDistY > sqDistZ)
            {
                max = maxy;
                min = miny;
            }
            if (sqDistZ > sqDistX && sqDistZ > sqDistY)
            {
                max = maxz;
                min = minz;
            }

            Vector3 center = (min + max) * 0.5f;
            float radius = FnaMath.Distance(max, center);

            float sqRadius = radius * radius;
            foreach (Vector3 pt in points)
            {
                Vector3 diff = (pt - center);
                float sqDist = FnaMath.LengthSquared(diff);
                if (sqDist > sqRadius)
                {
                    float distance = (float) Math.Sqrt(sqDist);
                    Vector3 direction = diff / distance;
                    Vector3 G = center - radius * direction;
                    center = (G + pt) / 2;
                    radius = FnaMath.Distance(pt, center);
                    sqRadius = radius * radius;
                }
            }

            return new BoundingSphere(center, radius);
        }

        public static BoundingSphere CreateMerged(BoundingSphere original, BoundingSphere additional)
        {
            BoundingSphere result;
            CreateMerged(ref original, ref additional, out result);
            return result;
        }

        public static void CreateMerged(
            ref BoundingSphere original,
            ref BoundingSphere additional,
            out BoundingSphere result
        ) {
            Vector3 ocenterToaCenter = FnaMath.Subtract(additional.Center, original.Center);
            float distance = FnaMath.Length(ocenterToaCenter);

            if (distance <= original.Radius + additional.Radius)
            {
                if (distance <= original.Radius - additional.Radius)
                {
                    result = original;
                    return;
                }

                if (distance <= additional.Radius - original.Radius)
                {
                    result = additional;
                    return;
                }
            }

            float leftRadius = Math.Max(original.Radius - distance, additional.Radius);
            float Rightradius = Math.Max(original.Radius + distance, additional.Radius);

            ocenterToaCenter = ocenterToaCenter +
                (
                    ((leftRadius - Rightradius) / (2 * FnaMath.Length(ocenterToaCenter)))
                    * ocenterToaCenter
                );

            result = new BoundingSphere();
            result.Center = original.Center + ocenterToaCenter;
            result.Radius = (leftRadius + Rightradius) / 2;
        }

        public bool Intersects(BoundingBox box)
        {
            return box.Intersects(this);
        }

        public void Intersects(ref BoundingBox box, out bool result)
        {
            box.Intersects(ref this, out result);
        }

        public bool Intersects(BoundingFrustum frustum)
        {
            return frustum.Intersects(this);
        }

        public bool Intersects(BoundingSphere sphere)
        {
            bool result;
            Intersects(ref sphere, out result);
            return result;
        }

        public void Intersects(ref BoundingSphere sphere, out bool result)
        {
            float sqDistance;
            FnaMath.DistanceSquared(ref sphere.Center, ref Center, out sqDistance);
            result = !(sqDistance > (sphere.Radius + Radius) * (sphere.Radius + Radius));
        }

        public float? Intersects(Ray ray)
        {
            return ray.Intersects(this);
        }

        public void Intersects(ref Ray ray, out float? result)
        {
            ray.Intersects(ref this, out result);
        }

        public PlaneIntersectionType Intersects(Plane plane)
        {
            PlaneIntersectionType result = default(PlaneIntersectionType);

            this.Intersects(ref plane, out result);
            return result;
        }

        public void Intersects(ref Plane plane, out PlaneIntersectionType result)
        {
            float distance = default(float);

            FnaMath.Dot(ref plane.Normal, ref this.Center, out distance);
            distance += plane.D;
            if (distance > this.Radius)
            {
                result = PlaneIntersectionType.Front;
            }
            else if (distance < -this.Radius)
            {
                result = PlaneIntersectionType.Back;
            }
            else
            {
                result = PlaneIntersectionType.Intersecting;
            }
        }

        public override bool Equals(object obj)
        {
            return (obj is BoundingSphere) && Equals((BoundingSphere) obj);
        }

        public override int GetHashCode()
        {
            return this.Center.GetHashCode() + this.Radius.GetHashCode();
        }

        public override string ToString()
        {
            return (
                "{Center:" + Center.ToString() +
                " Radius:" + Radius.ToString() +
                "}"
            );
        }

        public static bool operator ==(BoundingSphere a, BoundingSphere b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(BoundingSphere a, BoundingSphere b)
        {
            return !a.Equals(b);
        }
    }
}
