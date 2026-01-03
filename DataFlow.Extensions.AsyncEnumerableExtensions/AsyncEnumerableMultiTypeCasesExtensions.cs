namespace DataFlow;

/// <summary>
/// Multi-type SelectCases extensions for IAsyncEnumerable.
/// Supports different return types per branch (R1, R2, ... up to R7).
/// Uses FLAT tuple return types to distinguish from single-type versions.
/// </summary>
public static class AsyncEnumerableMultiTypeCasesExtensions
{
    #region SelectCases Multi-Type (2-7 types)

    // ===================== 2 Types =====================
    public static IAsyncEnumerable<(int category, T item, R1? result1, R2? result2)>
        SelectCases<T, R1, R2>(
            this IAsyncEnumerable<(int category, T item)> items,
            Func<T, R1> selector1,
            Func<T, R2> selector2)
        => items.Select(x => (
            x.category,
            x.item,
            x.category == 0 ? selector1(x.item) : default(R1),
            x.category == 1 ? selector2(x.item) : default(R2)
        ));

    // ===================== 3 Types =====================
    public static IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3)>
        SelectCases<T, R1, R2, R3>(
            this IAsyncEnumerable<(int category, T item)> items,
            Func<T, R1> selector1,
            Func<T, R2> selector2,
            Func<T, R3> selector3)
        => items.Select(x => (
            x.category,
            x.item,
            x.category == 0 ? selector1(x.item) : default(R1),
            x.category == 1 ? selector2(x.item) : default(R2),
            x.category == 2 ? selector3(x.item) : default(R3)
        ));

    // ===================== 4 Types =====================
    public static IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4)>
        SelectCases<T, R1, R2, R3, R4>(
            this IAsyncEnumerable<(int category, T item)> items,
            Func<T, R1> selector1,
            Func<T, R2> selector2,
            Func<T, R3> selector3,
            Func<T, R4> selector4)
        => items.Select(x => (
            x.category,
            x.item,
            x.category == 0 ? selector1(x.item) : default(R1),
            x.category == 1 ? selector2(x.item) : default(R2),
            x.category == 2 ? selector3(x.item) : default(R3),
            x.category == 3 ? selector4(x.item) : default(R4)
        ));

    // ===================== 5 Types =====================
    public static IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4, R5? result5)>
        SelectCases<T, R1, R2, R3, R4, R5>(
            this IAsyncEnumerable<(int category, T item)> items,
            Func<T, R1> selector1,
            Func<T, R2> selector2,
            Func<T, R3> selector3,
            Func<T, R4> selector4,
            Func<T, R5> selector5)
        => items.Select(x => (
            x.category,
            x.item,
            x.category == 0 ? selector1(x.item) : default(R1),
            x.category == 1 ? selector2(x.item) : default(R2),
            x.category == 2 ? selector3(x.item) : default(R3),
            x.category == 3 ? selector4(x.item) : default(R4),
            x.category == 4 ? selector5(x.item) : default(R5)
        ));

    // ===================== 6 Types =====================
    public static IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4, R5? result5, R6? result6)>
        SelectCases<T, R1, R2, R3, R4, R5, R6>(
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
            x.category == 0 ? selector1(x.item) : default(R1),
            x.category == 1 ? selector2(x.item) : default(R2),
            x.category == 2 ? selector3(x.item) : default(R3),
            x.category == 3 ? selector4(x.item) : default(R4),
            x.category == 4 ? selector5(x.item) : default(R5),
            x.category == 5 ? selector6(x.item) : default(R6)
        ));

    // ===================== 7 Types =====================
    public static IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4, R5? result5, R6? result6, R7? result7)>
        SelectCases<T, R1, R2, R3, R4, R5, R6, R7>(
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
            x.category == 0 ? selector1(x.item) : default(R1),
            x.category == 1 ? selector2(x.item) : default(R2),
            x.category == 2 ? selector3(x.item) : default(R3),
            x.category == 3 ? selector4(x.item) : default(R4),
            x.category == 4 ? selector5(x.item) : default(R5),
            x.category == 5 ? selector6(x.item) : default(R6),
            x.category == 6 ? selector7(x.item) : default(R7)
        ));

    #endregion

    #region ForEachCases Multi-Type (2-7 types)

    // ===================== 2 Types =====================
    public static IAsyncEnumerable<(int category, T item, R1? result1, R2? result2)>
        ForEachCases<T, R1, R2>(
            this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2)> items,
            Action<R1> action1,
            Action<R2> action2)
        => items.ForEach(x =>
        {
            switch (x.category)
            {
                case 0: if (x.result1 is not null) action1(x.result1); break;
                case 1: if (x.result2 is not null) action2(x.result2); break;
            }
        });

    // ===================== 3 Types =====================
    public static IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3)>
        ForEachCases<T, R1, R2, R3>(
            this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3)> items,
            Action<R1> action1,
            Action<R2> action2,
            Action<R3> action3)
        => items.ForEach(x =>
        {
            switch (x.category)
            {
                case 0: if (x.result1 is not null) action1(x.result1); break;
                case 1: if (x.result2 is not null) action2(x.result2); break;
                case 2: if (x.result3 is not null) action3(x.result3); break;
            }
        });

    // ===================== 4 Types =====================
    public static IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4)>
        ForEachCases<T, R1, R2, R3, R4>(
            this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4)> items,
            Action<R1> action1,
            Action<R2> action2,
            Action<R3> action3,
            Action<R4> action4)
        => items.ForEach(x =>
        {
            switch (x.category)
            {
                case 0: if (x.result1 is not null) action1(x.result1); break;
                case 1: if (x.result2 is not null) action2(x.result2); break;
                case 2: if (x.result3 is not null) action3(x.result3); break;
                case 3: if (x.result4 is not null) action4(x.result4); break;
            }
        });

    // ===================== 5 Types =====================
    public static IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4, R5? result5)>
        ForEachCases<T, R1, R2, R3, R4, R5>(
            this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4, R5? result5)> items,
            Action<R1> action1,
            Action<R2> action2,
            Action<R3> action3,
            Action<R4> action4,
            Action<R5> action5)
        => items.ForEach(x =>
        {
            switch (x.category)
            {
                case 0: if (x.result1 is not null) action1(x.result1); break;
                case 1: if (x.result2 is not null) action2(x.result2); break;
                case 2: if (x.result3 is not null) action3(x.result3); break;
                case 3: if (x.result4 is not null) action4(x.result4); break;
                case 4: if (x.result5 is not null) action5(x.result5); break;
            }
        });

    // ===================== 6 Types =====================
    public static IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4, R5? result5, R6? result6)>
        ForEachCases<T, R1, R2, R3, R4, R5, R6>(
            this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4, R5? result5, R6? result6)> items,
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
                case 0: if (x.result1 is not null) action1(x.result1); break;
                case 1: if (x.result2 is not null) action2(x.result2); break;
                case 2: if (x.result3 is not null) action3(x.result3); break;
                case 3: if (x.result4 is not null) action4(x.result4); break;
                case 4: if (x.result5 is not null) action5(x.result5); break;
                case 5: if (x.result6 is not null) action6(x.result6); break;
            }
        });

    // ===================== 7 Types =====================
    public static IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4, R5? result5, R6? result6, R7? result7)>
        ForEachCases<T, R1, R2, R3, R4, R5, R6, R7>(
            this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4, R5? result5, R6? result6, R7? result7)> items,
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
                case 0: if (x.result1 is not null) action1(x.result1); break;
                case 1: if (x.result2 is not null) action2(x.result2); break;
                case 2: if (x.result3 is not null) action3(x.result3); break;
                case 3: if (x.result4 is not null) action4(x.result4); break;
                case 4: if (x.result5 is not null) action5(x.result5); break;
                case 5: if (x.result6 is not null) action6(x.result6); break;
                case 6: if (x.result7 is not null) action7(x.result7); break;
            }
        });

    #endregion

    #region UnCase Multi-Type (2-7 types)

    public static IAsyncEnumerable<T> UnCase<T, R1, R2>(
        this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2)> items)
        => items.Select(x => x.item);

    public static IAsyncEnumerable<T> UnCase<T, R1, R2, R3>(
        this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3)> items)
        => items.Select(x => x.item);

    public static IAsyncEnumerable<T> UnCase<T, R1, R2, R3, R4>(
        this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4)> items)
        => items.Select(x => x.item);

    public static IAsyncEnumerable<T> UnCase<T, R1, R2, R3, R4, R5>(
        this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4, R5? result5)> items)
        => items.Select(x => x.item);

    public static IAsyncEnumerable<T> UnCase<T, R1, R2, R3, R4, R5, R6>(
        this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4, R5? result5, R6? result6)> items)
        => items.Select(x => x.item);

    public static IAsyncEnumerable<T> UnCase<T, R1, R2, R3, R4, R5, R6, R7>(
        this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4, R5? result5, R6? result6, R7? result7)> items)
        => items.Select(x => x.item);

    #endregion

    #region AllCases Multi-Type (2-7 types)

    public static IAsyncEnumerable<object?> AllCases<T, R1, R2>(
        this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2)> items)
        => items.Select(x => x.category switch
        {
            0 => (object?)x.result1,
            1 => (object?)x.result2,
            _ => null
        }).Where(x => x is not null);

    public static IAsyncEnumerable<object?> AllCases<T, R1, R2, R3>(
        this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3)> items)
        => items.Select(x => x.category switch
        {
            0 => (object?)x.result1,
            1 => (object?)x.result2,
            2 => (object?)x.result3,
            _ => null
        }).Where(x => x is not null);

    public static IAsyncEnumerable<object?> AllCases<T, R1, R2, R3, R4>(
        this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4)> items)
        => items.Select(x => x.category switch
        {
            0 => (object?)x.result1,
            1 => (object?)x.result2,
            2 => (object?)x.result3,
            3 => (object?)x.result4,
            _ => null
        }).Where(x => x is not null);

    public static IAsyncEnumerable<object?> AllCases<T, R1, R2, R3, R4, R5>(
        this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4, R5? result5)> items)
        => items.Select(x => x.category switch
        {
            0 => (object?)x.result1,
            1 => (object?)x.result2,
            2 => (object?)x.result3,
            3 => (object?)x.result4,
            4 => (object?)x.result5,
            _ => null
        }).Where(x => x is not null);

    public static IAsyncEnumerable<object?> AllCases<T, R1, R2, R3, R4, R5, R6>(
        this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4, R5? result5, R6? result6)> items)
        => items.Select(x => x.category switch
        {
            0 => (object?)x.result1,
            1 => (object?)x.result2,
            2 => (object?)x.result3,
            3 => (object?)x.result4,
            4 => (object?)x.result5,
            5 => (object?)x.result6,
            _ => null
        }).Where(x => x is not null);

    public static IAsyncEnumerable<object?> AllCases<T, R1, R2, R3, R4, R5, R6, R7>(
        this IAsyncEnumerable<(int category, T item, R1? result1, R2? result2, R3? result3, R4? result4, R5? result5, R6? result6, R7? result7)> items)
        => items.Select(x => x.category switch
        {
            0 => (object?)x.result1,
            1 => (object?)x.result2,
            2 => (object?)x.result3,
            3 => (object?)x.result4,
            4 => (object?)x.result5,
            5 => (object?)x.result6,
            6 => (object?)x.result7,
            _ => null
        }).Where(x => x is not null);

    #endregion
}
