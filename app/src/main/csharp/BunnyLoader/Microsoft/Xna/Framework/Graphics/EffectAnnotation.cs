using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Xna.Framework.Graphics;

public enum EffectParameterClass
{
    Scalar,
    Vector,
    Matrix,
    Object,
    Struct,
}

public enum EffectParameterType
{
    Void,
    Bool,
    Int32,
    Single,
    String,
    Texture,
    Texture1D,
    Texture2D,
    Texture3D,
    TextureCube,
}

public sealed class EffectAnnotation
{
    readonly float[] values;
    readonly string? text;

    internal EffectAnnotation(string name, string? semantic, EffectParameterClass parameterClass, EffectParameterType parameterType,
                              int rowCount, int columnCount, float[] values, string? text)
    {
        Name = name;
        Semantic = semantic;
        ParameterClass = parameterClass;
        ParameterType = parameterType;
        RowCount = rowCount;
        ColumnCount = columnCount;
        this.values = values;
        this.text = text;
    }

    public string Name { get; }

    public string? Semantic { get; }

    public EffectParameterClass ParameterClass { get; }

    public EffectParameterType ParameterType { get; }

    public int RowCount { get; }

    public int ColumnCount { get; }

    public bool GetValueBoolean() => Value(0) != 0;

    public int GetValueInt32() => (int)Value(0);

    public float GetValueSingle() => Value(0);

    public string? GetValueString() => text;

    public Vector2 GetValueVector2() => new(Value(0), Value(1));

    public Vector3 GetValueVector3() => new(Value(0), Value(1), Value(2));

    public Vector4 GetValueVector4() => new(Value(0), Value(1), Value(2), Value(3));

    public Matrix GetValueMatrix() => new(
        Value(0), Value(1), Value(2), Value(3),
        Value(4), Value(5), Value(6), Value(7),
        Value(8), Value(9), Value(10), Value(11),
        Value(12), Value(13), Value(14), Value(15));

    float Value(int index) => index < values.Length ? values[index] : 0f;

    internal static EffectParameterClass ClassOf(int mojoClass) => mojoClass switch
    {
        0 => EffectParameterClass.Scalar,
        1 => EffectParameterClass.Vector,
        2 or 3 => EffectParameterClass.Matrix,
        5 => EffectParameterClass.Struct,
        _ => EffectParameterClass.Object,
    };

    internal static EffectParameterType TypeOf(int mojoType) => mojoType switch
    {
        1 => EffectParameterType.Bool,
        2 => EffectParameterType.Int32,
        3 => EffectParameterType.Single,
        4 => EffectParameterType.String,
        5 or 10 => EffectParameterType.Texture,
        6 or 11 => EffectParameterType.Texture1D,
        7 or 12 => EffectParameterType.Texture2D,
        8 or 13 => EffectParameterType.Texture3D,
        9 or 14 => EffectParameterType.TextureCube,
        _ => EffectParameterType.Void,
    };
}

public sealed class EffectAnnotationCollection : IEnumerable<EffectAnnotation>
{
    internal static readonly EffectAnnotationCollection Empty = new(new List<EffectAnnotation>());

    readonly List<EffectAnnotation> annotations;

    internal EffectAnnotationCollection(List<EffectAnnotation> annotations) => this.annotations = annotations;

    public int Count => annotations.Count;

    public EffectAnnotation this[int index] => annotations[index];

    public EffectAnnotation? this[string name]
    {
        get
        {
            foreach (EffectAnnotation annotation in annotations)
                if (annotation.Name == name) return annotation;
            return null;
        }
    }

    public List<EffectAnnotation>.Enumerator GetEnumerator() => annotations.GetEnumerator();

    IEnumerator<EffectAnnotation> IEnumerable<EffectAnnotation>.GetEnumerator() => annotations.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => annotations.GetEnumerator();
}
