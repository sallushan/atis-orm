//using System;
//using System.Collections.Generic;
//using System.Data;
//using System.Data.Common;
//using System.Diagnostics;
//using System.Linq;
//using System.Linq.Expressions;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Reflection;
//using Atis.SqlExpressionEngine.Abstractions;

//namespace Atis.Orm.SqlServer
//{
//    /// <summary>
//    /// 
//    /// </summary>
//    public class SqlDataAdapter : IDatabaseAdapter
//    {
//        private static MethodInfo _openExecuteEnumerable;
//        private static MethodInfo OpenExecuteEnumerable =>
//            _openExecuteEnumerable ??
//            (_openExecuteEnumerable = typeof(SqlDataAdapter).GetMethod(nameof(ExecuteEnumerable)));
        
//        private static MethodInfo _openExecuteEnumerableAsync;
//        private static MethodInfo OpenExecuteEnumerableAsync =>
//            _openExecuteEnumerableAsync ??
//            (_openExecuteEnumerableAsync = typeof(SqlDataAdapter).GetMethod(nameof(ExecuteEnumerableAsync)));

//        //private DbConnection givenConnection;
//        //private DbConnection transactionConnection;
//        //private DbTransaction transaction;
//        private readonly IReflectionService reflectionService;
//        private readonly IDbCommunication dbCommunication;

//        public SqlDataAdapter(IReflectionService reflectionService, IDbCommunication dbCommunication)
//        {
//            this.reflectionService = reflectionService ?? throw new ArgumentNullException(nameof(reflectionService));
//            this.dbCommunication = dbCommunication ?? throw new ArgumentNullException(nameof(dbCommunication));
//        }

//        //public TResult ExecuteQuery<TResult>(IExecutionContext executionContext)
//        //{
//        //    if (executionContext.ElementFactory == null)
//        //        throw new ArgumentNullException(nameof(executionContext.ElementFactory));

//        //    return this.Execute<TResult>(executionContext);
//        //}

//        //public Task<TResult> ExecuteQueryAsync<TResult>(IExecutionContext executionContext, CancellationToken cancellationToken)
//        //{
//        //    if (executionContext.ElementFactory == null)
//        //        throw new ArgumentNullException(nameof(executionContext.ElementFactory));

//        //    return ExecuteAsync<TResult>(executionContext, cancellationToken);
//        //}

//        public virtual T Execute<T>(IExecutionContext executionContext)
//        {
//            if (!this.reflectionService.IsQueryableType(typeof(T)))
//                return this.ExecuteEnumerable<T>(executionContext).FirstOrDefault();
            
//            var elementType = this.reflectionService.GetElementType(typeof(T));
//            var closedExecuteEnumerable = OpenExecuteEnumerable.MakeGenericMethod(elementType);
//            return (T)closedExecuteEnumerable.Invoke(this, new object[] { executionContext });
//        }
        
//        public virtual async Task<T> ExecuteAsync<T>(IExecutionContext executionContext /*string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> createInstance*/, CancellationToken cancellationToken = default)
//        {
//            if (!this.reflectionService.IsQueryableAsyncType(typeof(T)))
//            {
//                var enumerableAsync = ExecuteEnumerableAsync<T>(executionContext);
//                var enumerator = enumerableAsync.GetAsyncEnumerator(cancellationToken);
//                try
//                {
//                    if (await enumerator.MoveNextAsync())
//                        return enumerator.Current;
//                    else
//                        return default;
//                }
//                finally
//                {
//                    await enumerator.DisposeAsync();
//                }
//            }

//            var elementType = this.reflectionService.GetElementType(typeof(T));
//            var closedExecuteEnumerableAsync = OpenExecuteEnumerableAsync.MakeGenericMethod(elementType);
//            var rawTask = (Task)closedExecuteEnumerableAsync.Invoke(this, new object[] { executionContext });
//            var result  = await GetTaskResult(rawTask);
//            return (T)result;
//        }
        
//        // Helper to get the result of a Task<T> when T is not known at compile time
//        private static async Task<object> GetTaskResult(Task task)
//        {
//            await task; // Await the task to ensure it completes
//            Type taskType = task.GetType();
//            if (taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>))
//            {
//                // Get the Result property using reflection
//                PropertyInfo resultProperty = taskType.GetProperty("Result");
//                if (resultProperty != null)
//                {
//                    return resultProperty.GetValue(task);
//                }
//            }
//            return null; // For non-generic Task or if Result property isn't found
//        }
        
//        public virtual T ExecuteScalar<T>(string query, DbParameter[] dbParameters)
//        {
//            return this.ExecuteNonReader<T>(query, dbParameters, scalar: true);
//        }

//        public virtual int ExecuteNonQuery(string query, DbParameter[] dbParameters)
//        {
//            return this.ExecuteNonReader<int>(query, dbParameters, scalar: false);
//        }
        
//        public virtual Task<T> ExecuteScalarAsync<T>(string query, DbParameter[] dbParameters, CancellationToken cancellationToken = default)
//        {
//            return this.ExecuteNonReaderAsync<T>(query, dbParameters, scalar: true, cancellationToken);
//        }

