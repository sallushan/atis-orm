﻿using System.Collections.Generic;
using System;

namespace Atis.SqlExpressionEngine.SqlExpressions
{
    /// <summary>
    ///     <para>
    ///         Represents a SQL column expression.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This class can represent either a standard SQL column or a scalar column. A scalar column is a single column
    ///         selected during projection on a single field without using <c>NewExpression</c> or <c>MemberInitExpression</c>.
    ///     </para>
    /// </remarks>
    public class SqlColumnExpression : SqlExpression
    {
        private static readonly ISet<SqlExpressionType> _allowedTypes = new HashSet<SqlExpressionType>
            {
                SqlExpressionType.Column,
                SqlExpressionType.ScalarColumn,
                SqlExpressionType.SubQueryColumn,
            };
        private static SqlExpressionType ValidateNodeType(SqlExpressionType nodeType)
            => _allowedTypes.Contains(nodeType)
                ? nodeType
                : throw new InvalidOperationException($"SqlExpressionType '{nodeType}' is not a valid {nameof(SqlColumnExpression)}.");


        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="SqlColumnExpression"/> class.
        ///     </para>
        /// </summary>
        /// <param name="columnExpression">The column expression.</param>
        /// <param name="columnAlias">The alias of the column.</param>
        /// <param name="modelPath">The model path of the column.</param>
        /// <param name="nodeType">The type of the SQL expression node.</param>
        public SqlColumnExpression(SqlExpression columnExpression, string columnAlias, ModelPath modelPath, SqlExpressionType nodeType)
        {
            this.ColumnExpression = columnExpression;
            this.ColumnAlias = columnAlias;
            this.ModelPath = modelPath;
            ValidateNodeType(nodeType);
            this.NodeType = nodeType;
        }

        /// <summary>
        ///     <para>
        ///         Gets the type of the SQL expression node.
        ///     </para>
        ///     <para>
        ///         This property always returns <see cref="SqlExpressionType.Column"/>.
        ///     </para>
        /// </summary>
        public override SqlExpressionType NodeType { get; }

        /// <summary>
        ///     <para>
        ///         Gets the column expression.
        ///     </para>
        /// </summary>
        public SqlExpression ColumnExpression { get; }

        /// <summary>
        ///     <para>
        ///         Gets the alias of the column.
        ///     </para>
        /// </summary>
        public string ColumnAlias { get; }

        /// <summary>
        ///     <para>
        ///         Gets the model path of the column.
        ///     </para>
        /// </summary>
        public ModelPath ModelPath { get; }

        /// <summary>
        ///     <para>
        ///         Updates the column expression, alias, and model path.
        ///     </para>
        ///     <para>
        ///         If the new values are the same as the current values, the current instance is returned.
        ///         Otherwise, a new instance with the updated values is returned.
        ///     </para>
        /// </summary>
        /// <param name="columnExpression">The new column expression.</param>
        /// <returns>A new <see cref="SqlColumnExpression"/> instance with the updated values, or the current instance if unchanged.</returns>
        public SqlColumnExpression Update(SqlExpression columnExpression)
        {
            if (columnExpression == this.ColumnExpression)
                return this;
            return new SqlColumnExpression(columnExpression, this.ColumnAlias, this.ModelPath, this.NodeType);
        }

        /// <summary>
        ///     <para>
        ///         Accepts a visitor to visit this SQL column expression.
        ///     </para>
        /// </summary>
        /// <param name="sqlExpressionVisitor">The visitor to accept.</param>
        /// <returns>The result of visiting this expression.</returns>
        protected internal override SqlExpression Accept(SqlExpressionVisitor sqlExpressionVisitor)
        {
            return sqlExpressionVisitor.VisitSqlColumnExpression(this);
        }


        // not updating the ModelPath, because we were copying projection elsewhere, so we experienced same
        // Projection expression was copied in a sql expression, then the source sql expression was
        // changing and it was causing the other sql expression's projection to be changed as well

        /// <summary>
        ///     <para>
        ///         Returns a string representation of the SQL column expression.
        ///     </para>
        ///     <para>
        ///         The string representation includes the column expression and its alias.
        ///     </para>
        /// </summary>
        /// <returns>A string representation of the SQL column expression.</returns>
        public override string ToString()
        {
            return $"{this.ColumnExpression} as {this.ColumnAlias}";
        }
    }
}