using Atis.Orm.Services;
using System;
using System.Linq.Expressions;

namespace Atis.Orm.Metadata
{
    /// <summary>
    ///     Fluent counterpart of <see cref="Atis.Orm.Annotations.ValueObjectAttribute"/> — maps members
    ///     of a value-object-typed property onto columns of the containing entity's own table.
    /// </summary>
    public class ValueObjectBuilder<T, TVo>
    {
        private readonly MutableEntityMetadata _mutable;
        private readonly string _outerPropertyName;

        internal ValueObjectBuilder(MutableEntityMetadata mutable, string outerPropertyName)
        {
            _mutable = mutable ?? throw new ArgumentNullException(nameof(mutable));
            _outerPropertyName = outerPropertyName ?? throw new ArgumentNullException(nameof(outerPropertyName));
        }

        /// <summary>
        ///     Maps <paramref name="voProperty"/>, a member of <typeparamref name="TVo"/>, to
        ///     <paramref name="columnName"/> on the containing entity's table.
        /// </summary>
        /// <param name="voProperty">The member of the value object.</param>
        /// <param name="columnName">The column it maps to on the containing entity's table.</param>
        /// <param name="kind">
        ///     How the column participates in Insert and Update — <see cref="ColumnKind.ReadOnly"/> for a
        ///     computed column, which is read back and never written. When omitted, the annotations on the
        ///     value object's own member decide, and without any it is a regular column.
        /// </param>
        public ValueObjectBuilder<T, TVo> Map(Expression<Func<TVo, object>> voProperty, string columnName, ColumnKind? kind = null)
        {
            if (voProperty == null) throw new ArgumentNullException(nameof(voProperty));
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentNullException(nameof(columnName));

            var voMemberName = MemberNameExtractor.GetMemberName(voProperty);
            _mutable.AddValueObjectColumn(_outerPropertyName, voMemberName, columnName, kind);
            return this;
        }
    }
}
