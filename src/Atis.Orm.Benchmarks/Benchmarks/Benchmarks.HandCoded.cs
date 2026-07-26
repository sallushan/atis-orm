using System.ComponentModel;
using System.Data;
using Atis.Orm.Benchmarks.Helpers;
using Atis.Orm.Benchmarks.Model;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.SqlClient;

namespace Atis.Orm.Benchmarks.Benchmarks
{
    /// <summary>
    /// The floor: a prepared <see cref="SqlCommand"/> with manual hydration. Everything an ORM adds
    /// is measured against this. Ported from Dapper's suite
    /// (benchmarks/Dapper.Tests.Performance/Benchmarks.HandCoded.cs), minus the DataTable variant.
    /// </summary>
    [Description("Hand Coded")]
    [BenchmarkCategory(Scenarios.ByPk)]
    public class HandCodedBenchmarks : BenchmarkBase
    {
        private SqlCommand _postCommand;
        private SqlParameter _idParam;

        [GlobalSetup]
        public void Setup()
        {
            BaseSetup();
            _postCommand = new SqlCommand("select Top 1 * from Posts where Id = @Id", _connection);
            _idParam = _postCommand.Parameters.Add("@Id", SqlDbType.Int);
            _postCommand.Prepare();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _postCommand?.Dispose();
            BaseCleanup();
        }

        [Benchmark(Description = "SqlCommand", Baseline = true)]
        public Post SqlCommand()
        {
            Step();
            _idParam.Value = i;

            using var reader = _postCommand.ExecuteReader(CommandBehavior.SingleResult | CommandBehavior.SingleRow);
            reader.Read();
            return new Post
            {
                Id = reader.GetInt32(0),
                Text = reader.GetNullableString(1),
                CreationDate = reader.GetDateTime(2),
                LastChangeDate = reader.GetDateTime(3),

                Counter1 = reader.GetNullableValue<int>(4),
                Counter2 = reader.GetNullableValue<int>(5),
                Counter3 = reader.GetNullableValue<int>(6),
                Counter4 = reader.GetNullableValue<int>(7),
                Counter5 = reader.GetNullableValue<int>(8),
                Counter6 = reader.GetNullableValue<int>(9),
                Counter7 = reader.GetNullableValue<int>(10),
                Counter8 = reader.GetNullableValue<int>(11),
                Counter9 = reader.GetNullableValue<int>(12)
            };
        }
    }
}
