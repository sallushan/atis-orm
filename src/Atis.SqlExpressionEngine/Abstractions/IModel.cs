using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Atis.SqlExpressionEngine.Abstractions
{
    public interface IModel
    {
        /// <summary>
        ///     <para>
        ///         Returns <paramref name="type"/>'s mapping for a caller that has already established it
        ///         is an entity — it occupies a query root position in the expression tree. Throws rather
        ///         than returning <c>null</c>, because at that point an absent mapping is a defect.
        ///     </para>
        ///     <para>
        ///         An implementation may derive the mapping here rather than only looking one up. The
        ///         caller's certainty is what makes that safe: the type is known to be an entity, so there
        ///         is nothing to guess at.
        ///     </para>
        /// </summary>
        EntityMetadata GetRequiredEntity(Type type);

        /// <summary>
        ///     <para>
        ///         Whether <paramref name="type"/> is an entity at all. This is the question for callers
        ///         that do not know — the declaring type of a member access, the element type of a
        ///         projection — where the answer is routinely "no" and that is not an error.
        ///     </para>
        ///     <para>
        ///         The pair divides by what the caller knows, not by what the model holds. Ask here when
        ///         unsure, then <see cref="GetRequiredEntity(Type)"/> once the answer is yes.
        ///     </para>
        /// </summary>
        bool CanBeEntity(Type type);
    }
}