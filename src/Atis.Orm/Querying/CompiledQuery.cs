using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;

using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
using Atis.Orm.Translation;
namespace Atis.Orm.Querying
{
    /// <summary>
    ///     <para>
    ///         Shared state and value-resolution policy for the two compiled-query shapes. The concrete
    ///         shape is chosen once, at compile time, from <see cref="SqlTranslationResult.HasExpandableParameters"/>:
    ///         <see cref="SimpleCompiledQuery"/> renders its SQL once, <see cref="ExpandableCompiledQuery"/>
    ///         re-renders every execution. Both instances are immutable, so a compiled query shared in the
    ///         singleton cache is safe to execute concurrently.
    ///     </para>
    /// </summary>
    public abstract class CompiledQueryBase : ICompiledQuery
    {
        private readonly bool isNonQuery;
        private readonly Func<IDataReader, object> elementFactory;

        protected CompiledQueryBase(bool isNonQuery, Func<IDataReader, object> elementFactory)
        {
            this.isNonQuery = isNonQuery;
            this.elementFactory = elementFactory;
        }

        public abstract IExecutionContext GetExecutionContext(IReadOnlyDictionary<string, object> parameterValuesByIdentity, bool useInitialValues);

        protected IExecutionContext CreateExecutionContext(string sql, IReadOnlyList<DbParameter> dbParameters)
        {
            return new ExecutionContext(sql, dbParameters, this.isNonQuery, this.elementFactory);
        }

        /// <summary>
        ///     <para>
        ///         Resolves the value bound to <paramref name="queryParameter"/> for one execution. Literals
        ///         and cache-miss executions keep their translation-time <see cref="IQueryParameter.InitialValue"/>;
        ///         on a cache hit, non-literal parameters are rebound by identity lookup (order-independent,
        ///         because the SqlExpression tree may be reshaped after emission).
        ///     </para>
        ///     <para>
        ///         A cache hit re-extracts values from the <em>original</em> expression, never a re-preprocessed
        ///         one, so an identity the original tree does not carry has nothing to rebind against — that is
        ///         a parameter a preprocessor introduced (an inlined static, a calculated-property constant).
        ///         Such a parameter falls back to its <see cref="IQueryParameter.InitialValue"/>, i.e. it
        ///         behaves as a literal frozen when the query first compiled. Per the shape-determinism
        ///         contract on <see cref="Atis.Expressions.IExpressionPreprocessor"/> an injected value is
        ///         stable configuration, so the frozen value is the right one; a preprocessor that injects
        ///         something genuinely per-execution is violating that contract, and no single execution can
        ///         detect it.
        ///     </para>
        /// </summary>
        protected static object ResolveValue(IQueryParameter queryParameter, IReadOnlyDictionary<string, object> parameterValuesByIdentity, bool useInitialValues)
        {
            if (useInitialValues || queryParameter.IsLiteral)
                return queryParameter.InitialValue;
            if (parameterValuesByIdentity != null
                && queryParameter.ParameterIdentity != null
                && parameterValuesByIdentity.TryGetValue(queryParameter.ParameterIdentity, out var reboundValue))
                return reboundValue;
            return queryParameter.InitialValue;
        }
    }

    /// <summary>
    ///     <para>
    ///         Compiled query with no expandable parameters: the SQL text and the placeholder names are
    ///         value-independent, so they are rendered once. Each execution only rebuilds the
    ///         <see cref="DbParameter"/>s with that execution's values.
    ///     </para>
    /// </summary>
    public sealed class SimpleCompiledQuery : CompiledQueryBase
    {
        private readonly string sql;
        // For a non-expandable query the markers are 1:1 with QueryParameters, in the same order, and the
        // renderer assigns index = position. So this list IS the placeholder plan: element i binds @p{i}.
        private readonly IReadOnlyList<IQueryParameter> orderedParameters;
        private readonly IDbParameterFactory parameterFactory;

        public SimpleCompiledQuery(SqlTranslationResult translation, ISqlCommandRenderer renderer, IDbParameterFactory parameterFactory, bool isNonQuery, Func<IDataReader, object> elementFactory)
            : base(isNonQuery, elementFactory)
        {
            if (translation is null)
                throw new ArgumentNullException(nameof(translation));
            Debug.Assert(!translation.RequiresPerExecutionRendering, "SimpleCompiledQuery is only valid for a translation whose SQL text does not depend on this execution's values.");
            if (renderer is null)
                throw new ArgumentNullException(nameof(renderer));
            // The SQL text is value-independent here, so render it once with any resolver.
            this.sql = renderer.Render(translation.Fragments, p => p.InitialValue).Sql;
            this.orderedParameters = translation.QueryParameters;
            this.parameterFactory = parameterFactory ?? throw new ArgumentNullException(nameof(parameterFactory));
        }

        public override IExecutionContext GetExecutionContext(IReadOnlyDictionary<string, object> parameterValuesByIdentity, bool useInitialValues)
        {
            var dbParameters = new DbParameter[this.orderedParameters.Count];
            for (int i = 0; i < this.orderedParameters.Count; i++)
            {
                var queryParameter = this.orderedParameters[i];
                var value = ResolveValue(queryParameter, parameterValuesByIdentity, useInitialValues);
                dbParameters[i] = this.parameterFactory.CreateDbParameter(i, queryParameter, value);
            }
            return this.CreateExecutionContext(this.sql, dbParameters);
        }
    }

    /// <summary>
    ///     <para>
    ///         Compiled query with at least one expandable parameter: the placeholder count depends on the
    ///         collection length at execution time, so the fragments are re-rendered on every execution.
    ///     </para>
    /// </summary>
    public sealed class ExpandableCompiledQuery : CompiledQueryBase
    {
        private readonly IReadOnlyList<SqlFragment> fragments;
        private readonly ISqlCommandRenderer renderer;

        public ExpandableCompiledQuery(SqlTranslationResult translation, ISqlCommandRenderer renderer, bool isNonQuery, Func<IDataReader, object> elementFactory)
            : base(isNonQuery, elementFactory)
        {
            if (translation is null)
                throw new ArgumentNullException(nameof(translation));
            this.fragments = translation.Fragments;
            this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        }

        public override IExecutionContext GetExecutionContext(IReadOnlyDictionary<string, object> parameterValuesByIdentity, bool useInitialValues)
        {
            var rendered = this.renderer.Render(this.fragments, p => ResolveValue(p, parameterValuesByIdentity, useInitialValues));
            return this.CreateExecutionContext(rendered.Sql, rendered.DbParameters);
        }
    }
}
