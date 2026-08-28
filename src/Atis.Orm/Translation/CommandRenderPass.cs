using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

using Atis.Orm.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.Orm.Translation
{
    /// <summary>
    ///     <para>
    ///         One walk over one fragment list, producing one command. Placeholder text and
    ///         <see cref="DbParameter"/> creation both go through <see cref="IDbParameterFactory"/>, so the
    ///         command text and the parameter list come from the same walk and cannot drift.
    ///     </para>
    ///     <para>
    ///         <strong>One instance renders once.</strong> The text, the parameters and the placeholder
    ///         numbering accumulate on the instance, which is what lets nested fragment lists render straight
    ///         into the same output however deeply composites nest - and is why
    ///         <see cref="CommandRenderer"/> creates a fresh one per render rather than sharing it.
    ///     </para>
    ///     <para>
    ///         A provider supporting its own <see cref="ICommandFragment"/> type derives from this, overrides
    ///         <see cref="RenderFragment"/>, handles the types it knows and calls <c>base</c> for the rest -
    ///         then returns it from <see cref="CommandRenderer.CreateRenderPass"/>.
    ///     </para>
    /// </summary>
    public class CommandRenderPass
    {
        private readonly IDbParameterFactory dbParameterFactory;
        private readonly Func<IQueryParameter, object> resolveValue;
        private readonly List<DbParameter> dbParameters = new List<DbParameter>();
        private readonly StringBuilder sql = new StringBuilder();

        // Set only while a repeated term is writing one copy of its template - see RenderRepeatingFragment.
        private Func<IQueryParameter, object> resolveValueOverride;

        private bool rendered;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandRenderPass"/> class.
        /// </summary>
        /// <param name="dbParameterFactory">Creates and names the <see cref="DbParameter"/>s.</param>
        /// <param name="resolveValue">Obtains the value bound to a parameter for this execution.</param>
        /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
        public CommandRenderPass(IDbParameterFactory dbParameterFactory, Func<IQueryParameter, object> resolveValue)
        {
            this.dbParameterFactory = dbParameterFactory ?? throw new ArgumentNullException(nameof(dbParameterFactory));
            this.resolveValue = resolveValue ?? throw new ArgumentNullException(nameof(resolveValue));
        }

        /// <summary>
        ///     Renders <paramref name="fragments"/> into a command. Callable once per instance: a second call
        ///     would append to the first command's text rather than start a new one.
        /// </summary>
        /// <param name="fragments">The fragments to render, in output order.</param>
        /// <exception cref="ArgumentNullException"><paramref name="fragments"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">This pass has already rendered.</exception>
        public RenderedCommand Render(IReadOnlyList<ICommandFragment> fragments)
        {
            if (fragments is null)
                throw new ArgumentNullException(nameof(fragments));
            if (this.rendered)
                throw new InvalidOperationException(
                    $"A {nameof(CommandRenderPass)} renders once. Create a new one per render - " +
                    $"{nameof(CommandRenderer)}.{nameof(CommandRenderer.Render)} does exactly that.");

            this.rendered = true;
            this.RenderFragments(fragments);
            return new RenderedCommand(this.sql.ToString(), this.dbParameters);
        }

        /// <summary>Renders every fragment of <paramref name="fragments"/>, in order.</summary>
        protected void RenderFragments(IReadOnlyList<ICommandFragment> fragments)
        {
            for (var i = 0; i < fragments.Count; i++)
                this.RenderFragment(fragments[i]);
        }

        /// <summary>
        ///     <para>
        ///         Dispatches one fragment to its renderer. Override to support a provider-defined fragment
        ///         type, calling <c>base</c> for everything else.
        ///     </para>
        /// </summary>
        protected virtual void RenderFragment(ICommandFragment fragment)
        {
            switch (fragment)
            {
                case TextCommandFragment text:
                    this.RenderTextFragment(text);
                    break;
                case ParameterCommandFragment parameter:
                    this.RenderParameterFragment(parameter);
                    break;
                case ExpandableParameterCommandFragment expandable:
                    this.RenderExpandableParameterFragment(expandable);
                    break;
                case RepeatingCommandFragment repeating:
                    this.RenderRepeatingFragment(repeating);
                    break;
                case NullSwitchCommandFragment nullSwitch:
                    this.RenderNullSwitchFragment(nullSwitch);
                    break;
                case OptionalPredicateCommandFragment optionalPredicate:
                    this.RenderOptionalPredicateFragment(optionalPredicate);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported fragment type: {fragment?.GetType().FullName ?? "null"}.");
            }
        }

        /// <summary>The command text written so far. For a derived pass rendering its own fragment type.</summary>
        protected StringBuilder Sql => this.sql;

        /// <summary>The parameters bound so far, in the order their placeholders appear.</summary>
        protected IReadOnlyList<DbParameter> DbParameters => this.dbParameters;

        /// <summary>
        ///     <para>
        ///         Obtains the value bound to <paramref name="queryParameter"/> for this execution - or, while
        ///         a <see cref="RepeatingCommandFragment"/> is writing one copy of its template, the single
        ///         element that copy stands for.
        ///     </para>
        /// </summary>
        protected object ResolveValue(IQueryParameter queryParameter)
        {
            return this.resolveValueOverride != null
                       ? this.resolveValueOverride(queryParameter)
                       : this.resolveValue(queryParameter);
        }

        /// <summary>Writes a run of literal command text.</summary>
        protected virtual void RenderTextFragment(TextCommandFragment fragment)
        {
            this.sql.Append(fragment.Text);
        }

        /// <summary>Writes one placeholder and binds one parameter to it.</summary>
        protected virtual void RenderParameterFragment(ParameterCommandFragment fragment)
        {
            var value = this.ResolveValue(fragment.QueryParameter);
            // Placeholders are numbered by how many parameters have actually been bound, so a dropped
            // optional term or an unused null-switch branch simply renumbers what follows. Nothing
            // downstream depends on a placeholder name being stable across executions.
            var dbParameter = this.dbParameterFactory.CreateDbParameter(this.dbParameters.Count, fragment.QueryParameter, value);
            this.sql.Append(dbParameter.ParameterName);
            this.dbParameters.Add(dbParameter);
        }

        /// <summary>
        ///     <para>
        ///         Expands a collection value into one placeholder per element. An empty collection - or a
        ///         null one - emits the empty-list template instead and binds nothing.
        ///     </para>
        /// </summary>
        protected virtual void RenderExpandableParameterFragment(ExpandableParameterCommandFragment fragment)
        {
            var value = this.ResolveValue(fragment.QueryParameter);
            if (fragment.ValueDelimiter != null)
                value = SqlDelimitedValuesExpression.Split(value, fragment.ValueDelimiter);
            if (value is IEnumerable enumerable && !(value is string))
            {
                var elementParameters = this.dbParameterFactory.CreateDbParameters(this.dbParameters.Count, fragment.QueryParameter, enumerable);
                var count = 0;
                foreach (var elementParameter in elementParameters)
                {
                    if (count > 0)
                        this.sql.Append(", ");
                    this.sql.Append(elementParameter.ParameterName);
                    this.dbParameters.Add(elementParameter);
                    count++;
                }
                if (count == 0)
                    this.RenderEmptyValueList(fragment);
            }
            else if (value is null)
            {
                this.RenderEmptyValueList(fragment);
            }
            else
            {
                throw new InvalidOperationException(
                    $"The value for parameter '{fragment.QueryParameter.ParameterIdentity}' must be an IEnumerable, " +
                    $"but was '{value.GetType().Name}'.");
            }
        }

        /// <summary>
        ///     <para>
        ///         Writes self-contained SQL in place of an empty value list. No parameter is emitted, which
        ///         keeps positional (<c>?</c>) dialects consistent.
        ///     </para>
        /// </summary>
        protected virtual void RenderEmptyValueList(ExpandableParameterCommandFragment fragment)
        {
            this.sql.Append(fragment.EmptyListTemplate ?? "NULL");
        }

        /// <summary>
        ///     <para>
        ///         Writes the template once per element of the collection value, separated by the fragment's
        ///         separator. Each copy binds its own parameter, so a three-element collection produces three
        ///         placeholders.
        ///     </para>
        ///     <para>
        ///         A null or empty collection writes the empty stand-in and binds nothing. In the
        ///         <c>WhereBuilder</c> shapes the surrounding optional term has already dropped by then, so
        ///         this is the answer for any other use of the fragment.
        ///     </para>
        /// </summary>
        protected virtual void RenderRepeatingFragment(RepeatingCommandFragment fragment)
        {
            var value = this.ResolveValue(fragment.QueryParameter);
            // Splitting happens here rather than in ResolveValue, because inside the template the same
            // parameter resolves to a single element - which is a string, and must not be split again.
            if (fragment.ValueDelimiter != null)
                value = SqlDelimitedValuesExpression.Split(value, fragment.ValueDelimiter);
            if (value is null || value is DBNull)
            {
                this.RenderEmptyRepetition(fragment);
                return;
            }

            if (!(value is IEnumerable enumerable) || value is string)
            {
                throw new InvalidOperationException(
                    $"The value for parameter '{fragment.QueryParameter.ParameterIdentity}' must be an IEnumerable, " +
                    $"but was '{value.GetType().Name}'.");
            }

            var count = 0;
            foreach (var element in enumerable)
            {
                if (count > 0)
                    this.sql.Append(fragment.Separator);

                // The collection's parameter stands for this element while its copy renders, so the
                // template's ordinary parameter marker binds the element rather than the whole collection.
                // The previous override is restored rather than cleared, and is what anything else falls
                // back to, so a repeat nested inside another repeat leaves the outer binding intact.
                var outerOverride = this.resolveValueOverride;
                this.resolveValueOverride = p => ReferenceEquals(p, fragment.QueryParameter)
                                                     ? element
                                                     : (outerOverride ?? this.resolveValue)(p);
                try
                {
                    this.RenderFragments(fragment.Template);
                }
                finally
                {
                    this.resolveValueOverride = outerOverride;
                }

                count++;
            }

            if (count == 0)
                this.RenderEmptyRepetition(fragment);
        }

        /// <summary>
        ///     Writes self-contained SQL in place of a repetition that has no elements. No parameter is bound.
        /// </summary>
        protected virtual void RenderEmptyRepetition(RepeatingCommandFragment fragment)
        {
            this.sql.Append(fragment.WhenEmpty ?? "1 = 0");
        }

        /// <summary>
        ///     Writes <c>col = @p</c> or <c>col IS NULL</c>, picked from the value this execution binds.
        /// </summary>
        protected virtual void RenderNullSwitchFragment(NullSwitchCommandFragment fragment)
        {
            var value = this.ResolveValue(fragment.QueryParameter);
            this.RenderFragments(fragment.SelectBranch(value));
        }

        /// <summary>
        ///     Writes the filter, or the always-true term standing in for it, picked from the guard's value
        ///     this execution binds.
        /// </summary>
        protected virtual void RenderOptionalPredicateFragment(OptionalPredicateCommandFragment fragment)
        {
            var guardValue = this.ResolveValue(fragment.QueryParameter);
            this.RenderFragments(fragment.SelectBranch(guardValue));
        }
    }
}
