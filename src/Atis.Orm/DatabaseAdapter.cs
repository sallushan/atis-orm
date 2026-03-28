using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Atis.SqlExpressionEngine.Abstractions;
// ReSharper disable InvertIf

namespace Atis.Orm
{
    public class DatabaseAdapter : IDatabaseAdapter
    {
        private static MethodInfo _openExecuteEnumerable;
        private static MethodInfo OpenExecuteEnumerable =>
            _openExecuteEnumerable ??
            (_openExecuteEnumerable = typeof(DatabaseAdapter).GetMethod(nameof(ExecuteEnumerable), BindingFlags.Public | BindingFlags.Instance));

        private static MethodInfo _openExecuteEnumerableAsync;
        private static MethodInfo OpenExecuteEnumerableAsync =>
            _openExecuteEnumerableAsync ??
            (_openExecuteEnumerableAsync = typeof(DatabaseAdapter).GetMethod(nameof(ExecuteEnumerableAsync), BindingFlags.Public | BindingFlags.Instance));

        private static readonly MethodInfo OpenExecuteInternalAsync = typeof(DatabaseAdapter)
            .GetMethod(nameof(ExecuteInternalAsync), BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly IReflectionService reflectionService;
        private readonly IDbCommunication dbCommunication;

        public DatabaseAdapter(IReflectionService reflectionService, IDbCommunication db)
        {
            this.reflectionService = reflectionService ?? throw new ArgumentNullException(nameof(reflectionService));
            this.dbCommunication = db ?? throw new ArgumentNullException(nameof(db));
        }

        public T Execute<T>(string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory)
        {
            if (!this.reflectionService.IsQueryableType(typeof(T)))
                return this.ExecuteEnumerable<T>(query, dbParameters, elementFactory).FirstOrDefault();

            var elementType = this.reflectionService.GetElementType(typeof(T));
            var closedExecuteEnumerable = OpenExecuteEnumerable.MakeGenericMethod(elementType);
            return (T)closedExecuteEnumerable.Invoke(this, new object[] { query, dbParameters, elementFactory });
        }        

        public TResult ExecuteAsync<TResult>(string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory, CancellationToken cancellationToken = default)
        {
            if (this.reflectionService.IsQueryableAsyncType(typeof(TResult)))
            {
                // Case 1: TResult is IAsyncEnumerable<X>
                var elementType = this.reflectionService.GetElementType(typeof(TResult));
                var method = OpenExecuteEnumerableAsync.MakeGenericMethod(elementType);
                var result = method.Invoke(this, new object[] { query, dbParameters, elementFactory });
                return (TResult)result;
            }
            else
            {
                // Case 2: TResult is Task<X>/ Case 2: TResult is Task<X>
                if (!typeof(TResult).IsGenericType || typeof(TResult).GetGenericTypeDefinition() != typeof(Task<>))
                {
                    throw new InvalidOperationException($"Expected TResult to be Task<T> or IAsyncEnumerable<T>, but got {typeof(TResult).Name}");
                }

                var elementType = typeof(TResult).GetGenericArguments()[0];
                var method = OpenExecuteInternalAsync.MakeGenericMethod(elementType);
                var result = method.Invoke(this, new object[] { query, dbParameters, elementFactory, cancellationToken });
                return (TResult)result;
            }
        }


        // Helper method 2: Returns Task<T>
        private async Task<T> ExecuteInternalAsync<T>(string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory, CancellationToken cancellationToken)
        {
            var enumerableAsync = ExecuteEnumerableAsync<T>(query, dbParameters, elementFactory);
            var enumerator = enumerableAsync.GetAsyncEnumerator(cancellationToken);
            try
            {
                if (await enumerator.MoveNextAsync())
                    return enumerator.Current;
                else
                    return default;
            }
            finally
            {
                await enumerator.DisposeAsync();
            }
        }

        public IEnumerable<T> ExecuteEnumerable<T>(string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory)
        {
            var enumerable = new DbEnumerable<T>(query, dbParameters, elementFactory, dbCommunication);
            return enumerable;
        }
        
        public IAsyncEnumerable<T> ExecuteEnumerableAsync<T>(string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory)
        {
            var enumerable = new DbAsyncEnumerable<T>(query, dbParameters, elementFactory, this.dbCommunication);
            return enumerable;
        }

        public int ExecuteNonQuery(string query, IEnumerable<DbParameter> dbParameters)
        {
            var dbCommand = this.dbCommunication.CreateCommand(query, dbParameters, CommandType.Text);
            return this.dbCommunication.ExecuteNonQueryCommand(dbCommand);
        }

        public Task<int> ExecuteNonQueryAsync(string query, IEnumerable<DbParameter> dbParameters, CancellationToken cancellationToken = default)
        {
            var dbCommand = this.dbCommunication.CreateCommand(query, dbParameters, CommandType.Text);
            return this.dbCommunication.ExecuteNonQueryCommandAsync(dbCommand, cancellationToken);
        }
    }
}