using System;
using System.Linq.Expressions;

namespace Atis.Orm.Abstractions
{
    public interface IPreprocessingRequirementTester
    {
        bool IsPreprocessingRequired(Expression originalExpression, Expression preprocessedExpression);
    }

    /// <summary>
    ///     <para>
    ///         Decides whether, on a cache hit, preprocessing must be re-run before the parameter values can be
    ///         re-extracted from an incoming expression.
    ///     </para>
    ///     <para>
    ///         Parameter values are read positionally from the variable (parameter) nodes of the tree. Those were
    ///         emitted by translating the <em>preprocessed</em> tree, so extracting from the original tree is only
    ///         safe when preprocessing did not change the ordered sequence of variable nodes. We compare the two
    ///         sequences by reference: an <see cref="ExpressionVisitor"/> reuses unchanged leaf nodes even when their
    ///         ancestors are rebuilt, so a captured-variable node that survives preprocessing is reference-identical
    ///         in both trees. Any add / remove / reorder of a variable node forces re-preprocessing. Injected
    ///         <em>constants</em> (e.g. from calculated-property expansion) are literals and are not collected as
    ///         variables, so they never force re-preprocessing on their own.
    ///     </para>
    /// </summary>
    public class PreprocessingRequirementTester : IPreprocessingRequirementTester
    {
        private readonly IExpressionVariableValuesExtractor variableValuesExtractor;

        public PreprocessingRequirementTester(IExpressionVariableValuesExtractor variableValuesExtractor)
        {
            this.variableValuesExtractor = variableValuesExtractor ?? throw new ArgumentNullException(nameof(variableValuesExtractor));
        }

        public bool IsPreprocessingRequired(Expression originalExpression, Expression preprocessedExpression)
        {
            var originalNodes = this.variableValuesExtractor.ExtractParameterNodes(originalExpression);
            var preprocessedNodes = this.variableValuesExtractor.ExtractParameterNodes(preprocessedExpression);

            if (originalNodes.Count != preprocessedNodes.Count)
                return true;

            for (int i = 0; i < originalNodes.Count; i++)
            {
                if (!ReferenceEquals(originalNodes[i], preprocessedNodes[i]))
                    return true;
            }

            return false;
        }
    }
}
