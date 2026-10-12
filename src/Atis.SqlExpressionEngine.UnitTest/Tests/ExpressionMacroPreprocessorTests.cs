using Atis.Orm.Annotations;
using Atis.Orm.Preprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    [TestClass]
    public class ExpressionMacroPreprocessorTests
    {
        public static readonly Expression<Func<int, int>> DoubleExpression = x => x * 2;

        [ExpressionMacro(nameof(DoubleExpression))]
        public static int Double(int x) => x * 2;

        [ExpressionMacro(nameof(PropertyHeldExpression))]
        public static int ViaProperty(int x) => x + 1;
        public static Expression<Func<int, int>> PropertyHeldExpression => x => x + 1;

        [ExpressionMacro("DoesNotExist")]
        public static int Broken(int x) => x;

        [ExpressionMacro(nameof(NotALambda))]
        public static int WrongHolder(int x) => x;
        public static readonly string NotALambda = "oops";

        private static Expression Run(Expression<Func<int, int>> e) => new ExpressionMacroPreprocessor().Preprocess(e);

        [TestMethod]
        public void Call_is_replaced_by_the_lambda_body_with_arguments_substituted()
        {
            var result = (LambdaExpression)Run(y => Double(y + 1));
            Assert.AreEqual("y => ((y + 1) * 2)", result.ToString());
        }

        [TestMethod]
        public void Expression_may_be_held_in_a_static_property()
        {
            var result = (LambdaExpression)Run(y => ViaProperty(y));
            Assert.AreEqual("y => (y + 1)", result.ToString());
        }

        [TestMethod]
        public void Unmarked_calls_are_left_alone()
        {
            Expression<Func<int, int>> e = y => Math.Abs(y);
            Assert.AreSame(e, new ExpressionMacroPreprocessor().Preprocess(e));
        }

        [TestMethod]
        public void Missing_holder_is_reported_not_ignored()
        {
            var ex = Assert.ThrowsException<InvalidOperationException>(() => Run(y => Broken(y)));
            StringAssert.Contains(ex.Message, "DoesNotExist");
        }

        [TestMethod]
        public void Holder_that_is_not_a_lambda_is_reported()
        {
            var ex = Assert.ThrowsException<InvalidOperationException>(() => Run(y => WrongHolder(y)));
            StringAssert.Contains(ex.Message, "LambdaExpression");
        }
    }
}
