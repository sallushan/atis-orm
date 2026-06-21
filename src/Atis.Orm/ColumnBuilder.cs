using System;
using System.Linq.Expressions;

namespace Atis.Orm
{
    public class ColumnBuilder<T>
    {
        private readonly MutableEntityMetadata _mutable;
        private readonly string _propertyName;

        internal ColumnBuilder(MutableEntityMetadata mutable, string propertyName)
        {
            _mutable = mutable ?? throw new ArgumentNullException(nameof(mutable));
            _propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        }

        public ColumnBuilder<T> SetColumnName(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentNullException(nameof(columnName));
            _mutable.GetOrAddColumn(_propertyName).DatabaseColumnName = columnName;
            return this;
        }

        public ColumnBuilder<T> MarkAsKey()
        {
            _mutable.GetOrAddColumn(_propertyName).IsPrimaryKey = true;
            return this;
        }
    }
}