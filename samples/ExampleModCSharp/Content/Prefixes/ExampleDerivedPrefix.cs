namespace ExampleMod.Content.Prefixes;

public class ExampleDerivedPrefix : ExamplePrefix
{
    public override float Power => base.Power * 2f;
}
