using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Atis.Expressions
{
    /// <summary>
    ///     <para>
    ///         Represents an expression preprocessor that modifies or replaces a given expression node
    ///         during the preprocessing phase of expression tree traversal.
    ///     </para>
    ///     <para>
    ///         <strong>Contract — a preprocessor must be shape-deterministic.</strong> Two expressions that
    ///         are structurally equal apart from the <em>values</em> of their variable nodes must preprocess
    ///         to structurally equal results. A preprocessor may freely change shape — expand, inline,
    ///         rewrite — but that change must be a function of the expression's shape and of stable
    ///         configuration (model metadata, mappings), <strong>never of a variable's runtime value</strong>.
    ///     </para>
    ///     <para>
    ///         Two consequences follow, and both are part of the contract:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             A preprocessor must not read a variable's value in order to decide what to emit.
    ///         </item>
    ///         <item>
    ///             A variable node a preprocessor introduces is treated as a <em>literal</em>: its value is
    ///             frozen at the moment the query first compiled and is reused by every later execution.
    ///             Introducing one is therefore legal only when the value is genuinely stable configuration.
    ///             Anything that must vary per execution has to reach the tree through the caller's own
    ///             expression, or through a <em>contextual</em> node (resolved per execution from ambient
    ///             context).
    ///         </item>
    ///     </list>
    ///     <para>
    ///         The freezing is not a courtesy — it is forced. A compiled query is cached under the key of the
    ///         original expression and rebinds its values from that same tree, so an identity that exists only
    ///         in the preprocessed tree has nothing to be re-extracted from, ever. Value resolution therefore
    ///         falls back to the parameter's translation-time value whenever the re-extracted set has no entry
    ///         for it (see <c>CompiledQueryBase.ResolveValue</c>).
    ///     </para>
    ///     <para>
    ///         Why this matters: a compiled query is cached under the key of the <em>original</em>
    ///         expression, while its SQL is produced from the <em>preprocessed</em> tree of whichever
    ///         execution compiled it first. Later executions reuse that SQL and only rebind parameter
    ///         values. A value-dependent preprocessor therefore yields cached SQL that does not correspond
    ///         to a later execution's values — surfacing either as a rebinding failure or, worse, as a
    ///         silently wrong predicate.
    ///     </para>
    ///     <para>
    ///         The engine <em>cannot</em> verify this contract in general: determinism is a property across
    ///         executions, and any single execution looks internally consistent. Only one symptom — a
    ///         preprocessor adding variable nodes — is detectable, and only at compile time. Everything else
    ///         rests on implementers honouring what is written here.
    ///     </para>
    ///     <para>
    ///         A preprocessor must also be <strong>idempotent</strong>. Providers re-run the whole
    ///         preprocessor set until the expression stops changing, so a preprocessor that keeps producing
    ///         a new tree for an already-processed input will spin until the iteration cap trips.
    ///     </para>
    /// </summary>
    public interface IExpressionPreprocessor
    {
        /// <summary>
        ///     <para>
        ///         Initializes the Expression Preprocessor instance.
        ///     </para>
        /// </summary>
        void Initialize();

        /// <summary>
        ///     <para>
        ///         Processes and potentially transforms the given expression node based on 
        ///         custom preprocessing logic.
        ///     </para>
        /// </summary>
        /// <param name="expression">The expression to preprocess.</param>
        /// <returns>The transformed or unmodified expression.</returns>
        Expression Preprocess(Expression expression);
    }

}
