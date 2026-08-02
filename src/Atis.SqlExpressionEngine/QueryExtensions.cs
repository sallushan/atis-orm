using System;
using System.Linq.Expressions;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Atis.SqlExpressionEngine.ExpressionExtensions;

namespace Atis.SqlExpressionEngine
{
    public static class QueryExtensions
    {
        public static T Table<T>()
        {
            throw new NotImplementedException();
        }

        public static T Schema<T>(this IQueryable<T> source)
        {
            throw new NotImplementedException();
        }

        public static IQueryable<T> From<T>(this IQueryProvider provider, Expression<Func<T>> dataSources)
        {
            if (provider is null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            return provider.CreateQuery<T>(
                Expression.Call(
                    typeof(QueryExtensions),
                    nameof(From),
                    new Type[] { typeof(T) },
                    Expression.Constant(provider),
                    dataSources));
        }

        public static IQueryable<T> DataSet<T>(this IQueryProvider provider)
        {
            if (provider is null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            return provider.CreateQuery<T>(
                Expression.Call(
                    typeof(QueryExtensions),
                    nameof(DataSet),
                    new Type[] { typeof(T) },
                    Expression.Constant(provider)));
        }

        public static IQueryable<R> LeftJoin<T, TJoin, R>(this IQueryable<T> source, IQueryable<TJoin> otherSource, Expression<Func<T, TJoin, R>> newSourceFactory, Expression<Func<R, bool>> joinPredicate)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (otherSource is null)
            {
                throw new ArgumentNullException(nameof(otherSource));
            }

            if (newSourceFactory is null)
            {
                throw new ArgumentNullException(nameof(newSourceFactory));
            }

            if (joinPredicate is null)
            {
                throw new ArgumentNullException(nameof(joinPredicate));
            }

            return source.Provider.CreateQuery<R>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, IQueryable<TJoin>, Expression<Func<T, TJoin, R>>, Expression<Func<R, bool>>, IQueryable<R>>(LeftJoin).Method,
                    source.Expression, otherSource.Expression, Expression.Quote(newSourceFactory), Expression.Quote(joinPredicate)));
        }

        public static IQueryable<R> RightJoin<T, TJoin, R>(this IQueryable<T> source, IQueryable<TJoin> otherSource, Expression<Func<T, TJoin, R>> newSourceFactory, Expression<Func<R, bool>> joinPredicate)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (otherSource is null)
            {
                throw new ArgumentNullException(nameof(otherSource));
            }

            if (newSourceFactory is null)
            {
                throw new ArgumentNullException(nameof(newSourceFactory));
            }

            if (joinPredicate is null)
            {
                throw new ArgumentNullException(nameof(joinPredicate));
            }

