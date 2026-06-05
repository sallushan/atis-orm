using Atis.Expressions;
using Atis.Orm;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionConverters;
using Atis.SqlExpressionEngine.Preprocessors;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;
using Atis.SqlExpressionEngine.UnitTest.Converters;

//using Atis.SqlExpressionEngine.UnitTest.Converters;
using Atis.SqlExpressionEngine.UnitTest.Preprocessors;
using Atis.SqlExpressionEngine.UnitTest.Services;
using System.Diagnostics;


//using Atis.SqlExpressionEngine.UnitTest.Services;
using System.Linq.Expressions;
using Model = Atis.SqlExpressionEngine.UnitTest.Services.Model;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    public class DataContext
    {
        public IQueryable<Invoice> Invoices { get; } = new Queryable<Invoice>(new QueryProvider());
    }

    public abstract class TestBase
    {
        protected readonly IQueryProvider queryProvider = new QueryProvider();
        protected readonly DataContext dataContext = new();
        protected readonly Stopwatch stopwatch = new Stopwatch();

        #region base methods
        protected void Test(string testHeading, Expression queryExpression, string expectedResult)
        {
            SqlExpression? result = null;
            Expression updatedQueryExpression = queryExpression;

            try
            {
                result = ConvertExpressionToSqlExpression(queryExpression, out updatedQueryExpression);
            }
            catch
            {
                printExpressions();
                throw;
            }

            //var toStringResult = ObjectGraphVisualizer.Dump(result);
            //Console.WriteLine(toStringResult);

            string resultQuery = null;
            if (result != null)
            {
                var translator = new SqlExpressionTranslator()
                {
                    IsRowNumberSupported = false,
                };
                try
                {
                    //if (result is SqlDerivedTableExpression derivedTable)
                    //{
                    //    var sqlQueryShapePrinter = new SqlQueryShapePrinter();
                    //    var shapeInString = sqlQueryShapePrinter.PrintShape(derivedTable.QueryShape, derivedTable.SelectColumnCollection.SelectColumns);
                    //    Console.WriteLine(shapeInString);
                    //}

                    resultQuery = translator.Translate(result);
                }
                catch
                {
                    printExpressions();
                    throw;
                }
                Console.WriteLine($"+++++++++++++++++++++++++ {testHeading} ++++++++++++++++++++++++");
                Console.WriteLine(resultQuery);
                Console.WriteLine("-----------------------------------------------------------------");
            }

            printExpressions();

            if (expectedResult != null && resultQuery != null)
            {
                ValidateQueryResults(resultQuery, expectedResult);
            }


            void printExpressions()
            {
                Console.WriteLine("Original Expression:");
                Console.WriteLine(ExpressionPrinter.PrintExpression(queryExpression));
                Console.WriteLine("Expression after Preprocessing:");
                Console.WriteLine(ExpressionPrinter.PrintExpression(updatedQueryExpression));
            }

        }

        protected SqlExpression? ConvertExpressionToSqlExpression(Expression queryExpression, out Expression updatedQueryExpression)
        {
            var model = new Model(new ReflectionService());
            updatedQueryExpression = PreprocessExpression(queryExpression, model);
            var sqlDataTypeFactory = new SqlDataTypeFactory();
            var reflectionService = new ReflectionService();
            var parameterMapper = new LambdaParameterToDataSourceMapper();
            var sqlFactory = new SqlExpressionFactory();
            var expressionEvaluator = new ExpressionEvaluator();
            var logger = new Services.Logger();
            var serviceCollection = new object[] { sqlDataTypeFactory, sqlFactory, model, parameterMapper, reflectionService, logger, expressionEvaluator };
            var converterServiceProvider = new ExpressionConverterDependencyProviderByCollection(serviceCollection);
            var factoryProvider = new LinqToSqlConverterFactoryProvider(reflectionService, expressionEvaluator, userProvidedFactories: [new SqlFunctionConverterFactory()]);
            var treeConverter = new LinqToSqlExpressionTreeConverter(converterServiceProvider, factoryProvider);
            var postProcessorProvider = new SqlExpressionPostprocessorProvider(postprocessors: []);
            var linqToSqlConverter = new LinqToSqlConverter(treeConverter, postProcessorProvider);

            return linqToSqlConverter.Convert(updatedQueryExpression); // Let exception bubble up
        }


        protected Expression PreprocessExpression(Expression expression, IModel model)
        {
            var expressionEvaluator = new ExpressionEvaluator();
            var reflectionService = new ReflectionService();
            var preprocessor = new OrmExpressionPreprocessorProvider(model, reflectionService, expressionEvaluator, plugins: new[] { new CustomBusinessMethodPreprocessor() });
            expression = preprocessor.Preprocess(expression);
            return expression;
        }

        private void ValidateQueryResults(string convertedQuery, string expectedQuery)
        {
            convertedQuery = SimplifyQuery(convertedQuery);
            expectedQuery = SimplifyQuery(expectedQuery);
            if (string.Compare(convertedQuery, expectedQuery, true) != 0)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("ERROR: Converted query is not as expected.");
                Console.ResetColor();
                Assert.Fail("Query is not matching");
            }
        }

        private string SimplifyQuery(string query)
        {
            query = query.Trim();
            if (query.StartsWith("("))
                query = query.Substring(1, query.Length - 2);
            query = query.Trim();
            query = query.Replace("\r\n", "").Replace("\r", "").Replace("\n", " ").Replace("\t", "").Replace(" ", "").ToLower();
            //while (query.Contains("  "))
            //{
            //    query = query.Replace("  ", " ");
            //}
            return query;
        }
        #endregion
    }
}
