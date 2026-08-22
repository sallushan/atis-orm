using System;
using System.Collections;
using System.Collections.Generic;

using Atis.Orm.Abstractions;
using Atis.SqlExpressionEngine;

namespace Atis.Orm.Translation
{
    /// <summary>
    ///     <para>
    ///         A single piece of a translated command. The translator emits an ordered sequence of these
    ///         instead of a flat string, so the exact position of every parameter is known and the parts
    ///         whose spelling depends on this execution's values can be decided at render time.
    ///     </para>
    ///     <para>
    ///         Fragments carry positions and structure, never rendering policy: <see cref="ICommandRenderer"/>
    ///         walks them together with the execution-time values to produce the command text and its
    ///         parameter bindings. A provider adds a fragment type by implementing this interface and
    ///         overriding one method on <see cref="CommandRenderer"/> - no base class changes.
    ///     </para>
    /// </summary>
    public interface ICommandFragment
    {
        /// <summary>
        ///     <para>
        ///         Whether this fragment's rendered output depends on the values bound at execution time, so
        ///         the command text cannot be rendered once and reused.
        ///     </para>
        ///     <para>
        ///         <b>Contract for composite fragments:</b> a fragment holding child fragments must report
        ///         <c>true</c> whenever any child would - either by asking its children or, when the composite
        ///         is value-dependent by its own nature (it picks a branch, or drops one), by returning
        ///         <c>true</c> outright. The translator only inspects the top level of the fragment list, so a
        ///         composite that under-reports hides its children and freezes a value-dependent command.
        ///     </para>
        /// </summary>
        bool RequirePerExecutionRendering { get; }
    }

    /// <summary>
    ///     <para>A run of literal command text (keywords, identifiers, aliases, punctuation).</para>
    /// </summary>
    public class TextCommandFragment : ICommandFragment
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TextCommandFragment"/> class with the specified text.
        /// </summary>
        /// <param name="text">The literal command text of this run.</param>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <c>null</c>.</exception>
        public TextCommandFragment(string text)
        {
            this.Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>The literal command text of this run.</summary>
        public string Text { get; }

        /// <inheritdoc />
        public bool RequirePerExecutionRendering => false;
    }

    /// <summary>
    ///     <para>
    ///         Marks the exact point where a parameter's placeholder (e.g. <c>@p0</c>) sits in the output.
    ///         Exactly one placeholder is emitted here, whatever the value turns out to be - a
    ///         <c>byte[]</c> compared with <c>=</c> is a collection value too and must stay one parameter.
    ///     </para>
    /// </summary>
    public class ParameterCommandFragment : ICommandFragment
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ParameterCommandFragment"/> class.
        /// </summary>
        /// <param name="queryParameter">The parameter this marker stands in for.</param>
        /// <exception cref="ArgumentNullException"><paramref name="queryParameter"/> is <c>null</c>.</exception>
        public ParameterCommandFragment(IQueryParameter queryParameter)
        {
            this.QueryParameter = queryParameter ?? throw new ArgumentNullException(nameof(queryParameter));
        }

        /// <summary>The parameter this marker stands in for.</summary>
        public IQueryParameter QueryParameter { get; }

