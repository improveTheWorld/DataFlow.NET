#nullable disable
// Nullable disabled due to heavy Microsoft.Spark interop and generic type constraints

using Microsoft.Spark.Sql;
using System.Linq.Expressions;
using DataFlow.Framework;

namespace DataFlow.Extensions;

/// <summary>
/// Multi-type SelectCase extensions for SparkQuery.
/// Supports different return types per branch (R1, R2, ... up to R4).
/// Note: Due to Spark's columnar model, multi-type results are stored as separate columns.
/// The category index determines which result slot is active, not nullability.
/// </summary>
public static class SparkQueryMultiTypeCasesExtensions
{
    #region SelectCase Multi-Type (2-4 types)

    // ===================== 2 Types =====================
    public static SparkQuery<(int category, T item, (R1, R2) result)> SelectCase<T, R1, R2>(
        this SparkQuery<(int category, T item)> items,
        Expression<Func<T, R1>> selector1,
        Expression<Func<T, R2>> selector2)
    {
        var df = items.ToDataFrame();
        var innerMapper = (items.ColumnMapper as CategoryTupleMapper<T>)?.InnerMapper ?? new ConventionColumnMapper<T>();
        var itemMapper = new StructPrefixedColumnMapper<T>("Item2", innerMapper);
        var translator = new ColumnExpressionTranslator<T>(itemMapper);

        Column col1 = Functions.When(df.Col("Item1") == 0, translator.TranslateToColumn(selector1))
            .Otherwise(Functions.Lit(null).Cast(SparkTypeUtil.GetSparkTypeName(typeof(R1))));

        Column col2 = Functions.When(df.Col("Item1") == 1, translator.TranslateToColumn(selector2))
            .Otherwise(Functions.Lit(null).Cast(SparkTypeUtil.GetSparkTypeName(typeof(R2))));

        var resultStruct = Functions.Struct(col1.As("Item1"), col2.As("Item2"));
        var resultDf = df.WithColumn("Item3", resultStruct);

        return SparkQueryFactory.Create<(int category, T item, (R1, R2) result)>(
            items.SparkSession, resultDf, new MultiTypeCategoryMapper<T, R1, R2>(innerMapper));
    }

    // ===================== 3 Types =====================
    public static SparkQuery<(int category, T item, (R1, R2, R3) result)> SelectCase<T, R1, R2, R3>(
        this SparkQuery<(int category, T item)> items,
        Expression<Func<T, R1>> selector1,
        Expression<Func<T, R2>> selector2,
        Expression<Func<T, R3>> selector3)
    {
        var df = items.ToDataFrame();
        var innerMapper = (items.ColumnMapper as CategoryTupleMapper<T>)?.InnerMapper ?? new ConventionColumnMapper<T>();
        var itemMapper = new StructPrefixedColumnMapper<T>("Item2", innerMapper);
        var translator = new ColumnExpressionTranslator<T>(itemMapper);

        Column col1 = Functions.When(df.Col("Item1") == 0, translator.TranslateToColumn(selector1))
            .Otherwise(Functions.Lit(null).Cast(SparkTypeUtil.GetSparkTypeName(typeof(R1))));
        Column col2 = Functions.When(df.Col("Item1") == 1, translator.TranslateToColumn(selector2))
            .Otherwise(Functions.Lit(null).Cast(SparkTypeUtil.GetSparkTypeName(typeof(R2))));
        Column col3 = Functions.When(df.Col("Item1") == 2, translator.TranslateToColumn(selector3))
            .Otherwise(Functions.Lit(null).Cast(SparkTypeUtil.GetSparkTypeName(typeof(R3))));

        var resultStruct = Functions.Struct(col1.As("Item1"), col2.As("Item2"), col3.As("Item3"));
        var resultDf = df.WithColumn("Item3", resultStruct);

        return SparkQueryFactory.Create<(int category, T item, (R1, R2, R3) result)>(
            items.SparkSession, resultDf, new MultiTypeCategoryMapper<T, R1, R2, R3>(innerMapper));
    }

