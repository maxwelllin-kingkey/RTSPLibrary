using System;
using System.Collections.Generic;
using System.Linq;

public static partial class LinqExtModule
{
    public static T[] InternalBuffer<T>(this List<T> TList)
    {
        return TList.ToArray();
    }

    public static TValue[] ValuesToArray<TKey, TValue>(this Dictionary<TKey, TValue> TDict)
    {
        TValue[] RetValue = null;

        lock (TDict)
            RetValue = TDict.Values.ToArray();

        return RetValue;
    }

    public static T[] ToArrayWithLock<T>(this List<T> TList)
    {
        T[] RetValue = null;

        lock (TList)
            RetValue = TList.ToArray();

        return RetValue;
    }
}