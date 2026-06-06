using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm
{
    public class ModelBuilder
    {
        private readonly IEntityMetadataBuilder _entityMetadataBuilder;
        private readonly IOrmModel _ormModel;
        private readonly List<Func<EntityMetadata>> _builders = new List<Func<EntityMetadata>>();

        public bool AllClrProperties { get; set; } = false;

        public ModelBuilder(IEntityMetadataBuilder entityMetadataBuilder, IOrmModel ormModel)
        {
            _entityMetadataBuilder = entityMetadataBuilder;
            _ormModel = ormModel;
        }

        public EntityBuilder<T> Entity<T>(string tableName = null)
        {
            var seeded = _entityMetadataBuilder.Build(typeof(T));
            var mutable = new MutableEntityMetadata(seeded);
            if (tableName != null)
            {
                var sqlTable = new SqlTable(tableName, mutable.Table?.Schema, mutable.Table?.Database, mutable.Table?.Server);
                mutable.Table = sqlTable;
            }
            var builder = new EntityBuilder<T>(mutable);
            _builders.Add(() => builder.Build());
            return builder;
        }

        internal void Build()
        {
            foreach (var build in _builders)
            {
                var metadata = build();
                _ormModel.Add(metadata);
            }
        }
    }
}