        /// <summary>
        ///     <para>
        ///         Always <c>false</c>: the placeholder count and the text around it are the same on every
        ///         execution, so only the bound value changes.
        ///     </para>
        /// </summary>
        public bool RequirePerExecutionRendering => false;
    }

    /// <summary>
    ///     <para>
    ///         Marks a position that accepts a comma-separated list of values (an <c>IN</c> list, a
    ///         <c>CONCAT_WS</c> value operand, ...), so a collection value expands into one placeholder per
    ///         element at render time.
    ///     </para>
    ///     <para>
    ///         Expansion is decided by the translator at emit time, never inferred from the value. Because a
    ///         compiled query is cached by expression shape and re-executed with collections of different
    ///         lengths, the placeholder count can only be settled per execution.
    ///     </para>
    /// </summary>
    public class ExpandableParameterCommandFragment : ICommandFragment
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExpandableParameterCommandFragment"/> class.
        /// </summary>
        /// <param name="queryParameter">The parameter this marker stands in for.</param>
        /// <param name="emptyListTemplate">Self-contained SQL emitted when the collection is empty.</param>
        /// <exception cref="ArgumentNullException"><paramref name="queryParameter"/> is <c>null</c>.</exception>
        public ExpandableParameterCommandFragment(IQueryParameter queryParameter, string emptyListTemplate)
        {
            this.QueryParameter = queryParameter ?? throw new ArgumentNullException(nameof(queryParameter));
            this.EmptyListTemplate = emptyListTemplate;
        }

        /// <summary>The parameter this marker stands in for.</summary>
        public IQueryParameter QueryParameter { get; }

        /// <summary>
        ///     <para>
        ///         Self-contained SQL emitted in place of the value list when the collection is empty; no
        ///         parameter is bound. The template must be valid where it lands: <c>IN (SELECT NULL WHERE
        ///         1 = 0)</c> matches nothing and negates correctly under <c>NOT IN</c>.
        ///     </para>
        /// </summary>
        public string EmptyListTemplate { get; }

        /// <summary>
        ///     <para>
        ///         Always <c>true</c>: the number of placeholders follows the collection's length, which is
        ///         only known once a value is bound.
        ///     </para>
        /// </summary>
        public bool RequirePerExecutionRendering => true;
    }

    /// <summary>
    ///     <para>
    ///         Two alternative spellings of the same comparison, one for when <see cref="QueryParameter"/>
    ///         holds a value and one for when it is null. The renderer emits exactly one of them, chosen from
    ///         the value this execution binds.
    ///     </para>
    ///     <para>
    ///         C# and SQL disagree about <c>null</c>: <c>x.Col == v</c> means "both are null" in C# but is
    ///         never true in SQL when <c>v</c> is null. The comparison therefore has to be spelled
    ///         <c>col = @p</c> or <c>col IS NULL</c> depending on the value - and the value is not knowable
    ///         when the query is compiled, because a compiled query is cached by expression shape and
    ///         re-executed with whatever the caller supplies next.
    ///     </para>
    ///     <para>
    ///         Deciding at render time rather than at translation time is what keeps this to <strong>one</strong>
    ///         cache entry per call site. Folding the comparison at translation time bakes one execution's
    ///         answer into the cached SQL for every later one; putting the value's nullness into the cache key
    ///         instead is correct but costs up to 2^n entries for n nullable parameters, in a cache that does
    ///         not evict. Both branches are built by the translator, so no translation logic moves here.
    ///     </para>
    /// </summary>
    public class NullSwitchCommandFragment : ICommandFragment
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NullSwitchCommandFragment"/> class.
        /// </summary>
        /// <param name="queryParameter">The parameter whose value picks the branch.</param>
        /// <param name="whenNull">The null-test form, emitted when the value is null.</param>
        /// <param name="whenNotNull">The ordinary comparison, emitted when the value is not null.</param>
        /// <exception cref="ArgumentNullException">Any argument is <c>null</c>.</exception>
        public NullSwitchCommandFragment(IQueryParameter queryParameter, IReadOnlyList<ICommandFragment> whenNull, IReadOnlyList<ICommandFragment> whenNotNull)
        {
            this.QueryParameter = queryParameter ?? throw new ArgumentNullException(nameof(queryParameter));
            this.WhenNull = whenNull ?? throw new ArgumentNullException(nameof(whenNull));
            this.WhenNotNull = whenNotNull ?? throw new ArgumentNullException(nameof(whenNotNull));
        }

        /// <summary>
        ///     <para>The parameter whose value picks the branch.</para>
        ///     <para>
        ///         It is not written to the output itself - <see cref="WhenNotNull"/> contains its own marker
        ///         for that - but it resolves through the same resolver as any other parameter, so it rebinds
        ///         by identity on a cache hit.
        ///     </para>
        /// </summary>
        public IQueryParameter QueryParameter { get; }

        /// <summary>
        ///     The null-test form (<c>col IS NULL</c>), emitted when the value is null. Binds no parameter for
        ///     the compared value, so a marker inside it would be wrong - there is nothing to send.
        /// </summary>
        public IReadOnlyList<ICommandFragment> WhenNull { get; }

        /// <summary>The comparison as ordinary SQL (<c>col = @p</c>), emitted when the value is not null.</summary>
        public IReadOnlyList<ICommandFragment> WhenNotNull { get; }

        /// <summary>Picks the branch <paramref name="value"/> calls for.</summary>
        public IReadOnlyList<ICommandFragment> SelectBranch(object value)
            => value is null || value is DBNull ? this.WhenNull : this.WhenNotNull;

        /// <summary>
        ///     <para>
        ///         Always <c>true</c>: which branch reaches the output is decided from the bound value, so the
        ///         command text is value-dependent whatever the branches contain.
        ///     </para>
        /// </summary>
        public bool RequirePerExecutionRendering => true;
    }

    /// <summary>
    ///     <para>
    ///         An <c>AND</c>-ed predicate term that is emitted only when its guard has a value at execution
    ///         time; otherwise only the <c>1 = 1</c> anchor survives and the filter disappears.
    ///     </para>
    ///     <para>
    ///         This is what makes an optional WHERE term possible without a cache entry per combination of
    ///         supplied values: one compiled query holds every term, and each execution decides which survive.
    ///         The anchor is load-bearing - it keeps the group valid boolean SQL in both states, so the
    ///         surrounding predicate needs no separator bookkeeping when a term vanishes.
    ///     </para>
    ///     <para>
    ///         The guard is never itself written to the output, so it has no placeholder. Its value is read
    ///         through the same resolver as any other parameter, so it rebinds by identity on a cache hit.
    ///     </para>
    /// </summary>
    public class OptionalPredicateCommandFragment : ICommandFragment
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OptionalPredicateCommandFragment"/> class.
        /// </summary>
        /// <param name="queryParameter">The parameter whose value decides whether the term is emitted.</param>
        /// <param name="guardKind">What counts as "no value" for the guard.</param>
        /// <param name="oneEqualOne">The anchor, emitted unconditionally.</param>
        /// <param name="actualPredicate">The term itself, emitted only when the guard has a value.</param>
        /// <exception cref="ArgumentNullException">Any fragment list is <c>null</c>.</exception>
        public OptionalPredicateCommandFragment(IQueryParameter queryParameter, OptionalGuardKind guardKind, IReadOnlyList<ICommandFragment> oneEqualOne, IReadOnlyList<ICommandFragment> actualPredicate)
        {
            this.QueryParameter = queryParameter ?? throw new ArgumentNullException(nameof(queryParameter));
            this.GuardKind = guardKind;
            this.OneEqualOne = oneEqualOne ?? throw new ArgumentNullException(nameof(oneEqualOne));
            this.ActualPredicate = actualPredicate ?? throw new ArgumentNullException(nameof(actualPredicate));
        }

        /// <summary>The parameter whose value decides whether <see cref="ActualPredicate"/> is emitted.</summary>
        public IQueryParameter QueryParameter { get; }

        /// <summary>
        ///     What counts as "no value" here. Decided by the translator from the expression tree, never
        ///     inferred from the value - an empty <c>byte[]</c> is not an absent filter.
        /// </summary>
        public OptionalGuardKind GuardKind { get; }

        /// <summary>The <c>1 = 1</c> anchor, emitted whether or not the guard has a value.</summary>
        public IReadOnlyList<ICommandFragment> OneEqualOne { get; }

        /// <summary>
        ///     The term itself - including the leading <c>AND</c> - emitted only when the guard has a value.
        /// </summary>
        public IReadOnlyList<ICommandFragment> ActualPredicate { get; }

        /// <summary>Whether <paramref name="guardValue"/> means this term should be dropped.</summary>
        public bool IsAbsent(object guardValue)
        {
            if (guardValue is null || guardValue is DBNull)
                return true;

            return this.GuardKind == OptionalGuardKind.NullOrEmptyCollection
                   && guardValue is IEnumerable collection
                   && !(guardValue is string)
                   && !collection.GetEnumerator().MoveNext();
        }

        /// <summary>
        ///     <para>
        ///         Always <c>true</c>: whether the term reaches the output is decided from the guard's value,
        ///         so the command text is value-dependent whatever the term contains.
        ///     </para>
        /// </summary>
        public bool RequirePerExecutionRendering => true;
    }
}
