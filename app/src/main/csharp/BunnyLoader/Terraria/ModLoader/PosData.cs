using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Terraria.ModLoader;

public struct PosData<T>
{
    public class OrderedSparseLookupBuilder
    {
        private readonly List<PosData<T>> list;
        private readonly bool compressEqualValues;
        private readonly bool insertDefaultEntries;
        private PosData<T> last;

        public OrderedSparseLookupBuilder(int capacity = 1048576, bool compressEqualValues = true, bool insertDefaultEntries = false)
        {
            list = new List<PosData<T>>(capacity);
            last = nullPosData;
            this.compressEqualValues = compressEqualValues;
            this.insertDefaultEntries = insertDefaultEntries;
        }

        public void Add(int x, int y, T value) => Add(PosData.CoordsToPos(x, y), value);

        public void Add(int pos, T value)
        {
            if (pos <= last.pos)
                throw new ArgumentException($"Must build in ascending index order. Prev: {last.pos}, pos: {pos}");
            if (compressEqualValues)
            {
                if (insertDefaultEntries && pos >= last.pos + 2)
                    Add(last.pos + 1, default!);
                if (EqualityComparer<T>.Default.Equals(value, last.value))
                    return;
            }

            list.Add(last = new PosData<T>(pos, value));
        }

        public PosData<T>[] Build() => list.ToArray();
    }

    public class OrderedSparseLookupReader
    {
        private readonly PosData<T>[] data;
        private readonly bool hasEqualValueCompression;
        private PosData<T> current;
        private int nextIdx;

        public OrderedSparseLookupReader(PosData<T>[] data, bool hasEqualValueCompression = true)
        {
            this.data = data;
            this.hasEqualValueCompression = hasEqualValueCompression;
            current = nullPosData;
            nextIdx = 0;
        }

        public T Get(int x, int y) => Get(PosData.CoordsToPos(x, y));

        public T Get(int pos)
        {
            if (pos <= current.pos)
                throw new ArgumentException($"Must read in ascending index order. Prev: {current.pos}, pos: {pos}");
            while (nextIdx < data.Length && data[nextIdx].pos <= pos)
                current = data[nextIdx++];
            if (!hasEqualValueCompression && current.pos != pos)
                throw new KeyNotFoundException($"Position does not exist in map. {pos} (X: {current.X}, Y: {current.Y})");
            return current.value;
        }
    }

    public readonly int pos;

    public T value;

    public int X => pos / Main.maxTilesY;

    public int Y => pos % Main.maxTilesY;

    public PosData(int pos, T value)
    {
        this.pos = pos;
        this.value = value;
    }

    public PosData(int x, int y, T value) : this(PosData.CoordsToPos(x, y), value)
    {
    }

    public static PosData<T> nullPosData = new PosData<T>(-1, default!);
}

public static class PosData
{
    public static int CoordsToPos(int x, int y) => x * Main.maxTilesY + y;

    public static int FindIndex<T>(this PosData<T>[] posMap, int x, int y) => posMap.FindIndex(CoordsToPos(x, y));

    public static int FindIndex<T>(this PosData<T>[] posMap, int pos)
    {
        int minimum = -1, maximum = posMap.Length;
        while (maximum - minimum > 1)
        {
            int split = (minimum + maximum) / 2;
            if (posMap[split].pos <= pos)
                minimum = split;
            else
                maximum = split;
        }

        return minimum;
    }

    public static PosData<T> Find<T>(this PosData<T>[] posMap, int pos)
    {
        int index = posMap.FindIndex(pos);
        return index < 0 ? PosData<T>.nullPosData : posMap[index];
    }

    public static T Lookup<T>(this PosData<T>[] posMap, int x, int y) => posMap.Lookup(CoordsToPos(x, y));

    public static T Lookup<T>(this PosData<T>[] posMap, int pos) => posMap.Find(pos).value;

    public static bool LookupExact<T>(this PosData<T>[] posMap, int x, int y, out T data) => posMap.LookupExact(CoordsToPos(x, y), out data);

    public static bool LookupExact<T>(this PosData<T>[] posMap, int pos, out T data)
    {
        PosData<T> posData = posMap.Find(pos);
        if (posData.pos != pos)
        {
            data = default!;
            return false;
        }

        data = posData.value;
        return true;
    }

    public static bool NearbySearchOrderedPosMap<T>(PosData<T>[] posMap, Point pt, int distance, out PosData<T> entry)
    {
        entry = new PosData<T>(-1, default!);
        if (posMap.Length == 0)
            return false;
        int minPos = CoordsToPos(Math.Max(pt.X - distance, 0), Math.Max(pt.Y - distance, 0));
        int maxPos = CoordsToPos(Math.Min(pt.X + distance, Main.maxTilesX - 1), Math.Min(pt.Y + distance, Main.maxTilesY - 1));
        if (posMap[0].pos > maxPos || posMap[posMap.Length - 1].pos < minPos)
            return false;
        int minimum = Math.Max(posMap.FindIndex(minPos), 0);
        int maximum = Math.Max(posMap.FindIndex(maxPos), 0);
        int bestSqDist = distance * distance + 1;
        for (int i = minimum; i < maximum; i++)
        {
            PosData<T> posData = posMap[i];
            int dy = posData.Y - pt.Y;
            if (dy < -distance || dy > distance)
                continue;
            int dx = posData.X - pt.X;
            int sqDist = dx * dx + dy * dy;
            if (sqDist < bestSqDist)
            {
                bestSqDist = sqDist;
                entry = posData;
            }
        }

        return entry.pos >= 0;
    }
}
