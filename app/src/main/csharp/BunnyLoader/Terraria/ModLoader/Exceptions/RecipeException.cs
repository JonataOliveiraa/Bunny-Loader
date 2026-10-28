using System;

namespace Terraria.ModLoader.Exceptions;

public class RecipeException : Exception
{
    public RecipeException(string message) : base(message) { }
}