    // ===================== 4 Types =====================
    public static SparkQuery<(int category, T item, (R1, R2, R3, R4) result)> SelectCase<T, R1, R2, R3, R4>(
        this SparkQuery<(int category, T item)> items,
        Expression<Func<T, R1>> selector1,
        Expression<Func<T, R2>> selector2,
        Expression<Func<T, R3>> selector3,
        Expression<Func<T, R4>> selector4)
    {
        var df = items.ToDataFrame();
        var innerMapper = (items.ColumnMapper as CategoryTupleMapper<T>)?.InnerMapper ?? new ConventionColumnMapper<T>();
        var itemMapper = new StructPrefixedColumnMapper<T>("Item2", innerMapper);
        var translator = new ColumnExpressionTranslator<T>(itemMapper);

        Column col1 = Functions.When(df.Col("Item1") == 0, translator.TranslateToColumn(selector1))
            .Otherwise(Functions.Lit(null).Cast(SparkTypeUtil.GetSparkTypeName(typeof(R1))));
        Column col2 = Functions.When(df.Col("Item1") == 1, translator.TranslateToColumn(selector2))
            .Otherwise(Functions.Lit(null).Cast(SparkTypeUtil.GetSparkTypeName(typeof(R2))));
        Column col3 = Functions.When(df.Col("Item1") == 2, translator.TranslateToColumn(selector3))
            .Otherwise(Functions.Lit(null).Cast(SparkTypeUtil.GetSparkTypeName(typeof(R3))));
        Column col4 = Functions.When(df.Col("Item1") == 3, translator.TranslateToColumn(selector4))
            .Otherwise(Functions.Lit(null).Cast(SparkTypeUtil.GetSparkTypeName(typeof(R4))));

        var resultStruct = Functions.Struct(col1.As("Item1"), col2.As("Item2"), col3.As("Item3"), col4.As("Item4"));
        var resultDf = df.WithColumn("Item3", resultStruct);

        return SparkQueryFactory.Create<(int category, T item, (R1, R2, R3, R4) result)>(
            items.SparkSession, resultDf, new MultiTypeCategoryMapper<T, R1, R2, R3, R4>(innerMapper));
    }

    #endregion

    #region ForEachCase Multi-Type (2-4 types)

    // ===================== 2 Types =====================
    public static void ForEachCase<T, R1, R2>(
        this SparkQuery<(int category, T item, (R1, R2) result)> items,
        Action<SparkQuery<R1>> action1,
        Action<SparkQuery<R2>> action2)
    {
        var categorizedDf = items.ToDataFrame();

        if (action1 != null)
        {
            var categoryDf = categorizedDf.Filter(Functions.Col("Item1") == 0);
            var itemsDf = categoryDf.Select("Item3.Item1").Filter(Functions.Col("Item1").IsNotNull());
            action1(SparkQueryFactory.Create<R1>(items.SparkSession, itemsDf, new SingleColumnMapper<R1>()));
        }

        if (action2 != null)
        {
            var categoryDf = categorizedDf.Filter(Functions.Col("Item1") == 1);
            var itemsDf = categoryDf.Select("Item3.Item2").Filter(Functions.Col("Item2").IsNotNull());
            action2(SparkQueryFactory.Create<R2>(items.SparkSession, itemsDf, new SingleColumnMapper<R2>()));
        }
    }

    // ===================== 3 Types =====================
    public static void ForEachCase<T, R1, R2, R3>(
        this SparkQuery<(int category, T item, (R1, R2, R3) result)> items,
        Action<SparkQuery<R1>> action1,
        Action<SparkQuery<R2>> action2,
        Action<SparkQuery<R3>> action3)
    {
        var categorizedDf = items.ToDataFrame();

        if (action1 != null)
        {
            var categoryDf = categorizedDf.Filter(Functions.Col("Item1") == 0);
            var itemsDf = categoryDf.Select("Item3.Item1").Filter(Functions.Col("Item1").IsNotNull());
            action1(SparkQueryFactory.Create<R1>(items.SparkSession, itemsDf, new SingleColumnMapper<R1>()));
        }

        if (action2 != null)
        {
            var categoryDf = categorizedDf.Filter(Functions.Col("Item1") == 1);
            var itemsDf = categoryDf.Select("Item3.Item2").Filter(Functions.Col("Item2").IsNotNull());
            action2(SparkQueryFactory.Create<R2>(items.SparkSession, itemsDf, new SingleColumnMapper<R2>()));
        }

        if (action3 != null)
        {
            var categoryDf = categorizedDf.Filter(Functions.Col("Item1") == 2);
            var itemsDf = categoryDf.Select("Item3.Item3").Filter(Functions.Col("Item3").IsNotNull());
            action3(SparkQueryFactory.Create<R3>(items.SparkSession, itemsDf, new SingleColumnMapper<R3>()));
        }
    }

