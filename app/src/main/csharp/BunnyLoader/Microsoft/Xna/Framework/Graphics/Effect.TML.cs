namespace Microsoft.Xna.Framework.Graphics;

public readonly unsafe partial struct EffectPassCollection
{
    public EffectPass this[int index] => _passes[index];

    public EffectPass this[string name]
    {
        get
        {
            foreach (EffectPass pass in _passes)
                if (!pass.IsNull && pass.Name == name) return pass;
            return default;
        }
    }
}

public readonly unsafe partial struct EffectParameterCollection
{
    public EffectParameter GetParameterBySemantic(string semantic)
    {
        foreach (EffectParameter parameter in _parameters)
            if (!parameter.IsNull && parameter.Semantic == semantic) return parameter;
        return default;
    }

    public EffectParameter this[int index] => _parameters[index];

    public EffectParameter this[string name]
    {
        get
        {
            foreach (EffectParameter parameter in _parameters)
                if (!parameter.IsNull && parameter.Name == name) return parameter;
            return default;
        }
    }
}

public readonly unsafe partial struct EffectTechniqueCollection
{
    public EffectTechnique this[int index] => _techniques[index];

    public EffectTechnique this[string name]
    {
        get
        {
            foreach (EffectTechnique technique in _techniques)
                if (!technique.IsNull && technique.Name == name) return technique;
            return default;
        }
    }
}

public readonly unsafe partial struct Effect
{
    public Effect(GraphicsDevice graphicsDevice, byte[] effectCode)
        : this(BunnyLoader.FxcGameEffect.Create(BunnyLoader.FxcEffect.FromBytes(effectCode)).Pointer)
    {
    }

    public Effect(Effect cloneSource) : this(BunnyLoader.FxcGameEffect.Clone(cloneSource).Pointer)
    {
    }

    public EffectTechniqueCollection Techniques => BunnyLoader.FxcGameEffect.TechniquesOf(this);

    public Effect Clone() => BunnyLoader.FxcGameEffect.SourceOf(this) != null ? BunnyLoader.FxcGameEffect.Clone(this) : Clone_();

    public void Clone(Effect cloneSource) => Clone_(cloneSource);
}

public readonly unsafe partial struct EffectPass
{
    public EffectAnnotationCollection Annotations => BunnyLoader.FxcGameEffect.AnnotationsOf(Pointer);
}

public readonly unsafe partial struct EffectTechnique
{
    public EffectAnnotationCollection Annotations => BunnyLoader.FxcGameEffect.AnnotationsOf(Pointer);
}

