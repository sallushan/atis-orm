using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Atis.Orm
{
    public class EntityBuilder<T>
    {
        private readonly MutableEntityMetadata _mutable;

        internal EntityBuilder(MutableEntityMetadata mutable)
        {
            _mutable = mutable;
        }

        public EntityBuilder<T> Column(Expression<Func<T, object>> property, string columnName)
        {
            var memberName = ((property.Body as MemberExpression)
                             ?? (MemberExpression)((UnaryExpression)property.Body).Operand).Member.Name;
            var existing = _mutable.SqlColumns.FirstOrDefault(x => x.ModelPropertyName == memberName);
            var isPrimaryKey = existing?.IsPrimaryKey ?? false;
            if (existing != null)
                _mutable.SqlColumns.Remove(existing);
            _mutable.SqlColumns.Add(new TableColumn(columnName, memberName, isPrimaryKey));
            return this;
        }

        internal EntityMetadata Build() => _mutable.Build();
    }
}