            return source.Provider.CreateQuery<R>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, IQueryable<TJoin>, Expression<Func<T, TJoin, R>>, Expression<Func<R, bool>>, IQueryable<R>>(RightJoin).Method,
                    source.Expression, otherSource.Expression, Expression.Quote(newSourceFactory), Expression.Quote(joinPredicate)));
        }

        public static IQueryable<R> InnerJoin<T, TJoin, R>(this IQueryable<T> source, IQueryable<TJoin> otherSource, Expression<Func<T, TJoin, R>> newSourceFactory, Expression<Func<R, bool>> joinPredicate)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (otherSource is null)
            {
                throw new ArgumentNullException(nameof(otherSource));
            }

            if (newSourceFactory is null)
            {
                throw new ArgumentNullException(nameof(newSourceFactory));
            }

            if (joinPredicate is null)
            {
                throw new ArgumentNullException(nameof(joinPredicate));
            }

            return source.Provider.CreateQuery<R>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, IQueryable<TJoin>, Expression<Func<T, TJoin, R>>, Expression<Func<R, bool>>, IQueryable<R>>(InnerJoin).Method,
                    source.Expression, otherSource.Expression, Expression.Quote(newSourceFactory), Expression.Quote(joinPredicate)));
        }

        public static IQueryable<T> LeftJoin<T, TJoin>(this IQueryable<T> source, Expression<Func<T, TJoin>> otherSource, Expression<Func<T, bool>> joinPredicate)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (otherSource is null)
            {
                throw new ArgumentNullException(nameof(otherSource));
            }

            if (joinPredicate is null)
            {
                throw new ArgumentNullException(nameof(joinPredicate));
            }

            return source.Provider.CreateQuery<T>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, Expression<Func<T, TJoin>>, Expression<Func<T, bool>>, IQueryable<T>>(LeftJoin).Method,
                    source.Expression, Expression.Quote(otherSource), Expression.Quote(joinPredicate)));
        }

        public static IQueryable<T> RightJoin<T, TJoin>(this IQueryable<T> source, Expression<Func<T, TJoin>> otherSource, Expression<Func<T, bool>> joinPredicate)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (otherSource is null)
            {
                throw new ArgumentNullException(nameof(otherSource));
            }

            if (joinPredicate is null)
            {
                throw new ArgumentNullException(nameof(joinPredicate));
            }

            return source.Provider.CreateQuery<T>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, Expression<Func<T, TJoin>>, Expression<Func<T, bool>>, IQueryable<T>>(RightJoin).Method,
                    source.Expression, Expression.Quote(otherSource), Expression.Quote(joinPredicate)));
        }

        public static IQueryable<T> InnerJoin<T, TJoined>(this IQueryable<T> source, Expression<Func<T, TJoined>> otherSource, Expression<Func<T, bool>> joinPredicate)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (otherSource is null)
            {
                throw new ArgumentNullException(nameof(otherSource));
            }
            if (joinPredicate is null)
            {
                throw new ArgumentNullException(nameof(joinPredicate));
            }
            return source.Provider.CreateQuery<T>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, Expression<Func<T, TJoined>>, Expression<Func<T, bool>>, IQueryable<T>>(InnerJoin).Method,
                    source.Expression, Expression.Quote(otherSource), Expression.Quote(joinPredicate)));
        }

        public static IQueryable<R> CrossApply<T, TJoin, R>(this IQueryable<T> source, Expression<Func<T, IQueryable<TJoin>>> otherSource, Expression<Func<T, TJoin, R>> newSourceFactory)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (otherSource is null)
            {
                throw new ArgumentNullException(nameof(otherSource));
            }

            if (newSourceFactory is null)
            {
                throw new ArgumentNullException(nameof(newSourceFactory));
            }

            return source.Provider.CreateQuery<R>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, Expression<Func<T, IQueryable<TJoin>>>, Expression<Func<T, TJoin, R>>, IQueryable<R>>(CrossApply).Method,
                    source.Expression, Expression.Quote(otherSource), Expression.Quote(newSourceFactory)));
        }

        public static IQueryable<R> OuterApply<T, TJoin, R>(this IQueryable<T> source, Expression<Func<T, IQueryable<TJoin>>> otherSource, Expression<Func<T, TJoin, R>> newSourceFactory)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (otherSource is null)
            {
                throw new ArgumentNullException(nameof(otherSource));
            }

            if (newSourceFactory is null)
            {
                throw new ArgumentNullException(nameof(newSourceFactory));
            }

            return source.Provider.CreateQuery<R>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, Expression<Func<T, IQueryable<TJoin>>>, Expression<Func<T, TJoin, R>>, IQueryable<R>>(OuterApply).Method,
                    source.Expression, Expression.Quote(otherSource), Expression.Quote(newSourceFactory)));
        }

        //public static IQueryable<IGrouping<T, R>> Having<T, R>(this IQueryable<IGrouping<T, R>> grouping, Expression<Func<IGrouping<T, R>, bool>> predicate)
        //{
        //    if (grouping is null)
        //    {
        //        throw new ArgumentNullException(nameof(grouping));
        //    }

        //    if (predicate is null)
        //    {
        //        throw new ArgumentNullException(nameof(predicate));
        //    }

        //    return grouping.Provider.CreateQuery<IGrouping<T, R>>(
        //        Expression.Call(
        //            null,
        //            new Func<IQueryable<IGrouping<T, R>>, Expression<Func<IGrouping<T, R>, bool>>, IQueryable<IGrouping<T, R>>>(Having).Method,
        //            grouping.Expression, Expression.Quote(predicate)));
        //}

        public static IQueryable<T> Having<T>(this IQueryable<T> grouping, Expression<Func<IGrouping<T, T>, bool>> predicate)
        {
            if (grouping is null)
            {
                throw new ArgumentNullException(nameof(grouping));
            }

            if (predicate is null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return grouping.Provider.CreateQuery<T>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, Expression<Func<IGrouping<T, T>, bool>>, IQueryable<T>>(Having).Method,
                    grouping.Expression, Expression.Quote(predicate)));
        }

        public static IQueryable<T> HavingOr<T>(this IQueryable<T> source, Expression<Func<IGrouping<T, T>, bool>> predicate)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (predicate is null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return source.Provider.CreateQuery<T>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, Expression<Func<IGrouping<T, T>, bool>>, IQueryable<T>>(HavingOr).Method,
                    source.Expression, Expression.Quote(predicate)));
        }

        public static IQueryable<T> Paging<T>(this IQueryable<T> source, int pageNumber, int pageSize)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Provider.CreateQuery<T>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, int, int, IQueryable<T>>(Paging).Method,
                    source.Expression, Expression.Constant(pageNumber), Expression.Constant(pageSize)));
        }

        public static IQueryable<T> UnionAll<T>(this IQueryable<T> source, IQueryable<T> otherSource)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (otherSource is null)
            {
                throw new ArgumentNullException(nameof(otherSource));
            }

            return source.Provider.CreateQuery<T>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, IQueryable<T>, IQueryable<T>>(UnionAll).Method,
                    source.Expression, otherSource.Expression));
        }

        public static IQueryable<T> RecursiveUnion<T>(this IQueryable<T> source, Expression<Func<IQueryable<T>, IQueryable<T>>> recursiveMember)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (recursiveMember is null)
            {
                throw new ArgumentNullException(nameof(recursiveMember));
            }

            return source.Provider.CreateQuery<T>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, Expression<Func<IQueryable<T>, IQueryable<T>>>, IQueryable<T>>(RecursiveUnion).Method,
                    source.Expression, Expression.Quote(recursiveMember)));
        }

        public static IQueryable<TKey> OrderByDesc<TSource, TKey>(this IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector)
        {
            // this method is for backward compatibility
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (keySelector is null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            return source.Provider.CreateQuery<TKey>(
                Expression.Call(
                    null,
                    new Func<IQueryable<TSource>, Expression<Func<TSource, TKey>>, IQueryable<TKey>>(OrderByDesc).Method,
                    source.Expression, Expression.Quote(keySelector)));
        }

        public static IQueryable<TSource> Top<TSource>(this IQueryable<TSource> source, int count)
        {
            // same as Take
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Provider.CreateQuery<TSource>(
                Expression.Call(
                    null,
                    new Func<IQueryable<TSource>, int, IQueryable<TSource>>(Queryable.Take).Method,
                    source.Expression, Expression.Constant(count)));
        }

        public static IQueryable<T> WhereOr<T>(this IQueryable<T> source, Expression<Func<T, bool>> predicate)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (predicate is null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return source.Provider.CreateQuery<T>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, Expression<Func<T, bool>>, IQueryable<T>>(WhereOr).Method,
                    source.Expression, Expression.Quote(predicate)));
        }

        public static IQueryable<R> FullOuterJoin<T, TJoin, R>(this IQueryable<T> source, IQueryable<TJoin> otherSource, Expression<Func<T, TJoin, R>> newSourceFactory, Expression<Func<R, bool>> joinPredicate)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (otherSource is null)
            {
                throw new ArgumentNullException(nameof(otherSource));
            }

            if (newSourceFactory is null)
            {
                throw new ArgumentNullException(nameof(newSourceFactory));
            }

            if (joinPredicate is null)
            {
                throw new ArgumentNullException(nameof(joinPredicate));
            }

            return source.Provider.CreateQuery<R>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, IQueryable<TJoin>, Expression<Func<T, TJoin, R>>, Expression<Func<R, bool>>, IQueryable<R>>(FullOuterJoin).Method,
                    source.Expression, otherSource.Expression, Expression.Quote(newSourceFactory), Expression.Quote(joinPredicate)));
        }

        public static IQueryable<T> FullOuterJoin<T>(this IQueryable<T> source, Expression<Func<T, T, bool>> joinPredicate)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (joinPredicate is null)
            {
                throw new ArgumentNullException(nameof(joinPredicate));
            }

            return source.Provider.CreateQuery<T>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, Expression<Func<T, T, bool>>, IQueryable<T>>(FullOuterJoin).Method,
                    source.Expression, Expression.Quote(joinPredicate)));
        }

        public static int Update<T>(this IQueryable<T> query, Expression<Func<T, T>> tableUpdateFields, Expression<Func<T, bool>> predicate)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            if (tableUpdateFields is null)
                throw new ArgumentNullException(nameof(tableUpdateFields));
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));
            var q = query.Provider.CreateQuery<T>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, Expression<Func<T, T>>, Expression<Func<T, bool>>, int>(Update).Method,
                    query.Expression, Expression.Quote(tableUpdateFields), Expression.Quote(predicate)));
            return query.Provider.Execute<int>(q.Expression);
        }

        public static int Update<T, R>(this IQueryable<T> query, Expression<Func<T, R>> tableSelection, Expression<Func<T, R>> tableUpdateFields, Expression<Func<T, bool>> predicate)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            if (tableSelection is null)
                throw new ArgumentNullException(nameof(tableSelection));
            if (tableUpdateFields is null)
                throw new ArgumentNullException(nameof(tableUpdateFields));
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));
            var q = query.Provider.CreateQuery<T>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, Expression<Func<T, R>>, Expression<Func<T, R>>, Expression<Func<T, bool>>, int>(Update).Method,
                    query.Expression, Expression.Quote(tableSelection), Expression.Quote(tableUpdateFields), Expression.Quote(predicate)));
            return query.Provider.Execute<int>(q.Expression);
        }

        public static int Delete<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));
            var q = query.Provider.CreateQuery<T>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, Expression<Func<T, bool>>, int>(Delete).Method,
                    query.Expression, Expression.Quote(predicate)));
            return query.Provider.Execute<int>(q.Expression);
        }

        public static int Delete<T, R>(this IQueryable<T> query, Expression<Func<T, R>> tableSelection, Expression<Func<T, bool>> predicate)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            if (tableSelection is null)
                throw new ArgumentNullException(nameof(tableSelection));
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));
            var q = query.Provider.CreateQuery<T>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, Expression<Func<T, R>>, Expression<Func<T, bool>>, int>(Delete).Method,
                    query.Expression, Expression.Quote(tableSelection), Expression.Quote(predicate)));
            return query.Provider.Execute<int>(q.Expression);
        }

        public static IQueryable<R> Select<R>(this IQueryProvider db, Expression<Func<R>> selector)
        {
            if (db is null)
                throw new ArgumentNullException(nameof(db));
            if (selector is null)
                throw new ArgumentNullException(nameof(selector));

            return db.CreateQuery<R>(
                Expression.Call(
                    typeof(QueryExtensions),
                    nameof(Select),
                    new Type[] { typeof(R) },
                    Expression.Constant(db),
                    selector));
        }

        public static int BulkInsert<T>(this IQueryable<T> query)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            return query.Provider.Execute<int>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, int>(BulkInsert).Method,
                    query.Expression));
        }


        /// <summary>
        ///     <para>
        ///         Starts a key-based update of <typeparamref name="T"/>: pick the columns to write with
        ///         <c>Set</c>, the rows to write them to with <c>Key</c>, and finish with <c>Execute</c>
        ///         or, to read columns back from the updated rows, <c>Output</c> then
        ///         <c>ExecuteDictionary</c>.
        ///     </para>
        ///     <para>
        ///         For an update whose values are expressions over the row itself, or which spans joined
        ///         tables, use <see cref="Update{T}(IQueryable{T}, Expression{Func{T, T}}, Expression{Func{T, bool}})"/>
        ///         instead. This API sets an entity's columns to values.
        ///     </para>
        /// </summary>
        public static UpdateSetters<T> UpdateEntity<T>(this IQueryProvider provider)
        {
            return new UpdateSetters<T>(provider);
        }
    }

    /// <summary>A column selector paired with the value to write to it.</summary>
    internal sealed class FieldValuePair
    {
        public FieldValuePair(LambdaExpression fieldSelector, LambdaExpression value)
        {
            this.FieldSelector = fieldSelector ?? throw new ArgumentNullException(nameof(fieldSelector));
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public LambdaExpression FieldSelector { get; }

        /// <summary>
        ///     The value, still as a lambda rather than a plain value, so that a captured variable stays
        ///     visible in the expression tree and can be rebound on a compiled-query cache hit.
        /// </summary>
        public LambdaExpression Value { get; }
    }

    /// <summary>
    ///     The first stage of <c>UpdateEntity&lt;T&gt;</c>: collecting the columns to write.
    /// </summary>
    public class UpdateSetters<T>
    {
        private readonly IQueryProvider provider;
        private readonly List<FieldValuePair> setters = new List<FieldValuePair>();

        internal UpdateSetters(IQueryProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>Assigns <paramref name="value"/> to the column <paramref name="fieldSelector"/> names.</summary>
        public UpdateSetters<T> Set<FT>(Expression<Func<T, FT>> fieldSelector, Expression<Func<FT>> value)
        {
            if (fieldSelector is null)
                throw new ArgumentNullException(nameof(fieldSelector));
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            this.setters.Add(new FieldValuePair(fieldSelector, value));
            return this;
        }

        /// <summary>Restricts the update to rows whose <paramref name="keySelector"/> column equals <paramref name="value"/>.</summary>
        public UpdateKey<T> Key<KT>(Expression<Func<T, KT>> keySelector, Expression<Func<KT>> value)
        {
            // The setters are snapshotted: the next stage must not change under a later Set on this one.
            return new UpdateKey<T>(this.provider, this.setters.ToList()).Key(keySelector, value);
        }
    }

    /// <summary>
    ///     An update with at least one key, so it can be executed.
    /// </summary>
    public class UpdateKey<T>
    {
        private readonly IQueryProvider provider;
        private readonly IReadOnlyList<FieldValuePair> setters;
        private readonly List<FieldValuePair> keys = new List<FieldValuePair>();

        internal UpdateKey(IQueryProvider provider, IReadOnlyList<FieldValuePair> setters)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.setters = setters ?? throw new ArgumentNullException(nameof(setters));
        }

        /// <summary>Adds a further key column; keys are combined with AND.</summary>
        public UpdateKey<T> Key<KT>(Expression<Func<T, KT>> keySelector, Expression<Func<KT>> value)
        {
            if (keySelector is null)
                throw new ArgumentNullException(nameof(keySelector));
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            this.keys.Add(new FieldValuePair(keySelector, value));
            return this;
        }

        /// <summary>Asks for <paramref name="outputSelector"/>'s column to be returned from the updated rows.</summary>
        public UpdateOutput<T> Output<KT>(Expression<Func<T, KT>> outputSelector)
        {
            return new UpdateOutput<T>(this.provider, this.setters, this.keys.ToList()).Output(outputSelector);
        }

        /// <summary>Runs the update and returns the number of rows affected.</summary>
        public int Execute()
        {
            // No output clause, so the statement yields the affected-row count.
            var updateEntityExpression = UpdateEntityExpressionFactory.Create(
                typeof(int), typeof(T), this.setters, this.keys, outputs: null);
            return this.provider.Execute<int>(updateEntityExpression);
        }
    }

    /// <summary>
    ///     An update that returns columns from the rows it wrote.
    /// </summary>
    public class UpdateOutput<T>
    {
        private readonly IQueryProvider provider;
        private readonly IReadOnlyList<FieldValuePair> setters;
        private readonly IReadOnlyList<FieldValuePair> keys;
        private readonly List<Expression> outputs = new List<Expression>();

        internal UpdateOutput(IQueryProvider provider, IReadOnlyList<FieldValuePair> setters, IReadOnlyList<FieldValuePair> keys)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.setters = setters ?? throw new ArgumentNullException(nameof(setters));
            this.keys = keys ?? throw new ArgumentNullException(nameof(keys));
        }

        /// <summary>
        ///     <para>
        ///         Adds a further column to return. Generic in the column type rather than taking
        ///         <c>Expression&lt;Func&lt;T, object&gt;&gt;</c>: an <c>object</c> selector boxes a
        ///         value-type column behind a <c>Convert</c> node, which hides the member access the
        ///         output alias is taken from.
        ///     </para>
        /// </summary>
        public UpdateOutput<T> Output<KT>(Expression<Func<T, KT>> outputSelector)
        {
            if (outputSelector is null)
                throw new ArgumentNullException(nameof(outputSelector));

            this.outputs.Add(outputSelector);
            return this;
        }

        /// <summary>
        ///     Runs the update and returns one dictionary per updated row, keyed by output column name
        ///     (matched case-insensitively), with a <c>NULL</c> column coming back as <c>null</c>.
        /// </summary>
        public IReadOnlyList<IReadOnlyDictionary<string, object>> ExecuteDictionary()
        {
            // Typed as the queryable it is handed to, not as the collection the caller gets back:
            // IQueryable<T>.Expression.Type must be assignable to IQueryable<T>, and the materializer
            // reads the element type from here to decide what to build.
            var updateEntityExpression = UpdateEntityExpressionFactory.Create(
                typeof(IQueryable<Dictionary<string, object>>), typeof(T), this.setters, this.keys, this.outputs);
            var query = this.provider.CreateQuery<Dictionary<string, object>>(updateEntityExpression);
            return new List<Dictionary<string, object>>(query);
        }
    }

    /// <summary>
    ///     Turns what the fluent stages collected into the <see cref="UpdateEntityExpression"/> the
    ///     converter consumes.
    /// </summary>
    internal static class UpdateEntityExpressionFactory
    {
        public static UpdateEntityExpression Create(Type resultType, Type entityType, IReadOnlyList<FieldValuePair> setters, IReadOnlyList<FieldValuePair> keys, IReadOnlyList<Expression> outputs)
        {
            var query = new QueryRootExpression(entityType);
            var setterLambda = CreateSetterLambda(entityType, setters);

            var keyExpressions = new List<Expression>(keys.Count);
            foreach (var key in keys)
            {
                // Equality is the actual semantics here, unlike the SET list.
                var equalExpression = Expression.Equal(key.FieldSelector.Body, key.Value.Body);
                keyExpressions.Add(Expression.Lambda(equalExpression, key.FieldSelector.Parameters[0]));
            }

            // An update without an output clause carries an empty list rather than null, so that the
            // converter can treat all of them alike.
            var outputFields = new AggregatedListExpression(outputs ?? (IReadOnlyList<Expression>)new Expression[0]);
            return new UpdateEntityExpression(resultType, query, setterLambda, new AggregatedListExpression(keyExpressions), outputFields);
        }

        /// <summary>
        ///     <para>
        ///         Builds the SET list as <c>x =&gt; new T { Member = value, ... }</c> — the same shape the
        ///         query-based <c>Update</c> API passes, so the converter resolves columns through the
        ///         entity's mapping metadata rather than inferring them from an already-converted
        ///         expression.
        ///     </para>
        ///     <para>
        ///         The field selector is only mined for the member it names; its body is never re-hosted in
        ///         the tree, so each <c>Set</c> call's own lambda parameter simply falls away. The value
        ///         expression, by contrast, is placed in the tree verbatim, which is what keeps a captured
        ///         variable visible for cache-hit rebinding.
        ///     </para>
        /// </summary>
        private static LambdaExpression CreateSetterLambda(Type entityType, IReadOnlyList<FieldValuePair> setters)
        {
            if (setters.Count == 0)
                throw new InvalidOperationException("At least one Set call is required to update an entity.");
            if (entityType.GetConstructor(Type.EmptyTypes) is null)
                throw new InvalidOperationException($"Entity '{entityType.Name}' needs a parameterless constructor to be updated through UpdateEntity.");

            var bindings = new List<MemberBinding>(setters.Count);
            foreach (var setter in setters)
            {
                var member = GetSelectedMember(setter.FieldSelector);
                try
                {
                    bindings.Add(Expression.Bind(member, setter.Value.Body));
                }
                catch (ArgumentException ex)
                {
                    // Most often a get-only member: a calculated property is not a column and cannot be set.
                    throw new InvalidOperationException(
                        $"'{entityType.Name}.{member.Name}' cannot be assigned by Set. A calculated or read-only member is not a stored column.", ex);
                }
            }

            return Expression.Lambda(
                Expression.MemberInit(Expression.New(entityType), bindings),
                Expression.Parameter(entityType, "x"));
        }

        private static MemberInfo GetSelectedMember(LambdaExpression selector)
        {
            var body = selector.Body;
            while (body is UnaryExpression unary &&
                   (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            {
                body = unary.Operand;
            }

            return (body as MemberExpression)?.Member
                ?? throw new ArgumentException($"Expected a member selector such as 'x => x.LastName', but got '{selector.Body}'.", nameof(selector));
        }
    }
}