public readonly unsafe partial struct EffectParameter
{
    static readonly System.Collections.Generic.Dictionary<nint, Matrix[]> matrices = new();

    public EffectParameterClass ParameterClass => BunnyLoader.FxcGameEffect.ParameterInfo(this)?.ParameterClass ?? EffectParameterClass.Scalar;

    public EffectParameterType ParameterType => BunnyLoader.FxcGameEffect.ParameterInfo(this)?.ParameterType ?? EffectParameterType.Void;

    public int RowCount => BunnyLoader.FxcGameEffect.ParameterInfo(this)?.Rows ?? 0;

    public int ColumnCount => BunnyLoader.FxcGameEffect.ParameterInfo(this)?.Columns ?? 0;

    public string? Semantic => BunnyLoader.FxcGameEffect.ParameterInfo(this)?.Semantic;

    public EffectAnnotationCollection Annotations => BunnyLoader.FxcGameEffect.AnnotationsOf(Pointer);

    public EffectParameterCollection Elements
    {
        get
        {
            int count = BunnyLoader.FxcGameEffect.ParameterInfo(this)?.Elements ?? 0;
            if (count > 0) throw new System.NotSupportedException($"EffectParameter {Name}: Elements of an array parameter are not supported; set the whole array with SetValue");
            return default;
        }
    }

    internal void CopyValueFrom(EffectParameter source)
    {
        BunnyLoader.Interop.GameArray<float> data = source.Data;

        if (!data.IsNull)
        {
            var copy = BunnyLoader.Interop.GameArray<float>.New(new BunnyLoader.Interop.GameClass("System", "Single"), data.Length);
            data.AsSpan().CopyTo(copy.AsSpan());
            Data = copy;
        }

        lock (matrices)
            if (matrices.TryGetValue(source.Pointer, out Matrix[]? kept)) matrices[Pointer] = (Matrix[])kept.Clone();
    }

    public void SetValue(int value) => SetValue((float)value);

    public void SetValue(Texture2D value) => BunnyLoader.FxcGameEffect.SetTexture(this, value);

    public void SetValue(Texture value) => SetValue((Texture2D)value);

    public void SetValue(bool value) => Write(value ? 1f : 0f, 0, 0, 0, 1);

    public void SetValue(float value) => Write(value, 0, 0, 0, 1);

    public void SetValue(Vector2 value) => Write(value.X, value.Y, 0, 0, 2);

    public void SetValue(Vector3 value) => Write(value.X, value.Y, value.Z, 0, 3);

    public void SetValue(Vector4 value) => Write(value.X, value.Y, value.Z, value.W, 4);

    public void SetValue(Quaternion value) => Write(value.X, value.Y, value.Z, value.W, 4);

    public void SetValue(Matrix value)
    {
        if (Pointer == 0) throw new System.NullReferenceException("EffectParameter is null");

        lock (matrices)
        {
            if (matrices.TryGetValue(Pointer, out Matrix[]? kept) && kept.Length == 1) kept[0] = value;
            else matrices[Pointer] = new[] { value };
        }
    }

    public void SetValueTranspose(Matrix value) => SetValue(Matrix.Transpose(value));

    public void SetValueTranspose(Matrix[] value) => SetValue(System.Array.ConvertAll(value, Matrix.Transpose));

    public void SetValue(Matrix[] value)
    {
        if (value == null) throw new System.ArgumentNullException(nameof(value));
        lock (matrices) matrices[Pointer] = (Matrix[])value.Clone();
    }

    public void SetValue(float[] value) => WriteAll(value);

    public void SetValue(int[] value) => WriteAll(System.Array.ConvertAll(value, v => (float)v));

    public void SetValue(bool[] value) => WriteAll(System.Array.ConvertAll(value, v => v ? 1f : 0f));

    public void SetValue(Vector2[] value) => WriteAll(Flatten(value, v => new[] { v.X, v.Y }));

    public void SetValue(Vector3[] value) => WriteAll(Flatten(value, v => new[] { v.X, v.Y, v.Z }));

    public void SetValue(Vector4[] value) => WriteAll(Flatten(value, v => new[] { v.X, v.Y, v.Z, v.W }));

    public void SetValue(Quaternion[] value) => WriteAll(Flatten(value, v => new[] { v.X, v.Y, v.Z, v.W }));

    public void SetValue(string value) => throw new System.NotSupportedException($"EffectParameter {Name}: string parameters are not supported");

    public bool GetValueBoolean() => Read(0) != 0;

    public int GetValueInt32() => (int)Read(0);

    public float GetValueSingle() => Read(0);

    public Vector2 GetValueVector2() => new(Read(0), Read(1));

    public Vector3 GetValueVector3() => new(Read(0), Read(1), Read(2));

    public Vector4 GetValueVector4() => new(Read(0), Read(1), Read(2), Read(3));

    public Quaternion GetValueQuaternion() => new() { X = Read(0), Y = Read(1), Z = Read(2), W = Read(3) };

    public Matrix GetValueMatrix()
    {
        lock (matrices) return matrices.TryGetValue(Pointer, out Matrix[]? m) && m.Length > 0 ? m[0] : Matrix.Identity;
    }

    public Matrix GetValueMatrixTranspose() => Matrix.Transpose(GetValueMatrix());

    public Matrix[] GetValueMatrixArray(int count)
    {
        lock (matrices) return matrices.TryGetValue(Pointer, out Matrix[]? m) ? m[..System.Math.Min(count, m.Length)] : new Matrix[0];
    }

    public Texture2D GetValueTexture2D() => throw new System.NotSupportedException($"EffectParameter {Name}: textures cannot be read back");

    internal bool TryGetMatrix(out Matrix value)
    {
        lock (matrices)
        {
            if (matrices.TryGetValue(Pointer, out Matrix[]? m) && m.Length > 0)
            {
                value = m[0];
                return true;
            }
        }

        value = Matrix.Identity;
        return false;
    }

    static float[] Flatten<T>(T[] values, System.Func<T, float[]> parts)
    {
        if (values == null) throw new System.ArgumentNullException(nameof(values));
        var all = new System.Collections.Generic.List<float>();
        foreach (T v in values) all.AddRange(parts(v));
        return all.ToArray();
    }

    float Read(int index)
    {
        if (Pointer == 0) throw new System.NullReferenceException("EffectParameter is null");
        BunnyLoader.Interop.GameArray<float> data = Data;
        return data.IsNull || index >= data.Length ? 0f : data[index];
    }

    System.Span<float> Slots()
    {
        if (Pointer == 0) throw new System.NullReferenceException("EffectParameter is null");
        BunnyLoader.Interop.GameArray<float> data = Data;

        if (data.IsNull || data.Length < 4)
        {
            data = BunnyLoader.Interop.GameArray<float>.New(new BunnyLoader.Interop.GameClass("System", "Single"), 4);
            Data = data;
        }

        return data.AsSpan();
    }

    void Write(float x, float y, float z, float w, int count)
    {
        System.Span<float> span = Slots();
        span[0] = x;
        if (count > 1) span[1] = y;
        if (count > 2) span[2] = z;
        if (count > 3) span[3] = w;
    }

    void WriteAll(float[] values)
    {
        if (values == null) throw new System.ArgumentNullException(nameof(values));
        System.Span<float> span = Slots();
        int count = System.Math.Min(values.Length, span.Length);
        new System.Span<float>(values, 0, count).CopyTo(span);
    }
}
