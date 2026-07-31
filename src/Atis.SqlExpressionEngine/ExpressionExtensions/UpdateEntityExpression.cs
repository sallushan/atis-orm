using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Atis.SqlExpressionEngine.ExpressionExtensions
{
    // TODO: see if below can be inherited from ChainedQueryExpression
    // the problem with ChainedQueryExpression is that it will always have the Type IQueryable<T>
    // so if T can be become Dictionary<string, object> then it's good (maybe)
    public class UpdateEntityExpression : Expression
    {
        public UpdateEntityExpression(Type resultType, Expression queryExpression, LambdaExpression setters, AggregatedListExpression keys, AggregatedListExpression outputFields)
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
        public AggregatedListExpression Keys { get; }
        public AggregatedListExpression OutputFields { get; }

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