//        public virtual Task<int> ExecuteNonQueryAsync(string query, DbParameter[] dbParameters, CancellationToken cancellationToken = default)
//        {
//            return this.ExecuteNonReaderAsync<int>(query, dbParameters, scalar: false, cancellationToken);
//        }

//        public virtual IEnumerable<T> ExecuteEnumerable<T>(IExecutionContext execution /*string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> createInstance*/)
//        {
//            if (execution is null)
//                throw new ArgumentNullException(nameof(execution));

//            //var availableConnection = this.givenConnection ?? this.transactionConnection;
//            //var availableTrans = availableConnection != null ? this.transaction : null;

//            //DbConnection dbConn;
//            //bool shouldDisposeConnection;
//            //if (availableConnection != null)
//            //{
//            //    dbConn = availableConnection;
//            //    shouldDisposeConnection = false;
//            //}
//            //else
//            //{
//            //    dbConn = this.CreateNewConnection();
//            //    shouldDisposeConnection = true;
//            //}

//            var enumerable = new DbEnumerable<T>(execution, dbCommunication);
//            return enumerable;
//        }
        
//        public virtual IAsyncEnumerable<T> ExecuteEnumerableAsync<T>(IExecutionContext executionContext /*string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> createInstance*/)
//        {
//            if (executionContext is null)
//                throw new ArgumentNullException(nameof(executionContext));

//            //var availableConnection = this.givenConnection ?? this.transactionConnection;
//            //var availableTrans = availableConnection != null ? this.transaction : null;

//            //DbConnection dbConn;
//            //bool shouldDisposeConnection;
//            //if (availableConnection != null)
//            //{
//            //    dbConn = availableConnection;
//            //    shouldDisposeConnection = false;
//            //}
//            //else
//            //{
//            //    dbConn = this.CreateNewConnection();
//            //    shouldDisposeConnection = true;
//            //}

//            var enumerable = new DbAsyncEnumerable<T>(executionContext, this.dbCommunication);
//            return enumerable;
//        }

//        protected virtual T ExecuteNonReader<T>(string query, DbParameter[] dbParameters, bool scalar)
//            => ExecuteNonReaderInternal<T>(query, dbParameters, scalar, false).GetAwaiter().GetResult();

//        protected virtual Task<T> ExecuteNonReaderAsync<T>(string query, DbParameter[] dbParameters, bool scalar, CancellationToken cancellationToken = default)
//            => ExecuteNonReaderInternal<T>(query, dbParameters, scalar, true, cancellationToken);
        
//        private async Task<T> ExecuteNonReaderInternal<T>(string query, DbParameter[] dbParameters, bool scalar, bool async, CancellationToken cancellationToken = default)
//        {
//            if (string.IsNullOrWhiteSpace(query))
//                throw new ArgumentNullException(nameof(query));

//            var dbCommand = this.dbCommunication.CreateCommand(query, dbParameters, CommandType.Text);
//            return (T)await this.dbCommunication.ExecuteScalarCommandAsync<T>(dbCommand, cancellationToken);
//            //var conn = this.givenConnection ?? this.transactionConnection;
//            //var trans = conn != null ? this.transaction : null;

//            //var createdNewConnection = false;
//            //if (conn is null)
//            //{
//            //    conn = this.CreateNewConnection();
//            //    createdNewConnection = true;
//            //}

//            //try
//            //{
//            //    var connectionWasOpened = false;
//            //    if (conn.State == ConnectionState.Closed)
//            //    {
//            //        if (async)
//            //            await conn.OpenAsync(cancellationToken);
//            //        else
//            //            conn.Open();
//            //        connectionWasOpened = true;
//            //    }

//            //    try
//            //    {
//            //        using (var cmd = conn.CreateCommand())
//            //        {
//            //            cmd.CommandText = query;
//            //            cmd.CommandType = CommandType.Text;
//            //            cmd.Transaction = trans;
//            //            if (dbParameters != null)
//            //                cmd.Parameters.AddRange(dbParameters);

//            //            if (scalar)
//            //            {
//            //                object result;
//            //                if (async)
//            //                    result = await cmd.ExecuteScalarAsync(cancellationToken);
//            //                else
//            //                    result = cmd.ExecuteScalar();
//            //                if (result == null || result == DBNull.Value)
//            //                    return default;
//            //                return (T)result;
//            //            }
//            //            else
//            //            {
//            //                int rowsAffected;
//            //                if (async)
//            //                    rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
//            //                else
//            //                    rowsAffected = cmd.ExecuteNonQuery();
//            //                return (T)(object)rowsAffected;
//            //            }
//            //        }
//            //    }
//            //    finally
//            //    {
//            //        if (connectionWasOpened)
//            //            conn.Close();
//            //    }
//            //}
//            //finally
//            //{
//            //    if (createdNewConnection)
//            //        conn.Dispose();
//            //}
//        }

//        //private void CloseConnection(DbConnection dbConn)
//        //{
//        //    throw new NotImplementedException();
//        //}

//        //private DbConnection CreateNewConnection()
//        //{
//        //    throw new NotImplementedException();
//        //}

//        //private void OpenConnection(object dbConn)
//        //{
//        //    throw new NotImplementedException();
//        //}
//    }
//}
