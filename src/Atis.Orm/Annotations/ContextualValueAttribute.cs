using System;

namespace Atis.Orm.Annotations
{
    /// <summary>
    ///     <para>
    ///         Marks a property or field as a value supplied by the ambient execution context, so a query can
    ///         simply name it:
    ///     </para>
    ///     <code>
    ///     public static class Current
    ///     {
    ///         [ContextualValue("CurrentUserId")]
    ///         public static int UserId =&gt; throw new NotSupportedException();
    ///     }
    ///
    ///     var mine = dbc.Invoices.Where(x =&gt; x.CreatedBy == Current.UserId).ToList();
    ///     </code>
    ///     <para>
    ///         The member is never read. The query pipeline replaces it with a contextual node and asks the
    ///         <c>IQueryContext</c> for the key on every execution. Works on a static member or on an instance
    ///         member of a captured object. A class that cannot be annotated is configured in
    ///         <c>OnModelCreating</c> with <c>ModelBuilder.ContextualValue</c> instead, and that wins over
    ///         the attribute.
    ///     </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class ContextualValueAttribute : Attribute
    {
        /// <summary>Marks the member; the context key is the member's own name.</summary>
        public ContextualValueAttribute() { }

        /// <summary>Marks the member and names the context key explicitly.</summary>
        public ContextualValueAttribute(string key)
        {
            this.Key = key;
        }

        /// <summary>Gets the context key, or <c>null</c> when the member name is to be used.</summary>
        public string Key { get; }
    }
}
