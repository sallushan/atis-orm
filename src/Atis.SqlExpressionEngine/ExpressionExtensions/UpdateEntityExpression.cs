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
        public UpdateEntityExpression(Type outputType, Expression queryExpression, AggregatedListExpression setters, AggregatedListExpression keys, AggregatedListExpression outputFields)
        {
            this.Query = queryExpression;
            this.OutputType = outputType;
            this.Setters = setters ?? throw new ArgumentNullException(nameof(setters));
            this.Keys = keys ?? throw new ArgumentNullException(nameof(keys));
            // Never null: an update with no output clause carries an empty list, so that consumers can
            // treat Setters, Keys and OutputFields alike instead of null-checking this one.
            this.OutputFields = outputFields ?? throw new ArgumentNullException(nameof(outputFields));
        }

        public override Type Type => this.OutputType;
        public override ExpressionType NodeType => ExpressionType.Extension;
        public Expression Query { get; }
        public Type OutputType { get; }
        public AggregatedListExpression Setters { get; }
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
                return new UpdateEntityExpression(this.OutputType, newQueryExpression, newSetters, newKeys, newOutputFields);
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
