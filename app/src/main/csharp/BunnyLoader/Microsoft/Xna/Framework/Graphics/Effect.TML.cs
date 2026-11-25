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
