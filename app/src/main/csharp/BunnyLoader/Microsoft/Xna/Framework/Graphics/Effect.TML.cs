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
    public EffectTechniqueCollection Techniques => BunnyLoader.FxcGameEffect.TechniquesOf(this);
}

public readonly unsafe partial struct EffectParameter
{
    public void SetValue(int value) => SetValue((float)value);

    public void SetValue(Texture2D value) => BunnyLoader.FxcGameEffect.SetTexture(this, value);
}
