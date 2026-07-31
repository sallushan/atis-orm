using System;
using System.Linq.Expressions;
using System.Linq;
using System.Collections.Generic;
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


        public static UpdateSetters<T> UpdateEntity<T>(this IQueryProvider provider)
        {
            return new UpdateSetters<T>(provider);
        }

        public static UpdateSetters<T> Set<T, FT>(this UpdateSetters<T> updateSetters, Expression<Func<T, FT>> fieldSelector, Expression<Func<FT>> value)
        {
            updateSetters.AddSetExpression(fieldSelector, value);
            return updateSetters;
        }

        public static UpdateKey<T> Key<T, KT>(this UpdateSetters<T> updateSetters, Expression<Func<T, KT>> keySelector, Expression<Func<KT>> value)
        {
            var updateKey = new UpdateKey<T>(updateSetters.Provider, updateSetters.SetExpressions);
            updateKey.AddKeyExpression(keySelector, value);
            return updateKey;
        }

        public static UpdateKey<T> Key<T, KT>(this UpdateKey<T> updateKey, Expression<Func<T, KT>> keySelector, Expression<Func<KT>> value)
        {
            updateKey.AddKeyExpression(keySelector, value);
            return updateKey;
        }

        public static UpdateOutput<T> Output<T, KT>(this UpdateKey<T> updateKey, Expression<Func<T, KT>> outputSelector)
        {
            if (updateKey == null)
                throw new ArgumentNullException(nameof(updateKey));
            if (outputSelector == null)
                throw new ArgumentNullException(nameof(outputSelector));
            var updateOutput = new UpdateOutput<T>(updateKey.Provider, updateKey.SetExpressions, updateKey.KeyExpressions);
            updateOutput.AddOutputExpression(outputSelector);
            return updateOutput;
        }

        public static UpdateOutput<T> Output<T>(this UpdateOutput<T> updateOutput, Expression<Func<T, object>> outputSelector)
        {
            if (updateOutput == null)
                throw new ArgumentNullException(nameof(updateOutput));
            if (outputSelector == null)
                throw new ArgumentNullException(nameof(outputSelector));
            updateOutput.AddOutputExpression(outputSelector);
            return updateOutput;
        }

        public static int Execute<T>(this UpdateKey<T> updateKey)
        {
            if (updateKey == null)
                throw new ArgumentNullException(nameof(updateKey));
            var updateEntityExpression = CreateUpdateEntityExpression(outputType: null, typeof(T), updateKey.SetExpressions, updateKey.KeyExpressions, outputs: null);
            return updateKey.Provider.Execute<int>(updateEntityExpression);
        }

        public static IReadOnlyList<IReadOnlyDictionary<string, object>> ExecuteDictionary<T>(this UpdateOutput<T> updateOutput)
        {
            if (updateOutput == null)
                throw new ArgumentNullException(nameof(updateOutput));
            var setters = updateOutput.SetExpressions;
            var keys = updateOutput.KeyExpressions;
            var outputs = updateOutput.OutputExpressions;
            var typeOfQuery = typeof(T);
            // TODO: check if the type should be only Dictionary<string, object>
            UpdateEntityExpression updateEntityExpression = CreateUpdateEntityExpression(typeof(List<Dictionary<string, object>>), typeOfQuery, setters, keys, outputs);
            var q = updateOutput.Provider.CreateQuery<Dictionary<string, object>>(updateEntityExpression);
            return new List<Dictionary<string, object>>(q);
        }

        private static UpdateEntityExpression CreateUpdateEntityExpression(Type outputType, Type typeOfQuery, IReadOnlyList<FieldValuePair> setters, IReadOnlyList<FieldValuePair> keys, IReadOnlyList<Expression> outputs)
        {
            var query = new QueryRootExpression(typeOfQuery);
            var setExpressions = new List<Expression>(setters.Count);
            foreach (var setter in setters)
            {
                var equalExpression = Expression.Equal(setter.FieldSelector.Body, setter.Value.Body);
                var lambda = Expression.Lambda(equalExpression, setter.FieldSelector.Parameters[0]);
                setExpressions.Add(lambda);
            }
            var keyExpressions = new List<Expression>(keys.Count);
            foreach (var key in keys)
            {
                var equalExpression = Expression.Equal(key.FieldSelector.Body, key.Value.Body);
                var lambda = Expression.Lambda(equalExpression, key.FieldSelector.Parameters[0]);
                keyExpressions.Add(lambda);
            }
            // An update without an output clause carries an empty list rather than null, so that all three
            // lists can be handled uniformly by the converter (see UpdateEntityExpressionConverter).
            var outputFields = new AggregatedListExpression(outputs ?? (IReadOnlyList<Expression>)new Expression[0]);
            var setterAggregated = new AggregatedListExpression(setExpressions);
            var keyAggregated = new AggregatedListExpression(keyExpressions);
            var updateEntityExpression = new UpdateEntityExpression(outputType, query, setterAggregated, keyAggregated, outputFields);
            return updateEntityExpression;
        }

        private static string GetMemberName(Expression expression, string paramName)
        {
            var memberExpression = expression as MemberExpression
                                   ?? (expression is UnaryExpression unary ? unary.Operand as MemberExpression : null);

            if (memberExpression == null)
                throw new ArgumentException("Expression must be a simple member access.", paramName);

            return memberExpression.Member.Name;
        }
    }

    public class FieldValuePair
    {
        public LambdaExpression FieldSelector { get; }
        public LambdaExpression Value { get; }
        public FieldValuePair(LambdaExpression fieldSelector, LambdaExpression value)
        {
            this.FieldSelector = fieldSelector ?? throw new ArgumentNullException(nameof(fieldSelector));
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    public class UpdateSetters<T>
    {
        public IQueryProvider Provider { get; }

        public UpdateSetters(IQueryProvider provider)
        {
            this.Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private readonly List<FieldValuePair> setExpressions = new List<FieldValuePair>();
        public IReadOnlyList<FieldValuePair> SetExpressions => setExpressions.AsReadOnly();

        internal void AddSetExpression<FT>(Expression<Func<T, FT>> fieldSelector, Expression<Func<FT>> value)
        {
            this.setExpressions.Add(new FieldValuePair(fieldSelector, value));
        }
    }

    public class UpdateKey<T>
    {
        public UpdateKey(IQueryProvider provider, IReadOnlyList<FieldValuePair> setExpressions)
        {
            this.Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.SetExpressions = setExpressions ?? throw new ArgumentNullException(nameof(setExpressions));
        }

        public IQueryProvider Provider { get; }
        public IReadOnlyList<FieldValuePair> SetExpressions { get; }

        private readonly List<FieldValuePair> keyExpressions = new List<FieldValuePair>();
        public IReadOnlyList<FieldValuePair> KeyExpressions => keyExpressions.AsReadOnly();

        
        internal void AddKeyExpression<KT>(Expression<Func<T, KT>> keySelector, Expression<Func<KT>> value)
        {
            this.keyExpressions.Add(new FieldValuePair(keySelector, value));
        }
    }

    public class UpdateOutput<T>
    {
        public UpdateOutput(IQueryProvider provider, IReadOnlyList<FieldValuePair> setExpressions, IReadOnlyList<FieldValuePair> keyExpressions)
        {
            this.Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.SetExpressions = setExpressions ?? throw new ArgumentNullException(nameof(setExpressions));
            this.KeyExpressions = keyExpressions ?? throw new ArgumentNullException(nameof(keyExpressions));
        }
        public IQueryProvider Provider { get; }
        public IReadOnlyList<FieldValuePair> SetExpressions { get; }
        public IReadOnlyList<FieldValuePair> KeyExpressions { get; }
        private readonly List<LambdaExpression> outputExpressions = new List<LambdaExpression>();
        public IReadOnlyList<LambdaExpression> OutputExpressions => outputExpressions.AsReadOnly();
        internal void AddOutputExpression<KT>(Expression<Func<T, KT>> outputSelector)
        {
            this.outputExpressions.Add(outputSelector);
        }
    }
}
