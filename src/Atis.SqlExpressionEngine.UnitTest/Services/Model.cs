using Atis.Orm;
using Atis.Orm.Annotations;
using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;
using Atis.SqlExpressionEngine.UnitTest.Metadata;
using Atis.SqlExpressionEngine.UnitTest.Preprocessors;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

using Atis.Orm.Metadata;
using Atis.Orm.Abstractions;
using Atis.Orm.Functions;
namespace Atis.SqlExpressionEngine.UnitTest.Services
{
    internal class Model : Atis.SqlExpressionEngine.Services.Model, ISqlFunctionMetadataProvider
    {
        private readonly EntityMetadataBuilder entityMetadataBuilder;
        private readonly ConcurrentDictionary<Type, EntityMetadata> metadataCache = new ConcurrentDictionary<Type, EntityMetadata>();
        private readonly SqlFunctionAnnotationReader sqlFunctionAnnotationReader = new SqlFunctionAnnotationReader();

        public Model(IReflectionService reflectionService)
        {
            this.entityMetadataBuilder = new EntityMetadataBuilder(reflectionService);
        }

        public override EntityMetadata GetRequiredEntity(Type type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (this.metadataCache.TryGetValue(type, out var metadata))
            {
                return metadata;
            }

            metadata = this.entityMetadataBuilder.Build(type);
            this.metadataCache.TryAdd(type, metadata);
            return metadata;
        }

        public bool TryGetSqlFunction(MethodInfo method, out SqlFunctionMetadata metadata)
            => this.sqlFunctionAnnotationReader.TryGetSqlFunction(method, out metadata);
    }
}
