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
    ///         Default single-pass renderer. Placeholder text and <see cref="DbParameter"/> creation both go
    ///         through <see cref="IDbParameterFactory"/>, so the command text and the parameter list come from
    ///         one walk and cannot drift.
    ///     </para>
    ///     <para>
    ///         A provider supporting its own <see cref="ICommandFragment"/> type overrides
    ///         <see cref="RenderFragment"/>, handles the types it knows, and calls <c>base</c> for the rest.
    ///     </para>
    /// </summary>
    public class CommandRenderer : ICommandRenderer
    {
        private readonly IDbParameterFactory dbParameterFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandRenderer"/> class.
        /// </summary>
        /// <param name="dbParameterFactory">Creates and names the <see cref="DbParameter"/>s.</param>
        /// <exception cref="ArgumentNullException"><paramref name="dbParameterFactory"/> is <c>null</c>.</exception>
        public CommandRenderer(IDbParameterFactory dbParameterFactory)
        {
            this.dbParameterFactory = dbParameterFactory ?? throw new ArgumentNullException(nameof(dbParameterFactory));
        }

        /// <summary>
        ///     <para>
        ///         The output being built by one <see cref="Render"/> call. Nested fragment lists render into
        ///         the same instance, so the text, the parameter list and the running placeholder index stay
        ///         in step no matter how deeply composites nest.
        ///     </para>
        /// </summary>
        protected sealed class RenderContext
        {
            private readonly Func<IQueryParameter, object> resolveValue;

            // Innermost last. A list rather than a dictionary because it holds one entry per repeat currently
            // being rendered - in practice one, occasionally two - and the innermost must win.
            private readonly List<KeyValuePair<IQueryParameter, object>> elementValues = new List<KeyValuePair<IQueryParameter, object>>();

            internal RenderContext(Func<IQueryParameter, object> resolveValue)
            {
                this.resolveValue = resolveValue;
            }

            /// <summary>The command text accumulated so far.</summary>
            public StringBuilder Sql { get; } = new StringBuilder();

            /// <summary>The parameters bound so far, in the order their placeholders appear.</summary>
            public List<DbParameter> DbParameters { get; } = new List<DbParameter>();

            /// <summary>
            ///     <para>
            ///         The index the next placeholder is named from. Only advanced by markers that actually
            ///         reach the output: a dropped optional term or an unused null-switch branch simply
            ///         renumbers what follows, and nothing downstream depends on a placeholder name being
            ///         stable across executions.
            ///     </para>
            /// </summary>
            public int NextParameterIndex { get; set; }

            /// <summary>
            ///     <para>
            ///         Obtains the value bound to <paramref name="queryParameter"/> for this execution - or,
            ///         while a <see cref="RepeatingCommandFragment"/> is rendering one copy of its template,
            ///         the single element that copy stands for.
            ///     </para>
            /// </summary>
            public object ResolveValue(IQueryParameter queryParameter)
            {
                for (var i = this.elementValues.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(this.elementValues[i].Key, queryParameter))
                        return this.elementValues[i].Value;
                }

                return this.resolveValue(queryParameter);
            }

            /// <summary>
            ///     Makes <paramref name="queryParameter"/> resolve to <paramref name="elementValue"/> until the
            ///     matching <see cref="EndElement"/>. Called by a repeating fragment around each copy.
            /// </summary>
            public void BeginElement(IQueryParameter queryParameter, object elementValue)
            {
                if (queryParameter is null)
                    throw new ArgumentNullException(nameof(queryParameter));

                this.elementValues.Add(new KeyValuePair<IQueryParameter, object>(queryParameter, elementValue));
            }

            /// <summary>Ends the binding opened by the matching <see cref="BeginElement"/>.</summary>
            public void EndElement()
            {
                if (this.elementValues.Count == 0)
                    throw new InvalidOperationException($"{nameof(EndElement)} was called without a matching {nameof(BeginElement)}.");

                this.elementValues.RemoveAt(this.elementValues.Count - 1);
            }
        }

        /// <inheritdoc />
        public RenderedCommand Render(IReadOnlyList<ICommandFragment> fragments, Func<IQueryParameter, object> resolveValue)
        {
            if (fragments is null)
                throw new ArgumentNullException(nameof(fragments));
            if (resolveValue is null)
                throw new ArgumentNullException(nameof(resolveValue));

            var context = new RenderContext(resolveValue);
            this.RenderFragments(fragments, context);
            return new RenderedCommand(context.Sql.ToString(), context.DbParameters);
        }

        /// <summary>Renders every fragment of <paramref name="fragments"/> into <paramref name="context"/>.</summary>
        protected void RenderFragments(IReadOnlyList<ICommandFragment> fragments, RenderContext context)
        {
            for (var i = 0; i < fragments.Count; i++)
                this.RenderFragment(fragments[i], context);
        }

        /// <summary>
        ///     <para>
        ///         Dispatches one fragment to its renderer. Override to support a provider-defined fragment
        ///         type, calling <c>base</c> for everything else.
        ///     </para>
        /// </summary>
        protected virtual void RenderFragment(ICommandFragment fragment, RenderContext context)
        {
            switch (fragment)
            {
                case TextCommandFragment text:
                    this.RenderTextFragment(text, context);
                    break;
                case ParameterCommandFragment parameter:
                    this.RenderParameterFragment(parameter, context);
                    break;
                case ExpandableParameterCommandFragment expandable:
                    this.RenderExpandableParameterFragment(expandable, context);
                    break;
                case RepeatingCommandFragment repeating:
                    this.RenderRepeatingFragment(repeating, context);
                    break;
                case NullSwitchCommandFragment nullSwitch:
                    this.RenderNullSwitchFragment(nullSwitch, context);
                    break;
                case OptionalPredicateCommandFragment optionalPredicate:
                    this.RenderOptionalPredicateFragment(optionalPredicate, context);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported fragment type: {fragment?.GetType().FullName ?? "null"}.");
            }
        }

        /// <summary>Writes a run of literal command text.</summary>
        protected virtual void RenderTextFragment(TextCommandFragment fragment, RenderContext context)
        {
            context.Sql.Append(fragment.Text);
        }

        /// <summary>Writes one placeholder and binds one parameter to it.</summary>
        protected virtual void RenderParameterFragment(ParameterCommandFragment fragment, RenderContext context)
        {
            var value = context.ResolveValue(fragment.QueryParameter);
            var dbParameter = this.dbParameterFactory.CreateDbParameter(context.NextParameterIndex, fragment.QueryParameter, value);
            context.Sql.Append(dbParameter.ParameterName);
            context.DbParameters.Add(dbParameter);
            context.NextParameterIndex++;
        }

        /// <summary>
        ///     <para>
        ///         Expands a collection value into one placeholder per element. An empty collection - or a
        ///         null one - emits the empty-list template instead and binds nothing.
        ///     </para>
        /// </summary>
        protected virtual void RenderExpandableParameterFragment(ExpandableParameterCommandFragment fragment, RenderContext context)
        {
            var value = context.ResolveValue(fragment.QueryParameter);
            if (value is IEnumerable enumerable && !(value is string))
            {
                var elementParameters = this.dbParameterFactory.CreateDbParameters(context.NextParameterIndex, fragment.QueryParameter, enumerable);
                var count = 0;
                foreach (var elementParameter in elementParameters)
                {
                    if (count > 0)
                        context.Sql.Append(", ");
                    context.Sql.Append(elementParameter.ParameterName);
                    context.DbParameters.Add(elementParameter);
                    count++;
                }
                if (count == 0)
                    this.RenderEmptyValueList(fragment, context);
            }
            else if (value is null)
            {
                this.RenderEmptyValueList(fragment, context);
            }
            else
            {
                throw new InvalidOperationException(
                    $"The value for parameter '{fragment.QueryParameter.ParameterIdentity}' must be an IEnumerable, " +
                    $"but was '{value.GetType().Name}'.");
            }

            context.NextParameterIndex++;
        }

        /// <summary>
        ///     <para>
        ///         Writes self-contained SQL in place of an empty value list. No parameter is emitted, which
        ///         keeps positional (<c>?</c>) dialects consistent.
        ///     </para>
        /// </summary>
        protected virtual void RenderEmptyValueList(ExpandableParameterCommandFragment fragment, RenderContext context)
        {
            context.Sql.Append(fragment.EmptyListTemplate ?? "NULL");
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
        protected virtual void RenderRepeatingFragment(RepeatingCommandFragment fragment, RenderContext context)
        {
            var value = context.ResolveValue(fragment.QueryParameter);
            if (value is null || value is DBNull)
            {
                this.RenderEmptyRepetition(fragment, context);
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
                    context.Sql.Append(fragment.Separator);

                // The collection's parameter stands for this element while its copy renders, so the template's
                // ordinary parameter marker binds the element rather than the whole collection.
                context.BeginElement(fragment.QueryParameter, element);
                try
                {
                    this.RenderFragments(fragment.Template, context);
                }
                finally
                {
                    context.EndElement();
                }

                count++;
            }

            if (count == 0)
                this.RenderEmptyRepetition(fragment, context);
        }

        /// <summary>
        ///     Writes self-contained SQL in place of a repetition that has no elements. No parameter is bound.
        /// </summary>
        protected virtual void RenderEmptyRepetition(RepeatingCommandFragment fragment, RenderContext context)
        {
            context.Sql.Append(fragment.WhenEmpty ?? "1 = 0");
        }

        /// <summary>
        ///     Writes <c>col = @p</c> or <c>col IS NULL</c>, picked from the value this execution binds.
        /// </summary>
        protected virtual void RenderNullSwitchFragment(NullSwitchCommandFragment fragment, RenderContext context)
        {
            var value = context.ResolveValue(fragment.QueryParameter);
            this.RenderFragments(fragment.SelectBranch(value), context);
        }

        /// <summary>
        ///     Writes the filter, or the always-true term standing in for it, picked from the guard's value
        ///     this execution binds.
        /// </summary>
        protected virtual void RenderOptionalPredicateFragment(OptionalPredicateCommandFragment fragment, RenderContext context)
        {
            var guardValue = context.ResolveValue(fragment.QueryParameter);
            this.RenderFragments(fragment.SelectBranch(guardValue), context);
        }
    }
}