    // ===================== 4 Types =====================
    public static void ForEachCase<T, R1, R2, R3, R4>(
        this SparkQuery<(int category, T item, (R1, R2, R3, R4) result)> items,
        Action<SparkQuery<R1>> action1,
        Action<SparkQuery<R2>> action2,
        Action<SparkQuery<R3>> action3,
        Action<SparkQuery<R4>> action4)
    {
        var categorizedDf = items.ToDataFrame();

        if (action1 != null)
        {
            var categoryDf = categorizedDf.Filter(Functions.Col("Item1") == 0);
            var itemsDf = categoryDf.Select("Item3.Item1").Filter(Functions.Col("Item1").IsNotNull());
            action1(SparkQueryFactory.Create<R1>(items.SparkSession, itemsDf, new SingleColumnMapper<R1>()));
        }

        if (action2 != null)
        {
            var categoryDf = categorizedDf.Filter(Functions.Col("Item1") == 1);
            var itemsDf = categoryDf.Select("Item3.Item2").Filter(Functions.Col("Item2").IsNotNull());
            action2(SparkQueryFactory.Create<R2>(items.SparkSession, itemsDf, new SingleColumnMapper<R2>()));
        }

        if (action3 != null)
        {
            var categoryDf = categorizedDf.Filter(Functions.Col("Item1") == 2);
            var itemsDf = categoryDf.Select("Item3.Item3").Filter(Functions.Col("Item3").IsNotNull());
            action3(SparkQueryFactory.Create<R3>(items.SparkSession, itemsDf, new SingleColumnMapper<R3>()));
        }

        if (action4 != null)
        {
            var categoryDf = categorizedDf.Filter(Functions.Col("Item1") == 3);
            var itemsDf = categoryDf.Select("Item3.Item4").Filter(Functions.Col("Item4").IsNotNull());
            action4(SparkQueryFactory.Create<R4>(items.SparkSession, itemsDf, new SingleColumnMapper<R4>()));
        }
    }

    #endregion

    #region UnCase Multi-Type (2-4 types)

    public static SparkQuery<T> UnCase<T, R1, R2>(
        this SparkQuery<(int category, T item, (R1, R2) result)> items)
    {
        var resultDf = items.ToDataFrame().Select("Item2.*");
        var originalMapper = (items.ColumnMapper as IMultiTypeCategoryMapper)?.InnerMapper as IColumnMapper<T>
            ?? new ConventionColumnMapper<T>();
        return SparkQueryFactory.Create<T>(items.SparkSession, resultDf, originalMapper);
    }

    public static SparkQuery<T> UnCase<T, R1, R2, R3>(
        this SparkQuery<(int category, T item, (R1, R2, R3) result)> items)
    {
        var resultDf = items.ToDataFrame().Select("Item2.*");
        var originalMapper = (items.ColumnMapper as IMultiTypeCategoryMapper)?.InnerMapper as IColumnMapper<T>
            ?? new ConventionColumnMapper<T>();
        return SparkQueryFactory.Create<T>(items.SparkSession, resultDf, originalMapper);
    }

    public static SparkQuery<T> UnCase<T, R1, R2, R3, R4>(
        this SparkQuery<(int category, T item, (R1, R2, R3, R4) result)> items)
    {
        var resultDf = items.ToDataFrame().Select("Item2.*");
        var originalMapper = (items.ColumnMapper as IMultiTypeCategoryMapper)?.InnerMapper as IColumnMapper<T>
            ?? new ConventionColumnMapper<T>();
        return SparkQueryFactory.Create<T>(items.SparkSession, resultDf, originalMapper);
    }

    #endregion
}

#region Mapper Types for Multi-Type Support

/// <summary>
/// Interface to identify multi-type category mappers and access their inner mapper.
/// </summary>
internal interface IMultiTypeCategoryMapper
{
    object InnerMapper { get; }
}

/// <summary>
/// Column mapper for 2-type multi-type SelectCase results.
/// </summary>
internal class MultiTypeCategoryMapper<T, R1, R2> : IColumnMapper<(int, T, (R1, R2))>, IMultiTypeCategoryMapper
{
    private readonly IColumnMapper<T> _innerMapper;
    public object InnerMapper => _innerMapper;

