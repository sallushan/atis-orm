using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Atis.SqlExpressionEngine.ExpressionExtensions
{
    /// <summary>
    ///     <para>
    ///         A key-based UPDATE built by the <c>UpdateEntity&lt;T&gt;</c> fluent API: a source query,
    ///         the columns to write, the keys that select the rows, and optionally the columns to read
    ///         back from them.
    ///     </para>
    ///     <para>
    ///         Deliberately not a <see cref="ChainedQueryExpression"/>. That base fixes <c>Type</c> from
    ///         its query (the property is <c>sealed</c>), so a node whose result is a row count or a set
    ///         of returned rows cannot use it. Nothing in the codebase branches on
    ///         <see cref="ChainedQueryExpression"/> either, so inheriting it would buy nothing.
    ///     </para>
    /// </summary>
    public class UpdateEntityExpression : Expression
    {
        public UpdateEntityExpression(Type resultType, Expression queryExpression, LambdaExpression setters, CollectionExpression keys, CollectionExpression outputFields)
        {
            this.Query = queryExpression ?? throw new ArgumentNullException(nameof(queryExpression));
            // Every Expression consumer is entitled to read Type, so it can never be null: an update
            // with no output clause is typed as the affected-row count.
            this.Type = resultType ?? throw new ArgumentNullException(nameof(resultType));
            this.Setters = setters ?? throw new ArgumentNullException(nameof(setters));
            this.Keys = keys ?? throw new ArgumentNullException(nameof(keys));
            // Never null: an update with no output clause carries an empty list, so that consumers can
            // treat Setters, Keys and OutputFields alike instead of null-checking this one.
            this.OutputFields = outputFields ?? throw new ArgumentNullException(nameof(outputFields));
        }

        /// <inheritdoc />
        public override Type Type { get; }
        /// <inheritdoc />
        public override ExpressionType NodeType => ExpressionType.Extension;
        public Expression Query { get; }

        /// <summary>
        ///     <para>
        ///         The SET list, as <c>x =&gt; new T { Member = value, ... }</c>. A member-init rather than
        ///         a list of assignments so that the member <em>names</em> survive into the converter,
        ///         which resolves them to columns through the entity's mapping metadata.
        ///     </para>
        /// </summary>
        public LambdaExpression Setters { get; }
        public CollectionExpression Keys { get; }
        public CollectionExpression OutputFields { get; }

        /// <inheritdoc />
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var newQueryExpression = visitor.Visit(this.Query);
            var newSetters = visitor.VisitAndConvert(this.Setters, "UpdateEntityExpression.VisitChildren");
            var newKeys = visitor.VisitAndConvert(this.Keys, "UpdateEntityExpression.VisitChildren");
            var newOutputFields = visitor.VisitAndConvert(this.OutputFields, "UpdateEntityExpression.VisitChildren");
            if (newQueryExpression != this.Query || newSetters != this.Setters || newKeys != this.Keys || newOutputFields != this.OutputFields)
            {
                return new UpdateEntityExpression(this.Type, newQueryExpression, newSetters, newKeys, newOutputFields);
            }
            return this;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{this.GetType().Name}(set{this.Setters}, keys{this.Keys}, outputFields{this.OutputFields})";
        }
    }
}
