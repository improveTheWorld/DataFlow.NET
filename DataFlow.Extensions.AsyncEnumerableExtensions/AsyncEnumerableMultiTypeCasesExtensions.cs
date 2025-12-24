namespace DataFlow.Extensions;

/// <summary>
/// Multi-type SelectCase extensions for IAsyncEnumerable.
/// Supports different return types per branch (R1, R2, ... up to R10).
/// </summary>
public static class AsyncEnumerableMultiTypeCasesExtensions
{
    #region SelectCase Multi-Type (2-10 types)

    // ===================== 2 Types =====================
    public static IAsyncEnumerable<(int category, T item, (R1?, R2?) result)>
        SelectCase<T, R1, R2>(
            this IAsyncEnumerable<(int category, T item)> items,
            Func<T, R1> selector1,
            Func<T, R2> selector2)
        => items.Select(x => (
            x.category,
            x.item,
            x.category switch
            {
                0 => (selector1(x.item), default(R2)),
                1 => (default(R1), selector2(x.item)),
                _ => (default(R1), default(R2))
            }
        ));

    // ===================== 3 Types =====================
    public static IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?) result)>
        SelectCase<T, R1, R2, R3>(
            this IAsyncEnumerable<(int category, T item)> items,
            Func<T, R1> selector1,
            Func<T, R2> selector2,
            Func<T, R3> selector3)
        => items.Select(x => (
            x.category,
            x.item,
            x.category switch
            {
                0 => (selector1(x.item), default(R2), default(R3)),
                1 => (default(R1), selector2(x.item), default(R3)),
                2 => (default(R1), default(R2), selector3(x.item)),
                _ => (default(R1), default(R2), default(R3))
            }
        ));

    // ===================== 4 Types =====================
    public static IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?) result)>
        SelectCase<T, R1, R2, R3, R4>(
            this IAsyncEnumerable<(int category, T item)> items,
            Func<T, R1> selector1,
            Func<T, R2> selector2,
            Func<T, R3> selector3,
            Func<T, R4> selector4)
        => items.Select(x => (
            x.category,
            x.item,
            x.category switch
            {
                0 => (selector1(x.item), default(R2), default(R3), default(R4)),
                1 => (default(R1), selector2(x.item), default(R3), default(R4)),
                2 => (default(R1), default(R2), selector3(x.item), default(R4)),
                3 => (default(R1), default(R2), default(R3), selector4(x.item)),
                _ => (default(R1), default(R2), default(R3), default(R4))
            }
        ));

    // ===================== 5 Types =====================
    public static IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?, R5?) result)>
        SelectCase<T, R1, R2, R3, R4, R5>(
            this IAsyncEnumerable<(int category, T item)> items,
            Func<T, R1> selector1,
            Func<T, R2> selector2,
            Func<T, R3> selector3,
            Func<T, R4> selector4,
            Func<T, R5> selector5)
        => items.Select(x => (
            x.category,
            x.item,
            x.category switch
            {
                0 => (selector1(x.item), default(R2), default(R3), default(R4), default(R5)),
                1 => (default(R1), selector2(x.item), default(R3), default(R4), default(R5)),
                2 => (default(R1), default(R2), selector3(x.item), default(R4), default(R5)),
                3 => (default(R1), default(R2), default(R3), selector4(x.item), default(R5)),
                4 => (default(R1), default(R2), default(R3), default(R4), selector5(x.item)),
                _ => (default(R1), default(R2), default(R3), default(R4), default(R5))
            }
        ));

    // ===================== 6 Types =====================
    public static IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?, R5?, R6?) result)>
        SelectCase<T, R1, R2, R3, R4, R5, R6>(
            this IAsyncEnumerable<(int category, T item)> items,
            Func<T, R1> selector1,
            Func<T, R2> selector2,
            Func<T, R3> selector3,
            Func<T, R4> selector4,
            Func<T, R5> selector5,
            Func<T, R6> selector6)
        => items.Select(x => (
            x.category,
            x.item,
            x.category switch
            {
                0 => (selector1(x.item), default(R2), default(R3), default(R4), default(R5), default(R6)),
                1 => (default(R1), selector2(x.item), default(R3), default(R4), default(R5), default(R6)),
                2 => (default(R1), default(R2), selector3(x.item), default(R4), default(R5), default(R6)),
                3 => (default(R1), default(R2), default(R3), selector4(x.item), default(R5), default(R6)),
                4 => (default(R1), default(R2), default(R3), default(R4), selector5(x.item), default(R6)),
                5 => (default(R1), default(R2), default(R3), default(R4), default(R5), selector6(x.item)),
                _ => (default(R1), default(R2), default(R3), default(R4), default(R5), default(R6))
            }
        ));

    // ===================== 7 Types =====================
    public static IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?, R5?, R6?, R7?) result)>
        SelectCase<T, R1, R2, R3, R4, R5, R6, R7>(
            this IAsyncEnumerable<(int category, T item)> items,
            Func<T, R1> selector1,
            Func<T, R2> selector2,
            Func<T, R3> selector3,
            Func<T, R4> selector4,
            Func<T, R5> selector5,
            Func<T, R6> selector6,
            Func<T, R7> selector7)
        => items.Select(x => (
            x.category,
            x.item,
            x.category switch
            {
                0 => (selector1(x.item), default(R2), default(R3), default(R4), default(R5), default(R6), default(R7)),
                1 => (default(R1), selector2(x.item), default(R3), default(R4), default(R5), default(R6), default(R7)),
                2 => (default(R1), default(R2), selector3(x.item), default(R4), default(R5), default(R6), default(R7)),
                3 => (default(R1), default(R2), default(R3), selector4(x.item), default(R5), default(R6), default(R7)),
                4 => (default(R1), default(R2), default(R3), default(R4), selector5(x.item), default(R6), default(R7)),
                5 => (default(R1), default(R2), default(R3), default(R4), default(R5), selector6(x.item), default(R7)),
                6 => (default(R1), default(R2), default(R3), default(R4), default(R5), default(R6), selector7(x.item)),
                _ => (default(R1), default(R2), default(R3), default(R4), default(R5), default(R6), default(R7))
            }
        ));

    #endregion

    #region ForEachCase Multi-Type (2-7 types)

    // ===================== 2 Types =====================
    public static IAsyncEnumerable<(int category, T item, (R1?, R2?) result)>
        ForEachCase<T, R1, R2>(
            this IAsyncEnumerable<(int category, T item, (R1?, R2?) result)> items,
            Action<R1> action1,
            Action<R2> action2)
        => items.ForEach(x =>
        {
            switch (x.category)
            {
                case 0: if (x.result.Item1 is not null) action1(x.result.Item1); break;
                case 1: if (x.result.Item2 is not null) action2(x.result.Item2); break;
            }
        });

    // ===================== 3 Types =====================
    public static IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?) result)>
        ForEachCase<T, R1, R2, R3>(
            this IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?) result)> items,
            Action<R1> action1,
            Action<R2> action2,
            Action<R3> action3)
        => items.ForEach(x =>
        {
            switch (x.category)
            {
                case 0: if (x.result.Item1 is not null) action1(x.result.Item1); break;
                case 1: if (x.result.Item2 is not null) action2(x.result.Item2); break;
                case 2: if (x.result.Item3 is not null) action3(x.result.Item3); break;
            }
        });

    // ===================== 4 Types =====================
    public static IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?) result)>
        ForEachCase<T, R1, R2, R3, R4>(
            this IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?) result)> items,
            Action<R1> action1,
            Action<R2> action2,
            Action<R3> action3,
            Action<R4> action4)
        => items.ForEach(x =>
        {
            switch (x.category)
            {
                case 0: if (x.result.Item1 is not null) action1(x.result.Item1); break;
                case 1: if (x.result.Item2 is not null) action2(x.result.Item2); break;
                case 2: if (x.result.Item3 is not null) action3(x.result.Item3); break;
                case 3: if (x.result.Item4 is not null) action4(x.result.Item4); break;
            }
        });

    // ===================== 5 Types =====================
    public static IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?, R5?) result)>
        ForEachCase<T, R1, R2, R3, R4, R5>(
            this IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?, R5?) result)> items,
            Action<R1> action1,
            Action<R2> action2,
            Action<R3> action3,
            Action<R4> action4,
            Action<R5> action5)
        => items.ForEach(x =>
        {
            switch (x.category)
            {
                case 0: if (x.result.Item1 is not null) action1(x.result.Item1); break;
                case 1: if (x.result.Item2 is not null) action2(x.result.Item2); break;
                case 2: if (x.result.Item3 is not null) action3(x.result.Item3); break;
                case 3: if (x.result.Item4 is not null) action4(x.result.Item4); break;
                case 4: if (x.result.Item5 is not null) action5(x.result.Item5); break;
            }
        });

    // ===================== 6 Types =====================
    public static IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?, R5?, R6?) result)>
        ForEachCase<T, R1, R2, R3, R4, R5, R6>(
            this IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?, R5?, R6?) result)> items,
            Action<R1> action1,
            Action<R2> action2,
            Action<R3> action3,
            Action<R4> action4,
            Action<R5> action5,
            Action<R6> action6)
        => items.ForEach(x =>
        {
            switch (x.category)
            {
                case 0: if (x.result.Item1 is not null) action1(x.result.Item1); break;
                case 1: if (x.result.Item2 is not null) action2(x.result.Item2); break;
                case 2: if (x.result.Item3 is not null) action3(x.result.Item3); break;
                case 3: if (x.result.Item4 is not null) action4(x.result.Item4); break;
                case 4: if (x.result.Item5 is not null) action5(x.result.Item5); break;
                case 5: if (x.result.Item6 is not null) action6(x.result.Item6); break;
            }
        });

    // ===================== 7 Types =====================
    public static IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?, R5?, R6?, R7?) result)>
        ForEachCase<T, R1, R2, R3, R4, R5, R6, R7>(
            this IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?, R5?, R6?, R7?) result)> items,
            Action<R1> action1,
            Action<R2> action2,
            Action<R3> action3,
            Action<R4> action4,
            Action<R5> action5,
            Action<R6> action6,
            Action<R7> action7)
        => items.ForEach(x =>
        {
            switch (x.category)
            {
                case 0: if (x.result.Item1 is not null) action1(x.result.Item1); break;
                case 1: if (x.result.Item2 is not null) action2(x.result.Item2); break;
                case 2: if (x.result.Item3 is not null) action3(x.result.Item3); break;
                case 3: if (x.result.Item4 is not null) action4(x.result.Item4); break;
                case 4: if (x.result.Item5 is not null) action5(x.result.Item5); break;
                case 5: if (x.result.Item6 is not null) action6(x.result.Item6); break;
                case 6: if (x.result.Item7 is not null) action7(x.result.Item7); break;
            }
        });

    #endregion

    #region UnCase Multi-Type (2-7 types)

    public static IAsyncEnumerable<T> UnCase<T, R1, R2>(
        this IAsyncEnumerable<(int category, T item, (R1?, R2?) result)> items)
        => items.Select(x => x.item);

    public static IAsyncEnumerable<T> UnCase<T, R1, R2, R3>(
        this IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?) result)> items)
        => items.Select(x => x.item);

    public static IAsyncEnumerable<T> UnCase<T, R1, R2, R3, R4>(
        this IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?) result)> items)
        => items.Select(x => x.item);

    public static IAsyncEnumerable<T> UnCase<T, R1, R2, R3, R4, R5>(
        this IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?, R5?) result)> items)
        => items.Select(x => x.item);

    public static IAsyncEnumerable<T> UnCase<T, R1, R2, R3, R4, R5, R6>(
        this IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?, R5?, R6?) result)> items)
        => items.Select(x => x.item);

    public static IAsyncEnumerable<T> UnCase<T, R1, R2, R3, R4, R5, R6, R7>(
        this IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?, R5?, R6?, R7?) result)> items)
        => items.Select(x => x.item);

    #endregion

    #region AllCases Multi-Type (2-7 types) - Returns object?

    public static IAsyncEnumerable<object?> AllCases<T, R1, R2>(
        this IAsyncEnumerable<(int category, T item, (R1?, R2?) result)> items)
        => items.Select(x => x.category switch
        {
            0 => (object?)x.result.Item1,
            1 => (object?)x.result.Item2,
            _ => null
        }).Where(x => x is not null);

    public static IAsyncEnumerable<object?> AllCases<T, R1, R2, R3>(
        this IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?) result)> items)
        => items.Select(x => x.category switch
        {
            0 => (object?)x.result.Item1,
            1 => (object?)x.result.Item2,
            2 => (object?)x.result.Item3,
            _ => null
        }).Where(x => x is not null);

    public static IAsyncEnumerable<object?> AllCases<T, R1, R2, R3, R4>(
        this IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?) result)> items)
        => items.Select(x => x.category switch
        {
            0 => (object?)x.result.Item1,
            1 => (object?)x.result.Item2,
            2 => (object?)x.result.Item3,
            3 => (object?)x.result.Item4,
            _ => null
        }).Where(x => x is not null);

    public static IAsyncEnumerable<object?> AllCases<T, R1, R2, R3, R4, R5>(
        this IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?, R5?) result)> items)
        => items.Select(x => x.category switch
        {
            0 => (object?)x.result.Item1,
            1 => (object?)x.result.Item2,
            2 => (object?)x.result.Item3,
            3 => (object?)x.result.Item4,
            4 => (object?)x.result.Item5,
            _ => null
        }).Where(x => x is not null);

    public static IAsyncEnumerable<object?> AllCases<T, R1, R2, R3, R4, R5, R6>(
        this IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?, R5?, R6?) result)> items)
        => items.Select(x => x.category switch
        {
            0 => (object?)x.result.Item1,
            1 => (object?)x.result.Item2,
            2 => (object?)x.result.Item3,
            3 => (object?)x.result.Item4,
            4 => (object?)x.result.Item5,
            5 => (object?)x.result.Item6,
            _ => null
        }).Where(x => x is not null);

    public static IAsyncEnumerable<object?> AllCases<T, R1, R2, R3, R4, R5, R6, R7>(
        this IAsyncEnumerable<(int category, T item, (R1?, R2?, R3?, R4?, R5?, R6?, R7?) result)> items)
        => items.Select(x => x.category switch
        {
            0 => (object?)x.result.Item1,
            1 => (object?)x.result.Item2,
            2 => (object?)x.result.Item3,
            3 => (object?)x.result.Item4,
            4 => (object?)x.result.Item5,
            5 => (object?)x.result.Item6,
            6 => (object?)x.result.Item7,
            _ => null
        }).Where(x => x is not null);

    #endregion
}
