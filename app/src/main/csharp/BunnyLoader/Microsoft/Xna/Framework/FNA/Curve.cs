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
    public class Curve
    {
        public bool IsConstant
        {
            get
            {
                return Keys.Count <= 1;
            }
        }

        public CurveKeyCollection Keys
        {
            get;
            private set;
        }

        public CurveLoopType PostLoop
        {
            get;
            set;
        }

        public CurveLoopType PreLoop
        {
            get;
            set;
        }

        public Curve()
        {
            Keys = new CurveKeyCollection();
        }

        private Curve(CurveKeyCollection keys)
        {
            Keys = keys;
        }

        public Curve Clone()
        {
            Curve curve = new Curve(Keys.Clone());
            curve.PreLoop = PreLoop;
            curve.PostLoop = PostLoop;
            return curve;
        }

        public float Evaluate(float position)
        {
            if (Keys.Count == 0)
            {
                return 0.0f;
            }
            if (Keys.Count == 1)
            {
                return Keys[0].Value;
            }

            CurveKey first = Keys[0];
            CurveKey last = Keys[Keys.Count - 1];

            if (position < first.Position)
            {
                switch (this.PreLoop)
                {
                    case CurveLoopType.Constant:
                        return first.Value;

                    case CurveLoopType.Linear:

                        return first.Value - first.TangentIn * (first.Position - position);

                    case CurveLoopType.Cycle:

                        int cycle = GetNumberOfCycle(position);
                        float virtualPos = position - (cycle * (last.Position - first.Position));
                        return GetCurvePosition(virtualPos);

                    case CurveLoopType.CycleOffset:

                        cycle = GetNumberOfCycle(position);
                        virtualPos = position - (cycle * (last.Position - first.Position));
                        return (GetCurvePosition(virtualPos) + cycle * (last.Value - first.Value));

                    case CurveLoopType.Oscillate:

                        cycle = GetNumberOfCycle(position);

                        if (0 == cycle % 2f)
                        {
                            virtualPos = position - (cycle * (last.Position - first.Position));
                        }
                        else
                        {
                            virtualPos = last.Position - position + first.Position + (cycle * (last.Position - first.Position));
                        }
                        return GetCurvePosition(virtualPos);
                }
            }
            else if (position > last.Position)
            {
                int cycle;
                switch (this.PostLoop)
                {
                    case CurveLoopType.Constant:
                        return last.Value;

                    case CurveLoopType.Linear:

                        return last.Value + first.TangentOut * (position - last.Position);

                    case CurveLoopType.Cycle:

                        cycle = GetNumberOfCycle(position);
                        float virtualPos = position - (cycle * (last.Position - first.Position));
                        return GetCurvePosition(virtualPos);

                    case CurveLoopType.CycleOffset:

                        cycle = GetNumberOfCycle(position);
                        virtualPos = position - (cycle * (last.Position - first.Position));
                        return (GetCurvePosition(virtualPos) + cycle * (last.Value - first.Value));

                    case CurveLoopType.Oscillate:

                        cycle = GetNumberOfCycle(position);
                        virtualPos = position - (cycle * (last.Position - first.Position));

                        if (0 == cycle % 2f)
                        {
                            virtualPos = position - (cycle * (last.Position - first.Position));
                        }
                        else
                        {
                            virtualPos =
                                last.Position - position + first.Position +
                                (cycle * (last.Position - first.Position)
                            );
                        }
                        return GetCurvePosition(virtualPos);
                }
            }

            return GetCurvePosition(position);
        }

        public void ComputeTangents(CurveTangent tangentType)
        {
            ComputeTangents(tangentType, tangentType);
        }

        public void ComputeTangents(CurveTangent tangentInType, CurveTangent tangentOutType)
        {
            for (int i = 0; i < Keys.Count; i += 1)
            {
                ComputeTangent(i, tangentInType, tangentOutType);
            }
        }

        public void ComputeTangent(int keyIndex, CurveTangent tangentType)
        {
            ComputeTangent(keyIndex, tangentType, tangentType);
        }

        public void ComputeTangent(
            int keyIndex,
            CurveTangent tangentInType,
            CurveTangent tangentOutType
        ) {
            CurveKey key = Keys[keyIndex];

            float p0, p, p1;
            p0 = p = p1 = key.Position;

            float v0, v, v1;
            v0 = v = v1 = key.Value;

            if (keyIndex > 0)
            {
                p0 = Keys[keyIndex - 1].Position;
                v0 = Keys[keyIndex - 1].Value;
            }

            if (keyIndex < Keys.Count-1)
            {
                p1 = Keys[keyIndex + 1].Position;
                v1 = Keys[keyIndex + 1].Value;
            }

            switch (tangentInType)
            {
                case CurveTangent.Flat:
                    key.TangentIn = 0;
                    break;
                case CurveTangent.Linear:
                    key.TangentIn = v - v0;
                    break;
                case CurveTangent.Smooth:
                    float pn = p1 - p0;
                    if (FnaMath.WithinEpsilon(pn, 0.0f))
                    {
                        key.TangentIn = 0;
                    }
                    else
                    {
                        key.TangentIn = (v1 - v0) * ((p - p0) / pn);
                    }
                    break;
            }

            switch (tangentOutType)
            {
                case CurveTangent.Flat:
                    key.TangentOut = 0;
                    break;
                case CurveTangent.Linear:
                    key.TangentOut = v1 - v;
                    break;
                case CurveTangent.Smooth:
                    float pn = p1 - p0;
                    if (Math.Abs(pn) < float.Epsilon)
                    {
                        key.TangentOut = 0;
                    }
                    else
                    {
                        key.TangentOut = (v1 - v0) * ((p1 - p) / pn);
                    }
                    break;
            }
        }

        private int GetNumberOfCycle(float position)
        {
            float cycle = (position - Keys[0].Position) /
                (Keys[Keys.Count - 1].Position - Keys[0].Position);
            if (cycle < 0f)
            {
                cycle -= 1;
            }
            return (int) cycle;
        }

        private float GetCurvePosition(float position)
        {
            CurveKey prev = Keys[0];
            CurveKey next;
            for (int i = 1; i < Keys.Count; i += 1)
            {
                next = Keys[i];
                if (next.Position >= position)
                {
                    if (prev.Continuity == CurveContinuity.Step)
                    {
                        if (position >= 1f)
                        {
                            return next.Value;
                        }
                        return prev.Value;
                    }

                    float t = (
                        (position - prev.Position) /
                        (next.Position - prev.Position)
                    );
                    float ts = t * t;
                    float tss = ts * t;

                    return (
                        (2 * tss - 3 * ts + 1f) * prev.Value +
                        (tss - 2 * ts + t) * prev.TangentOut +
                        (3 * ts - 2 * tss) * next.Value +
                        (tss - ts) * next.TangentIn
                    );
                }
                prev = next;
            }
            return 0f;
        }
    }
}
