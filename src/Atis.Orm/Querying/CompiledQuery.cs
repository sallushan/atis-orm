using System;
using System.Collections.Generic;
using System.Data;

using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
using Atis.Orm.Translation;
namespace Atis.Orm.Querying
{
    public class CompiledQuery : ICompiledQuery
    {
        private readonly IReadOnlyList<SqlFragment> fragments;
        private readonly ISqlCommandRenderer renderer;
        private readonly bool isNonQuery;
        private readonly Func<IDataReader, object> elementFactory;

        public CompiledQuery(SqlTranslationResult translation, ISqlCommandRenderer renderer, bool isNonQuery, Func<IDataReader, object> elementFactory, bool isPreprocessingRequired)
        {
            if (translation is null)
                throw new ArgumentNullException(nameof(translation));
            this.fragments = translation.Fragments;
            this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            this.isNonQuery = isNonQuery;
            this.elementFactory = elementFactory;
            this.IsPreprocessingRequired = isPreprocessingRequired;
        }

        public bool IsPreprocessingRequired { get; }

        public IExecutionContext GetExecutionContext(IReadOnlyDictionary<string, object> parameterValuesByIdentity, bool useInitialValues)
        {
            // Rendering (SQL text + DbParameters) is delegated to the single renderer so a collection
            // parameter can expand to the right number of placeholders for this execution's values. We only
            // supply the value-provenance policy: literals and cache-miss executions keep their translation
            // -time InitialValue; on a cache hit, non-literal parameters are rebound by identity lookup (order
            // -independent, because the SqlExpression tree may be reshaped after emission).
            object ResolveValue(IQueryParameter queryParameter)
            {
                if (useInitialValues || queryParameter.IsLiteral)
                    return queryParameter.InitialValue;
                if (parameterValuesByIdentity != null
                    && queryParameter.ParameterIdentity != null
                    && parameterValuesByIdentity.TryGetValue(queryParameter.ParameterIdentity, out var reboundValue))
                    return reboundValue;
                throw new InvalidOperationException(
                    $"Could not rebind parameter (identity '{queryParameter.ParameterIdentity}') on a cache hit: " +
                    $"no re-extracted value matched its identity.");
            }

            var rendered = this.renderer.Render(this.fragments, ResolveValue);
            return new ExecutionContext(rendered.Sql, rendered.DbParameters, this.isNonQuery, this.elementFactory);
        }
    }
}
