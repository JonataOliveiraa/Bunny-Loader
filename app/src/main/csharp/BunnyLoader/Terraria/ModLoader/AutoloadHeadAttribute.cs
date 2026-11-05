using System;

namespace Terraria.ModLoader;

[AttributeUsage(AttributeTargets.Class)]
public sealed class AutoloadHead : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public sealed class AutoloadBossHead : Attribute { }
