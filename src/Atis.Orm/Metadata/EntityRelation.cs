using Atis.SqlExpressionEngine.ExpressionExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Atis.Orm.Metadata
{
    /// <summary>
    /// 
    /// </summary>
    public interface IEntityRelation
    {
        /// <summary>
        /// 
        /// </summary>
        Expression JoinExpression { get; }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        LambdaExpression FromParentToChild();
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        LambdaExpression FromChildToParent();
        /// <summary>
        /// 
        /// </summary>
        Type ParentType { get; }
        /// <summary>
        /// 
        /// </summary>
        Type ChildType { get; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TParent"></typeparam>
    /// <typeparam name="TChild"></typeparam>
    public interface IEntityRelation<TParent, TChild> : IEntityRelation
    {
        /// <inheritdoc />
        new Expression<Func<TParent, IQueryable<TChild>>> FromParentToChild();
        /// <inheritdoc />
        new Expression<Func<TChild, IQueryable<TParent>>> FromChildToParent();
    }

    /// <inheritdoc />
    public abstract class EntityRelation<TParent, TChild> : IEntityRelation<TParent, TChild>
    {
        Expression IEntityRelation.JoinExpression => JoinExpression;

        public abstract Expression<Func<TParent, TChild, bool>> JoinExpression { get; }

        /// <inheritdoc />
        public Type ParentType => typeof(TParent);
        /// <inheritdoc />
        public Type ChildType => typeof(TChild);

        /// <inheritdoc />
        public virtual Expression<Func<TParent, IQueryable<TChild>>> FromParentToChild()
        {
            var p = Expression.Parameter(typeof(TParent), "p");
            var body = new QueryRootExpression(typeof(TChild)); // IQueryable<TChild> in your system
            return Expression.Lambda<Func<TParent, IQueryable<TChild>>>(body, p);
        }

        /// <inheritdoc />
        public virtual Expression<Func<TChild, IQueryable<TParent>>> FromChildToParent()
        {
            var c = Expression.Parameter(typeof(TChild), "c");
            var body = new QueryRootExpression(typeof(TParent));
            return Expression.Lambda<Func<TChild, IQueryable<TParent>>>(body, c);
        }

        LambdaExpression IEntityRelation.FromChildToParent() => this.FromChildToParent();
        LambdaExpression IEntityRelation.FromParentToChild() => this.FromParentToChild();
    }
}
