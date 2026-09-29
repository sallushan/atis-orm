using Atis.SqlExpressionEngine;
using System;
using System.Collections.Generic;

using Atis.Orm.Abstractions;
namespace Atis.Orm.Metadata
{
    public class ModelBuilder
    {
        private readonly IEntityMetadataBuilder _entityMetadataBuilder;
        private readonly IEntityCrudMetadataFactory _entityCrudMetadataFactory;
        private readonly IOrmModel _ormModel;
        private readonly Dictionary<Type, MutableEntityMetadata> _mutableEntityMetadata = new Dictionary<Type, MutableEntityMetadata>();

        public bool AllClrProperties { get; set; } = false;

        public ModelBuilder(IEntityMetadataBuilder entityMetadataBuilder, IOrmModel ormModel)
            : this(entityMetadataBuilder, crudMetadataFactory: null, ormModel: ormModel)
        {
        }

        /// <param name="crudMetadataFactory">
        ///     Seeds the persistence side of each mapping from annotations, so that a fluent call
        ///     overrides an annotation rather than the other way round. When <c>null</c>, every column
        ///     starts out as <see cref="ColumnKind.Regular"/> and only fluent configuration applies.
        /// </param>
        public ModelBuilder(IEntityMetadataBuilder entityMetadataBuilder, IEntityCrudMetadataFactory crudMetadataFactory, IOrmModel ormModel)
        {
            _entityMetadataBuilder = entityMetadataBuilder ?? throw new ArgumentNullException(nameof(entityMetadataBuilder));
            _entityCrudMetadataFactory = crudMetadataFactory;
            _ormModel = ormModel ?? throw new ArgumentNullException(nameof(ormModel));
        }

        public EntityBuilder<T> Entity<T>()
        {
            if (!this._mutableEntityMetadata.TryGetValue(typeof(T), out var existing))
            {
                var seeded = _entityMetadataBuilder.Build(typeof(T));
                var crudSeeded = _entityCrudMetadataFactory?.Build(typeof(T));
                var mutable = new MutableEntityMetadata(seeded, crudSeeded);
                _mutableEntityMetadata.Add(typeof(T), mutable);
                existing = mutable;
            }
            var builder = new EntityBuilder<T>(existing);
            return builder;
        }

        public EntityBuilder<T> Entity<T>(Action<EntityBuilder<T>> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var builder = Entity<T>();
            configure(builder);
            return builder;
        }

        /// <summary>
        ///     <para>
        ///         Marks a static member as a value the execution context supplies, for a class that cannot
        ///         carry <c>[ContextualValue]</c>: <c>mb.ContextualValue(() =&gt; Current.UserId, "CurrentUserId")</c>.
        ///         The key defaults to the member's name. Wins over an annotation on the same member.
        ///     </para>
        /// </summary>
        public ModelBuilder ContextualValue<TValue>(System.Linq.Expressions.Expression<Func<TValue>> member, string key = null)
        {
            return this.AddContextualValue(member, key);
        }

        /// <summary>
        ///     Marks an instance member as a context value, for every instance of <typeparamref name="TOwner"/>:
        ///     <c>mb.ContextualValue&lt;RequestInfo&gt;(r =&gt; r.UserId, "CurrentUserId")</c>.
        /// </summary>
        public ModelBuilder ContextualValue<TOwner, TValue>(System.Linq.Expressions.Expression<Func<TOwner, TValue>> member, string key = null)
        {
            return this.AddContextualValue(member, key);
        }

        private ModelBuilder AddContextualValue(System.Linq.Expressions.LambdaExpression member, string key)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            var body = member.Body;
            // A value-typed member arrives wrapped in a Convert when the lambda is typed as object.
            while (body is System.Linq.Expressions.UnaryExpression unary && body.NodeType == System.Linq.Expressions.ExpressionType.Convert)
                body = unary.Operand;
            if (!(body is System.Linq.Expressions.MemberExpression memberExpression))
                throw new ArgumentException($"'{member}' must be a plain property or field access.", nameof(member));
            this._contextualValues.Add((memberExpression.Member, key ?? memberExpression.Member.Name));
            return this;
        }

        private readonly List<(System.Reflection.MemberInfo Member, string Key)> _contextualValues = new List<(System.Reflection.MemberInfo, string)>();

        internal void Build()
        {
            foreach (var (member, key) in _contextualValues)
                _ormModel.AddContextualValue(member, key);

            foreach (var mutableMetadata in _mutableEntityMetadata.Values)
            {
                _ormModel.Add(mutableMetadata.Build());
                _ormModel.AddCrud(mutableMetadata.BuildCrud());
            }
        }
    }
}