using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

using Atis.Orm.Abstractions;

namespace Atis.Orm.Translation
{
    /// <summary>
    ///     <para>
    ///         The materialized command produced from a fragment list: the SQL text, the
    ///         <see cref="DbParameter"/>s in the order they appear in it, and whether any parameter was
    ///         expanded (so a caller knows the text is only valid for the collection lengths used here).
    ///     </para>
    /// </summary>
    public sealed class RenderedCommand
    {
        internal RenderedCommand(string sql, IReadOnlyList<DbParameter> dbParameters, bool hasExpandableParameters)
        {
            this.Sql = sql ?? throw new ArgumentNullException(nameof(sql));
            this.DbParameters = dbParameters ?? throw new ArgumentNullException(nameof(dbParameters));
            this.HasExpandableParameters = hasExpandableParameters;
        }

        /// <summary>The complete SQL statement.</summary>
        public string Sql { get; }

        /// <summary>The placeholders in the order they appear in <see cref="Sql"/>.</summary>
        public IReadOnlyList<DbParameter> DbParameters { get; }

        /// <summary>
        ///     Whether the statement contains a collection parameter that was expanded. When <c>true</c>,
        ///     the same fragments re-rendered with a different-length collection produce different SQL.
        /// </summary>
        public bool HasExpandableParameters { get; }
    }

    /// <summary>
    ///     <para>
    ///         Walks a translated <see cref="SqlFragment"/> list together with the current parameter values
    ///         and produces the SQL text plus the matching <see cref="DbParameter"/>s.
    ///     </para>
    /// </summary>
    public interface ISqlCommandRenderer
    {
        /// <summary>
        ///     Renders <paramref name="fragments"/>, using <paramref name="resolveValue"/> to obtain each
        ///     parameter's current value (initial value for display / cache-miss, or a rebound value).
        /// </summary>
        RenderedCommand Render(IReadOnlyList<SqlFragment> fragments, Func<IQueryParameter, object> resolveValue);
    }

    /// <summary>
    ///     <para>
    ///         Default single-pass renderer. Placeholder text and <see cref="DbParameter"/> creation both go
    ///         through <see cref="IDbParameterFactory"/> (named by <see cref="IDbParameterNameGenerator"/>),
    ///         so the SQL text and the parameter list come from one walk and cannot drift.
    ///     </para>
    ///     <para>
    ///         This is the stage that expands a collection parameter: a marker the translator flagged as
    ///         <see cref="SqlParameterFragment.IsExpandable"/> becomes one placeholder per element. Because a
    ///         compiled query is cached by expression shape and re-executed with collections of different
    ///         lengths, the placeholder count can only be decided here, per execution.
    ///     </para>
    /// </summary>
    public class SqlCommandRenderer : ISqlCommandRenderer
    {
        private readonly IDbParameterNameGenerator nameGenerator;
        private readonly IDbParameterFactory parameterFactory;

        public SqlCommandRenderer(IDbParameterNameGenerator nameGenerator, IDbParameterFactory parameterFactory)
        {
            this.nameGenerator = nameGenerator ?? throw new ArgumentNullException(nameof(nameGenerator));
            this.parameterFactory = parameterFactory ?? throw new ArgumentNullException(nameof(parameterFactory));
        }

        /// <inheritdoc />
        public RenderedCommand Render(IReadOnlyList<SqlFragment> fragments, Func<IQueryParameter, object> resolveValue)
        {
            if (fragments is null)
                throw new ArgumentNullException(nameof(fragments));
            if (resolveValue is null)
                throw new ArgumentNullException(nameof(resolveValue));

            var sb = new StringBuilder();
            var dbParameters = new List<DbParameter>();
            var hasExpandableParameters = false;
            // One index per logical parameter; the factory turns it (and, for expansion, a sub-index) into names.
            var parameterIndex = 0;

            for (var i = 0; i < fragments.Count; i++)
            {
                if (fragments[i] is SqlTextFragment text)
                {
                    sb.Append(text.Text);
                }
                else if (fragments[i] is SqlParameterFragment marker)
                {
                    var value = resolveValue(marker.Parameter);
                    if (marker.IsExpandable)
                    {
                        hasExpandableParameters = true;
                        this.RenderExpandable(marker, value, parameterIndex, sb, dbParameters);
                    }
                    else
                    {
                        this.RenderSingle(marker.Parameter, value, parameterIndex, sb, dbParameters);
                    }
                    parameterIndex++;
                }
                else
                {
                    throw new NotSupportedException($"SQL fragment type '{fragments[i]?.GetType().Name}' is not supported.");
                }
            }

            return new RenderedCommand(sb.ToString(), dbParameters, hasExpandableParameters);
        }

        private void RenderSingle(IQueryParameter parameter, object value, int parameterIndex, StringBuilder sb, List<DbParameter> dbParameters)
        {
            var dbParameter = this.parameterFactory.CreateDbParameter(parameterIndex, parameter, value);
            sb.Append(dbParameter.ParameterName);
            dbParameters.Add(dbParameter);
        }

        private void RenderExpandable(SqlParameterFragment marker, object value, int parameterIndex, StringBuilder sb, List<DbParameter> dbParameters)
        {
            // A non-string IEnumerable expands; an empty one (or a null collection) becomes the empty-list
            // template with a single null placeholder. Anything else is a bug: the translator only marks a
            // position expandable when it emitted a multi-value parameter there.
            if (value is IEnumerable enumerable && !(value is string))
            {
                var elementParameters = this.parameterFactory.CreateDbParameters(parameterIndex, marker.Parameter, enumerable);
                var count = 0;
                foreach (var elementParameter in elementParameters)
                {
                    if (count > 0)
                        sb.Append(", ");
                    sb.Append(elementParameter.ParameterName);
                    dbParameters.Add(elementParameter);
                    count++;
                }
                if (count == 0)
                    this.RenderEmptyList(marker, parameterIndex, sb, dbParameters);
            }
            else if (value == null)
            {
                this.RenderEmptyList(marker, parameterIndex, sb, dbParameters);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Parameter at index {parameterIndex} is marked as expandable, but its value is not a collection.");
            }
        }

        private void RenderEmptyList(SqlParameterFragment marker, int parameterIndex, StringBuilder sb, List<DbParameter> dbParameters)
        {
            // An empty list is not valid SQL anywhere, so the position is replaced by the template the
            // translator chose for it, with a single placeholder bound to null (`IN (SELECT @p0 WHERE 1 = 0)`).
            // Binding a parameter rather than writing a bare NULL keeps the operand types compatible.
            var dbParameter = this.parameterFactory.CreateDbParameter(parameterIndex, marker.Parameter, null);
            var template = marker.EmptyListTemplate ?? "{0}";
            // Plain Replace, not string.Format: the templates are SQL and may contain braces.
            sb.Append(template.Replace("{0}", dbParameter.ParameterName));
            dbParameters.Add(dbParameter);
        }
    }
}
