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
        public ValueObjectBuilder<T, TVo> Map(Expression<Func<TVo, object>> voProperty, string columnName)
        {
            if (voProperty == null) throw new ArgumentNullException(nameof(voProperty));
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentNullException(nameof(columnName));

            var voMemberName = MemberNameExtractor.GetMemberName(voProperty);
            _mutable.AddValueObjectColumn(_outerPropertyName, voMemberName, columnName);
            return this;
        }
    }
}