    public MultiTypeCategoryMapper(IColumnMapper<T> innerMapper) => _innerMapper = innerMapper ?? new ConventionColumnMapper<T>();

    public string[] GetAllColumns() => new[] { "Item1", "Item2", "Item3" };
    public string GetColumnName(string propertyName) => propertyName switch
    {
        "Item1" or "category" => "Item1",
        "Item2" or "item" => "Item2",
        "Item3" or "result" => "Item3",
        _ => propertyName
    };
    public string GetColumnName(IEnumerable<string> propertyPath) => GetColumnName(propertyPath.First());

    public (int, T, (R1, R2)) MapFromRow(Row row)
    {
        var category = row.GetAs<int>("Item1");
        var itemRow = row.GetAs<Row>("Item2");
        var item = _innerMapper.MapFromRow(itemRow);
        var resultRow = row.GetAs<Row>("Item3");
        var result = (resultRow != null ? resultRow.GetAs<R1>(0) : default,
                      resultRow != null ? resultRow.GetAs<R2>(1) : default);
        return (category, item, result);
    }
}

/// <summary>
/// Column mapper for 3-type multi-type SelectCase results.
/// </summary>
internal class MultiTypeCategoryMapper<T, R1, R2, R3> : IColumnMapper<(int, T, (R1, R2, R3))>, IMultiTypeCategoryMapper
{
    private readonly IColumnMapper<T> _innerMapper;
    public object InnerMapper => _innerMapper;

    public MultiTypeCategoryMapper(IColumnMapper<T> innerMapper) => _innerMapper = innerMapper ?? new ConventionColumnMapper<T>();

    public string[] GetAllColumns() => new[] { "Item1", "Item2", "Item3" };
    public string GetColumnName(string propertyName) => propertyName switch
    {
        "Item1" or "category" => "Item1",
        "Item2" or "item" => "Item2",
        "Item3" or "result" => "Item3",
        _ => propertyName
    };
    public string GetColumnName(IEnumerable<string> propertyPath) => GetColumnName(propertyPath.First());

    public (int, T, (R1, R2, R3)) MapFromRow(Row row)
    {
        var category = row.GetAs<int>("Item1");
        var itemRow = row.GetAs<Row>("Item2");
        var item = _innerMapper.MapFromRow(itemRow);
        var resultRow = row.GetAs<Row>("Item3");
        var result = (resultRow != null ? resultRow.GetAs<R1>(0) : default,
                      resultRow != null ? resultRow.GetAs<R2>(1) : default,
                      resultRow != null ? resultRow.GetAs<R3>(2) : default);
        return (category, item, result);
    }
}

/// <summary>
/// Column mapper for 4-type multi-type SelectCase results.
/// </summary>
internal class MultiTypeCategoryMapper<T, R1, R2, R3, R4> : IColumnMapper<(int, T, (R1, R2, R3, R4))>, IMultiTypeCategoryMapper
{
    private readonly IColumnMapper<T> _innerMapper;
    public object InnerMapper => _innerMapper;

    public MultiTypeCategoryMapper(IColumnMapper<T> innerMapper) => _innerMapper = innerMapper ?? new ConventionColumnMapper<T>();

    public string[] GetAllColumns() => new[] { "Item1", "Item2", "Item3" };
    public string GetColumnName(string propertyName) => propertyName switch
    {
        "Item1" or "category" => "Item1",
        "Item2" or "item" => "Item2",
        "Item3" or "result" => "Item3",
        _ => propertyName
    };
    public string GetColumnName(IEnumerable<string> propertyPath) => GetColumnName(propertyPath.First());

    public (int, T, (R1, R2, R3, R4)) MapFromRow(Row row)
    {
        var category = row.GetAs<int>("Item1");
        var itemRow = row.GetAs<Row>("Item2");
        var item = _innerMapper.MapFromRow(itemRow);
        var resultRow = row.GetAs<Row>("Item3");
        var result = (resultRow != null ? resultRow.GetAs<R1>(0) : default,
                      resultRow != null ? resultRow.GetAs<R2>(1) : default,
                      resultRow != null ? resultRow.GetAs<R3>(2) : default,
                      resultRow != null ? resultRow.GetAs<R4>(3) : default);
        return (category, item, result);
    }
}

#endregion
