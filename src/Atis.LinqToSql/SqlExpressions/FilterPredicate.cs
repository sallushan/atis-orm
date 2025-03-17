﻿using System;

namespace Atis.LinqToSql.SqlExpressions
{
    // WARNING: do NOT create another class on top of SqlQueryExpression, because SqlQueryExpression's instance is being used
    //          in different places, so always do it very carefully.


    // CAUTION: when accepting the expressions within this SqlQueryExpression it's NOT recommended
    //          to change the expression to something else. For example, getting the SqlDataSourceExpression
    //          when creating the instance of this class, and during that creating if we changed the given
    //          SqlDataSourceExpression to something else, it might break the caller part of the program
    //          because caller might have cached or used that SqlDataSourceExpression and it will never
    //          know this class is no longer using the given SqlDataSourceExpression.


    /// <summary>
    ///     <para>
    ///         Represents a filtering condition used in SQL query `WHERE` or `HAVING` clauses.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This class allows specifying individual filter conditions along with an indicator 
    ///         that determines whether the condition should be combined using the `OR` operator 
    ///         instead of the default `AND`.
    ///     </para>
    ///     <para>
    ///         In standard SQL filtering, multiple conditions are typically combined using boolean 
    ///         expressions. However, this class introduces the ability to explicitly control whether 
    ///         a condition should be connected with `AND` or `OR`, providing greater flexibility in query 
    ///         construction.
    ///     </para>
    /// </remarks>
    public class FilterPredicate
    {
        /// <summary>
        ///     <para>
        ///         Gets or sets the SQL predicate expression.
        ///     </para>
        /// </summary>
        public SqlExpression Predicate { get; set; }
        /// <summary>
        ///     <para>
        ///         Gets or sets a value indicating whether to use the OR operator.
        ///     </para>
        /// </summary>
        public bool UseOrOperator { get; set; }

        /// <summary>
        ///     <para>
        ///         Compares the current FilterPredicate instance with another FilterPredicate.
        ///     </para>
        /// </summary>
        /// <param name="other">The other FilterPredicate instance to compare with.</param>
        /// <returns>True if both instances have the same Predicate and IsOrOperator value; otherwise, false.</returns>
        public bool Equals(FilterPredicate other)
        {
            if (other == null)
                return false;

            // Compare Predicate instances and IsOrOperator
            return ReferenceEquals(this.Predicate, other.Predicate) && this.UseOrOperator == other.UseOrOperator;
        }

        /// <summary>
        ///     <para>
        ///         Overrides the base Equals method to use the custom comparison logic.
        ///     </para>
        /// </summary>
        /// <param name="obj">The object to compare with.</param>
        /// <returns>True if the provided object is equal to the current instance.</returns>
        public override bool Equals(object obj)
        {
            if (obj is FilterPredicate otherPredicate)
            {
                return Equals(otherPredicate);
            }
            return false;
        }

        /// <summary>
        ///     <para>
        ///         Overrides the GetHashCode method to ensure consistent behavior when using FilterPredicate in collections.
        ///     </para>
        /// </summary>
        /// <returns>A hash code for the current FilterPredicate instance.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Predicate, UseOrOperator);
        }
    }
}
