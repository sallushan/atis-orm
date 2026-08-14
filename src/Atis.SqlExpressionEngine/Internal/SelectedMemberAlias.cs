using System;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.Internal
{
    /// <summary>
    ///     <para>
    ///         Works out the name to alias one element of an <c>x =&gt; new object[] { x.A, x.B }</c>
    ///         selector by. Three methods take a selector in that form — <c>Insert</c> and
    ///         <c>Update</c> for their OUTPUT clause, and <c>SelectFields</c> for its select list —
    ///         and all three name each column by the member it selects.
    ///     </para>
    ///     <para>
    ///         The alias is the <em>member</em> name, not the mapped column name. That is what makes
    ///         the resulting dictionary readable as <c>row[nameof(Student.Name)]</c> whatever the
    ///         column happens to be called.
    ///     </para>
    /// </summary>
    internal static class SelectedMemberAlias
    {
        /// <summary>
        ///     <para>
        ///         The member name <paramref name="selectedField"/> selects.
        ///     </para>
        ///     <para>
        ///         The <c>Convert</c> nodes come off first: every element of an <c>object[]</c> whose
        ///         member is not already <see cref="object"/> is boxed by the compiler, so the member
        ///         access is never the outermost node.
        ///     </para>
        /// </summary>
        /// <param name="selectedField">One element of the selector's array.</param>
        /// <param name="index">Its position, so the diagnostic can point at the right element.</param>
        /// <param name="methodName">
        ///     The method the caller actually typed. The work is identical for all three, but a
        ///     message naming the wrong one sends the reader to the wrong line.
        /// </param>
        public static string Resolve(Expression selectedField, int index, string methodName)
        {
            while (selectedField is UnaryExpression unary
                   && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            {
                selectedField = unary.Operand;
            }

            return (selectedField as MemberExpression)?.Member.Name
                ?? throw new InvalidOperationException(
                    $"{methodName} field {index} must select a member, for example " +
                    $"'x => new object[] {{ x.EmployeeId }}', but was '{selectedField}'.");
        }
    }
}
