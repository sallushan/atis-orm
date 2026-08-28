using System;
using System.Collections;
using System.Collections.Generic;

using Atis.Orm.Abstractions;
using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.SqlExpressions;

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
    ///         overriding <see cref="CommandRenderPass.RenderFragment"/> on its own pass - no base class
    ///         changes.
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
        /// <param name="valueDelimiter">
        ///     Set when the value is one delimited string rather than a collection; see
        ///     <see cref="ValueDelimiter"/>.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="queryParameter"/> is <c>null</c>.</exception>
        public ExpandableParameterCommandFragment(IQueryParameter queryParameter, string emptyListTemplate, string valueDelimiter = null)
        {
            this.QueryParameter = queryParameter ?? throw new ArgumentNullException(nameof(queryParameter));
            this.EmptyListTemplate = emptyListTemplate;
            this.ValueDelimiter = valueDelimiter;
        }

        /// <summary>The parameter this marker stands in for.</summary>
        public IQueryParameter QueryParameter { get; }

        /// <summary>
        ///     <para>
        ///         When set, the bound value is one delimited string (<c>"HR,IT"</c>) that stands for the whole
        ///         list, and this is the text between entries. <c>null</c> - the usual case - means the value
        ///         is already a collection.
        ///     </para>
        ///     <para>
        ///         Decided by the translator from the expression, never inferred from the value: a string is
        ///         an ordinary single value nearly everywhere it appears, so reading one as a list has to be
        ///         asked for.
        ///     </para>
        /// </summary>
        public string ValueDelimiter { get; }

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
    ///         One piece of SQL emitted once per element of a collection value, with a separator between the
    ///         copies: <c>(col LIKE '%' + @p0 + '%') OR (col LIKE '%' + @p1 + '%')</c>. The whole term repeats,
    ///         which is what distinguishes this from <see cref="ExpandableParameterCommandFragment"/> - there
    ///         only the value list at one position grows.
    ///     </para>
    ///     <para>
    ///         It exists because SQL has no "LIKE any of these" operator, so a multi-value LIKE has to be
    ///         spelled out as a disjunction whose length follows the collection - and the collection is not
    ///         knowable when the query is compiled, because one compiled query is re-executed with collections
    ///         of every length.
    ///     </para>
    ///     <para>
    ///         <strong>Inside <see cref="Template"/>, <see cref="QueryParameter"/> stands for the element
    ///         being rendered, not the collection.</strong> The renderer binds it per copy, so the template
    ///         holds one ordinary parameter marker and needs no element-specific fragment type. That is also
    ///         why the copies get consecutive placeholder names rather than sharing one.
    ///     </para>
    /// </summary>
    public class RepeatingCommandFragment : ICommandFragment
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RepeatingCommandFragment"/> class.
        /// </summary>
        /// <param name="queryParameter">The collection parameter, and the element marker inside the template.</param>
        /// <param name="template">The SQL emitted once per element.</param>
        /// <param name="separator">Text written between consecutive copies.</param>
        /// <param name="whenEmpty">Self-contained SQL emitted when there are no elements at all.</param>
        /// <param name="valueDelimiter">
        ///     Set when the value is one delimited string rather than a collection; see
        ///     <see cref="ExpandableParameterCommandFragment.ValueDelimiter"/>.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="queryParameter"/> or <paramref name="template"/> is <c>null</c>.</exception>
        public RepeatingCommandFragment(IQueryParameter queryParameter, IReadOnlyList<ICommandFragment> template, string separator, string whenEmpty, string valueDelimiter = null)
        {
            this.QueryParameter = queryParameter ?? throw new ArgumentNullException(nameof(queryParameter));
            this.Template = template ?? throw new ArgumentNullException(nameof(template));
            this.Separator = separator ?? string.Empty;
            this.WhenEmpty = whenEmpty;
            this.ValueDelimiter = valueDelimiter;
        }

        /// <summary>
        ///     When set, the bound value is one delimited string standing for the whole collection, and this is
        ///     the text between entries. Each entry still becomes its own copy of the template, so the
        ///     rendered SQL is identical to the one a real collection produces.
        /// </summary>
        public string ValueDelimiter { get; }

        /// <summary>
        ///     The parameter holding the collection. Inside <see cref="Template"/> the same parameter resolves
        ///     to the single element currently being rendered.
        /// </summary>
        public IQueryParameter QueryParameter { get; }

        /// <summary>The SQL emitted once per element, with the element's marker in it.</summary>
        public IReadOnlyList<ICommandFragment> Template { get; }

        /// <summary>Text written between consecutive copies (<c> OR </c> for a multi-value LIKE).</summary>
        public string Separator { get; }

        /// <summary>
        ///     <para>
        ///         Self-contained SQL emitted when the collection has no elements, so the position is still
        ///         filled: a disjunction of nothing matches nothing, hence <c>1 = 0</c>.
        ///     </para>
        ///     <para>
        ///         Normally unreachable in the <see cref="WhereBuilder"/> shapes, where an empty collection
        ///         drops the surrounding optional term before this fragment is ever reached. It is the answer
        ///         for anywhere else this fragment is used, and it keeps the fragment valid on its own.
        ///     </para>
        /// </summary>
        public string WhenEmpty { get; }

        /// <summary>
        ///     <para>
        ///         Always <c>true</c>: how many copies reach the output follows the collection's length, which
        ///         is only known once a value is bound.
        ///     </para>
        /// </summary>
        public bool RequirePerExecutionRendering => true;
    }

    /// <summary>
    ///     <para>
    ///         A predicate term in one of two spellings: the filter itself when its guard has a value at
    ///         execution time, or a standing-true placeholder when it does not. The renderer emits exactly
    ///         one of them - never both - so it needs to know nothing about what either contains.
    ///     </para>
    ///     <para>
    ///         This is what makes an optional WHERE term possible without a cache entry per combination of
    ///         supplied values: one compiled query holds every term, and each execution decides which survive.
    ///         <see cref="WhenAbsent"/> is load-bearing - a term that renders to nothing would leave a dangling
    ///         operator behind, so the dropped state still has to be valid boolean SQL.
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
        /// <param name="queryParameter">The parameter whose value decides which branch is emitted.</param>
        /// <param name="guardKind">What counts as "no value" for the guard.</param>
        /// <param name="whenAbsent">Emitted when the guard has no value - a term that is always true.</param>
        /// <param name="whenPresent">The filter itself, emitted when the guard has a value.</param>
        /// <param name="valueDelimiter">
        ///     Set when the guard's value is one delimited string rather than a collection; see
        ///     <see cref="ValueDelimiter"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">Any fragment list is <c>null</c>.</exception>
        public OptionalPredicateCommandFragment(IQueryParameter queryParameter, OptionalGuardKind guardKind, IReadOnlyList<ICommandFragment> whenAbsent, IReadOnlyList<ICommandFragment> whenPresent, string valueDelimiter = null)
        {
            this.QueryParameter = queryParameter ?? throw new ArgumentNullException(nameof(queryParameter));
            this.GuardKind = guardKind;
            this.WhenAbsent = whenAbsent ?? throw new ArgumentNullException(nameof(whenAbsent));
            this.WhenPresent = whenPresent ?? throw new ArgumentNullException(nameof(whenPresent));
            this.ValueDelimiter = valueDelimiter;
        }

        /// <summary>
        ///     <para>
        ///         When set, the guard's value is one delimited string standing for a list of values, and this
        ///         is the text between entries. It is what lets <see cref="IsAbsent"/> tell a string that
        ///         holds no values (<c>""</c>, <c>" "</c>, <c>","</c>) from one that holds some.
        ///     </para>
        /// </summary>
        public string ValueDelimiter { get; }

        /// <summary>The parameter whose value decides which branch is emitted.</summary>
        public IQueryParameter QueryParameter { get; }

        /// <summary>
        ///     What counts as "no value" here. Decided by the translator from the expression tree, never
        ///     inferred from the value - an empty <c>byte[]</c> is not an absent filter.
        /// </summary>
        public OptionalGuardKind GuardKind { get; }

        /// <summary>
        ///     Emitted when the guard has no value: a term that is always true (<c>1 = 1</c>), so the filter
        ///     stops narrowing the result while the statement stays valid.
        /// </summary>
        public IReadOnlyList<ICommandFragment> WhenAbsent { get; }

        /// <summary>The filter itself, emitted when the guard has a value.</summary>
        public IReadOnlyList<ICommandFragment> WhenPresent { get; }

        /// <summary>Picks the branch <paramref name="guardValue"/> calls for.</summary>
        public IReadOnlyList<ICommandFragment> SelectBranch(object guardValue)
            => this.IsAbsent(guardValue) ? this.WhenAbsent : this.WhenPresent;

        /// <summary>Whether <paramref name="guardValue"/> means this term should be dropped.</summary>
        public bool IsAbsent(object guardValue)
        {
            if (guardValue is null || guardValue is DBNull)
                return true;

            if (this.GuardKind != OptionalGuardKind.NullOrEmptyCollection)
                return false;

            // A delimited string holds a list, so "empty" is about what it splits into, not about the string
            // itself: "," and " " name no values and drop the term, exactly as an empty collection does.
            if (this.ValueDelimiter != null)
                return SqlDelimitedValuesExpression.Split(guardValue, this.ValueDelimiter).Count == 0;

            return guardValue is IEnumerable collection
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
