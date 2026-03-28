using System.Linq.Expressions;

namespace Atis.Orm
{
    public interface IPreprocessingRequirementTester
    {
        bool IsPreprocessingRequired(Expression originalExpression, Expression preprocessedExpression);
    }

    public class PreprocessingRequirementTester : IPreprocessingRequirementTester
    {
        public bool IsPreprocessingRequired(Expression originalExpression, Expression preprocessedExpression)
        {
            return true;
        }
    }
}