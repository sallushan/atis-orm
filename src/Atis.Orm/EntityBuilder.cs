using Atis.Expressions;
using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.Orm
{
    public class EntityBuilder<T>
    {
        private readonly MutableEntityMetadata _mutable;

        internal EntityBuilder(MutableEntityMetadata mutable)
        {
            _mutable = mutable ?? throw new ArgumentNullException(nameof(mutable));
        }

        public EntityBuilder<T> ToTable(string tableName)
            => this.ToTable(tableName, schema: null, database: null, server: null);

        public EntityBuilder<T> ToTable(string tableName, string schema)
            => this.ToTable(tableName, schema, database: null, server: null);

        public EntityBuilder<T> ToTable(string tableName, string schema, string database, string server)
        {
            _mutable.TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
            _mutable.Schema = schema;
            _mutable.Database = database;
            _mutable.Server = server;
            return this;
        }

        public ColumnBuilder<T> Column(Expression<Func<T, object>> property, string columnName)
        {
            return this.Column(property).SetColumnName(columnName);
        }

        public ColumnBuilder<T> Column(Expression<Func<T, object>> property)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));
            var memberName = MemberNameExtractor.GetMemberName(property);
            return new ColumnBuilder<T>(_mutable, memberName);
        }

        public EntityBuilder<T> HasKey(Expression<Func<T, object>> property)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));

            this.Column(property).MarkAsKey();

            return this;
        }

        /// <summary>
        ///     Configures a calculated property. The property name is taken from <paramref name="property"/>
        ///     and the calculation from <paramref name="expression"/>.
        /// </summary>
        public EntityBuilder<T> Calculated<TResult>(Expression<Func<T, TResult>> property, Expression<Func<T, TResult>> expression)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));
            if (expression == null) throw new ArgumentNullException(nameof(expression));

            var memberName = MemberNameExtractor.GetMemberName(property);
            _mutable.RemoveColumn(memberName);
            _mutable.CalculatedProperties[memberName] = expression;
            return this;
        }

        // Navigations — ToChildren (one-to-many)

        public EntityBuilder<T> HasMany<C>(Expression<Func<T, IEnumerable<C>>> nav, Expression<Func<T, object>> parentKey, Expression<Func<C, object>> childKey)
        {
            var memberName = MemberNameExtractor.GetMemberName(nav);
            var joinCondition = JoinConditionFactory.Create(typeof(T), MemberNameExtractor.GetMemberNames(parentKey), typeof(C), MemberNameExtractor.GetMemberNames(childKey));
            this.AddNavigation<C>(NavigationType.ToChildren, memberName, joinCondition);
            return this;
        }

        public EntityBuilder<T> HasMany<C>(Expression<Func<T, IEnumerable<C>>> nav, Expression<Func<T, C, bool>> joinCondition)
        {
            if (joinCondition == null) throw new ArgumentNullException(nameof(joinCondition));
            var memberName = MemberNameExtractor.GetMemberName(nav);
            this.AddNavigation<C>(NavigationType.ToChildren, memberName, joinCondition);
            return this;
        }

        // Navigations — ToSingleChild (one-to-one, this entity is principal)

        public EntityBuilder<T> HasChild<C>(Expression<Func<T, C>> nav, Expression<Func<T, object>> parentKey, Expression<Func<C, object>> childKey)
        {
            var memberName = MemberNameExtractor.GetMemberName(nav);
            var joinCondition = JoinConditionFactory.Create(typeof(T), MemberNameExtractor.GetMemberNames(parentKey), typeof(C), MemberNameExtractor.GetMemberNames(childKey));
            this.AddNavigation<C>(NavigationType.ToSingleChild, memberName, joinCondition);
            return this;
        }

        public EntityBuilder<T> HasChild<C>(Expression<Func<T, C>> nav, Expression<Func<T, C, bool>> joinCondition)
        {
            if (joinCondition == null) throw new ArgumentNullException(nameof(joinCondition));
            var memberName = MemberNameExtractor.GetMemberName(nav);
            this.AddNavigation<C>(NavigationType.ToSingleChild, memberName, joinCondition);
            return this;
        }

        // Navigations — ToParent (many-to-one); .Optional() promotes to ToParentOptional

        public ParentNavigationBuilder<T> HasParent<P>(Expression<Func<T, P>> nav, Expression<Func<P, object>> parentKey, Expression<Func<T, object>> childKey)
        {
            var memberName = MemberNameExtractor.GetMemberName(nav);
            var joinCondition = JoinConditionFactory.Create(typeof(P), MemberNameExtractor.GetMemberNames(parentKey), typeof(T), MemberNameExtractor.GetMemberNames(childKey));
            var mutableNav = this.AddNavigation<P>(NavigationType.ToParent, memberName, joinCondition);
            return new ParentNavigationBuilder<T>(mutableNav);
        }

        public ParentNavigationBuilder<T> HasParent<P>(Expression<Func<T, P>> nav, Expression<Func<P, T, bool>> joinCondition)
        {
            if (joinCondition == null) throw new ArgumentNullException(nameof(joinCondition));
            var memberName = MemberNameExtractor.GetMemberName(nav);
            var mutableNav = this.AddNavigation<P>(NavigationType.ToParent, memberName, joinCondition);
            return new ParentNavigationBuilder<T>(mutableNav);
        }

        // Navigations — single related row sourced from a correlated subquery (OUTER APPLY)

        /// <summary>
        ///     <para>
        ///         Configures a single-valued navigation whose related row is produced by a
        ///         correlated sub-query (e.g. a <c>TOP 1</c> look-up). It is registered as
        ///         <see cref="NavigationType.ToSingleChild"/> with no join condition, which the
        ///         engine translates to an <c>OUTER APPLY</c>.
        ///     </para>
        ///     <para>
        ///         The <paramref name="subquery"/> receives the declaring entity and the data source
        ///         of <typeparamref name="C"/>, and must correlate the two, for example:
        ///         <code>(a, books) =&gt; books.Where(b =&gt; b.AuthorId == a.Id).OrderByDesc(b =&gt; b.Year).Take(1)</code>
        ///     </para>
        /// </summary>
        /// <typeparam name="C">The related entity type.</typeparam>
        public EntityBuilder<T> HasOneRow<C>(Expression<Func<T, C>> nav, Expression<Func<T, IQueryable<C>, IQueryable<C>>> subquery)
        {
            if (nav == null) throw new ArgumentNullException(nameof(nav));
            if (subquery == null) throw new ArgumentNullException(nameof(subquery));

            var memberName = MemberNameExtractor.GetMemberName(nav);

            // Turn (entity, source) => correlatedQuery into (entity) => correlatedQuery by replacing
            // the source parameter with the engine's query root for C. The body still references the
            // entity parameter, which is exactly the correlation required for OUTER APPLY.
            var entityParameter = subquery.Parameters[0];
            var sourceParameter = subquery.Parameters[1];
            var joinedSourceBody = ExpressionReplacementVisitor.Replace(sourceParameter, new QueryRootExpression(typeof(C)), subquery.Body);
            var joinedSource = Expression.Lambda<Func<T, IQueryable<C>>>(joinedSourceBody, entityParameter);

            this.AddNavigation(NavigationType.ToSingleChild, memberName, joinCondition: null, joinedSource: joinedSource);
            return this;
        }

        private MutableNavigationInfo AddNavigation<TTarget>(NavigationType navigationType, string propertyName, LambdaExpression joinCondition)
        {
            var joinedSource = NavigationDataSourceFactory.Create(typeof(T), typeof(TTarget));
            return this.AddNavigation(navigationType, propertyName, joinCondition, joinedSource);
        }

        private MutableNavigationInfo AddNavigation(NavigationType navigationType, string propertyName, LambdaExpression joinCondition, LambdaExpression joinedSource)
        {
            this._mutable.RemoveColumn(propertyName);
            var nav = new MutableNavigationInfo(navigationType, joinCondition, joinedSource, propertyName);
            this._mutable.Navigations[propertyName] = nav;
            return nav;
        }

        internal EntityMetadata Build() => _mutable.Build();
    }
}
