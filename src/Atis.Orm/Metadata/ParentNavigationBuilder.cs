using Atis.SqlExpressionEngine;
using System;

namespace Atis.Orm.Metadata
{
    /// <summary>
    ///     <para>
    ///         Returned by <see cref="EntityBuilder{T}"/>.HasParent. The navigation is registered as
    ///         <see cref="NavigationType.ToParent"/> immediately; calling <see cref="Optional"/> changes it to
    ///         <see cref="NavigationType.ToParentOptional"/> (a left/outer join).
    ///     </para>
    /// </summary>
    /// <typeparam name="T">The entity type declaring the navigation.</typeparam>
    public class ParentNavigationBuilder<T>
    {
        private readonly MutableNavigationInfo _nav;

        internal ParentNavigationBuilder(MutableNavigationInfo nav)
        {
            _nav = nav ?? throw new ArgumentNullException(nameof(nav));
        }

        /// <summary>
        ///     Marks the parent navigation as optional (<see cref="NavigationType.ToParentOptional"/>).
        /// </summary>
        public ParentNavigationBuilder<T> Optional()
        {
            _nav.NavigationType = NavigationType.ToParentOptional;
            return this;
        }
    }
}
