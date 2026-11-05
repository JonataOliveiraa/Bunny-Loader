using System;
using System.Collections.Generic;

namespace Terraria.Utilities;

public class WeightedRandom<T>
{
    public readonly List<Tuple<T, double>> elements = new();
    double totalWeight;
    bool mustRecalculateTotalWeight;

    public bool needsRefresh = true;

    public void Add(T element, double weight = 1.0)
    {
        elements.Add(new Tuple<T, double>(element, weight));
        mustRecalculateTotalWeight = true;
    }

    public T Get()
    {
        if (mustRecalculateTotalWeight)
        {
            totalWeight = 0.0;
            foreach (Tuple<T, double> element in elements) totalWeight += element.Item2;
            mustRecalculateTotalWeight = false;
        }
        double chosen = Main.rand.NextDouble() * totalWeight;
        double current = 0.0;
        foreach (Tuple<T, double> element in elements)
        {
            current += element.Item2;
            if (chosen <= current) return element.Item1;
        }
        return default!;
    }

    public void Clear()
    {
        elements.Clear();
        totalWeight = 0.0;
    }

    public static implicit operator T(WeightedRandom<T> weightedRandom) => weightedRandom.Get();
}
