using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

using Atis.Orm.Metadata;

namespace Atis.Orm.Abstractions
{
    /// <summary>
    ///     <para>
    ///         The model, holding both sides of every entity's mapping: the query side
    ///         (<see cref="EntityMetadata"/>, which is all the expression engine sees) and the
    ///         persistence side (<see cref="EntityCrudMetadata"/>, which only entity level CRUD uses).
    ///     </para>
    ///     <para>
    ///         The two are stored separately and keyed by the same CLR type. Splitting them is what
    ///         keeps persistence concepts such as identity and concurrency columns out of the
    ///         expression engine.
    ///     </para>
    /// </summary>
    public interface IOrmModel : IModel
    {
        /// <summary>
        ///     <para>
        ///         Returns <paramref name="type"/>'s query side mapping, deriving it from annotations if
        ///         the entity was never configured in <c>OnModelCreating</c>.
        ///     </para>
        ///     <para>
        ///         Translation reads an entity's mapping out of the model and does not build one on
        ///         demand, so every entry point that can name an entity type calls this first. It is the
        ///         explicit form of what creating a queryable used to do as a side effect.
        ///     </para>
        /// </summary>
        EntityMetadata EnsureEntityMapped(Type type);

        void Add(EntityMetadata metadata);
        bool TryGet(Type type, out EntityMetadata metadata);

        /// <summary>
        ///     Stores the persistence side of an entity's mapping, replacing any existing entry.
        /// </summary>
        void AddCrud(EntityCrudMetadata crudMetadata);

        /// <summary>
        ///     <para>
        ///         Returns the persistence side of an entity's mapping, building it with
        ///         <paramref name="factory"/> when the entity was never configured in
        ///         <c>OnModelCreating</c>.
        ///     </para>
        /// </summary>
        EntityCrudMetadata GetOrAddCrud(Type type, Func<Type, EntityCrudMetadata> factory);
    }
}
