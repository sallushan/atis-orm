using Atis.SqlExpressionEngine.ExpressionExtensions;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.UnitTest.Metadata
{
    public interface IEntityRelation
    {
        Expression? JoinExpression { get; }
        LambdaExpression? FromParentToChild();
        LambdaExpression? FromChildToParent();
        Type ParentType { get; }
        Type ChildType { get; }
    }

    public interface IEntityRelation<TParent, TChild> : IEntityRelation
    {
        Expression<Func<TParent, IQueryable<TChild>>>? FromParentToChild();
        Expression<Func<TChild, IQueryable<TParent>>>? FromChildToParent();
    }

    public abstract class EntityRelation<TParent, TChild> : IEntityRelation<TParent, TChild>
    {
        Expression? IEntityRelation.JoinExpression => JoinExpression;

        public abstract Expression<Func<TParent, TChild, bool>>? JoinExpression { get; }

        public Type ParentType => typeof(TParent);
        public Type ChildType => typeof(TChild);

        public virtual Expression<Func<TParent, IQueryable<TChild>>> FromParentToChild()
        {
            var p = Expression.Parameter(typeof(TParent), "p");
            var body = new QueryRootExpression(typeof(TChild)); // IQueryable<TChild> in your system
            return Expression.Lambda<Func<TParent, IQueryable<TChild>>>(body, p);
        }

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
