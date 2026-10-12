using Atis.Orm.Annotations;
using Atis.Orm.Metadata;
using Atis.SqlExpressionEngine.SqlExpressions;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Backlog item 22. When a property is declared on a base class, the C# compiler puts the
    ///         <i>base</i> <c>PropertyInfo</c> into the expression tree, not the derived one. Anything that
    ///         reads an annotation straight off that member can miss an attribute written on the derived
    ///         side (an <c>override</c>, or a <c>new</c> property that hides the base one).
    ///     </para>
    ///     <para>
    ///         Every test here queries a <i>derived</i> entity whose interesting members come from a base
    ///         class, and checks that the mapping that comes out is the one the derived class asks for.
    ///         No database is involved: it is all translation and metadata.
    ///     </para>
    /// </summary>
    [TestClass]
    public class BaseClassMemberResolutionTests : TestBase
    {
        #region entities

        [DbTable("InhOwner")]
        public class InhOwner
        {
            public int OwnerId { get; set; }
            public string OwnerName { get; set; }
        }

        [DbTable("InhPart")]
        public class InhPart
        {
            public int PartId { get; set; }
            public int InhId { get; set; }
            public string PartName { get; set; }
        }

        /// <summary>A generic base, the usual <c>EntityBase&lt;TKey&gt;</c> shape.</summary>
        public abstract class InhBase<TKey>
        {
            [PrimaryKey]
            public TKey Id { get; set; }

            // annotation on the base property, plain property
            [DbColumn("BASE_NM")]
            public string Name { get; set; }

            // annotation on the base property, derived class overrides WITHOUT repeating it
            [DbColumn("BASE_CODE")]
            public virtual string Code { get; set; }

            // no annotation on the base property, derived class overrides AND annotates
            public virtual string Region { get; set; }

            // base property that the derived class hides with a different type
            public int Level { get; set; }

            public int OwnerId { get; set; }

            [NavigationLink(NavigationType.ToParent, nameof(InhOwner.OwnerId), nameof(OwnerId))]
            public Func<InhOwner> NavOwner { get; set; }

            [NavigationLink(NavigationType.ToChildren, nameof(Id), nameof(InhPart.InhId))]
            public IQueryable<InhPart> NavParts { get; set; }

            // calculated property, with its expression, declared on the base only
            [CalculatedProperty(nameof(CalcLabelExpression))]
            public string CalcLabel => CalcLabelCompiled(this);
            public static readonly Expression<Func<InhBase<TKey>, string>> CalcLabelExpression = e => e.Name + "!";
            public static readonly Func<InhBase<TKey>, string> CalcLabelCompiled = CalcLabelExpression.Compile();
        }

        [DbTable("InhStudent")]
        public class InhStudent : InhBase<int>
        {
            public override string Code { get; set; }

            [DbColumn("DRV_REGION")]
            public override string Region { get; set; }

            public string Grade { get; set; }
        }

        /// <summary>A plain (non-generic) base, to tell a generic-base problem from an inheritance problem.</summary>
        public abstract class InhPlainBase
        {
            [DbColumn("PLAIN_NM")]
            public string Name { get; set; }

            [CalculatedProperty(nameof(CalcLabelExpression))]
            public string CalcLabel => CalcLabelCompiled(this);
            public static readonly Expression<Func<InhPlainBase, string>> CalcLabelExpression = e => e.Name + "!";
            public static readonly Func<InhPlainBase, string> CalcLabelCompiled = CalcLabelExpression.Compile();
        }

        [DbTable("InhPlainDerived")]
        public class InhPlainDerived : InhPlainBase
        {
            public int Extra { get; set; }
        }

        /// <summary>Kept apart from <see cref="InhStudent"/>: a hidden base property currently breaks the whole entity.</summary>
        [DbTable("InhHiddenStudent")]
        public class InhHiddenStudent : InhBase<int>
        {
            [DbColumn("DRV_LEVEL")]
            public new string Level { get; set; }
        }

        #endregion

        private string Render(Expression queryExpression)
        {
            var sqlExpression = this.ConvertExpressionToSqlExpression(queryExpression, out _);
            var translator = new SqlExpressionTranslator { IsRowNumberSupported = false };
            return translator.Translate(sqlExpression);
        }

        private static string Squash(string sql) => sql.Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");

        private IQueryable<InhStudent> Students() => new Queryable<InhStudent>(this.queryProvider);

        private static int CountOf(string text, string part)
        {
            int count = 0, index = 0;
            while ((index = text.IndexOf(part, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += part.Length;
            }
            return count;
        }

        #region query side: columns

        [TestMethod]
        public void Base_property_with_annotation_on_the_base_resolves_to_its_column()
        {
            var rendered = Squash(this.Render(this.Students().Where(x => x.Name == "a").Expression));
            StringAssert.Contains(rendered, "a_1.BASE_NM=");
        }

        [TestMethod]
        public void Base_key_of_a_generic_base_resolves_to_its_column()
        {
            var rendered = Squash(this.Render(this.Students().Where(x => x.Id == 5).Expression));
            StringAssert.Contains(rendered, "a_1.Id=");
        }

        [TestMethod]
        public void Override_without_annotation_keeps_the_annotation_of_the_base_property()
        {
            var rendered = Squash(this.Render(this.Students().Where(x => x.Code == "a").Expression));
            StringAssert.Contains(rendered, "a_1.BASE_CODE=");
        }

        [TestMethod]
        public void Override_with_annotation_on_the_derived_side_uses_the_derived_column()
        {
            var rendered = Squash(this.Render(this.Students().Where(x => x.Region == "a").Expression));
            StringAssert.Contains(rendered, "a_1.DRV_REGION=");
            Assert.IsFalse(rendered.Contains("a_1.Region"), rendered);
        }

        [TestMethod]
        public void New_property_hiding_a_base_property_uses_the_derived_column()
        {
            var students = new Queryable<InhHiddenStudent>(this.queryProvider);
            var rendered = Squash(this.Render(students.Where(x => x.Level == "3").Expression));
            StringAssert.Contains(rendered, "a_1.DRV_LEVEL=");
        }

        [TestMethod]
        public void Select_of_the_whole_entity_lists_each_column_exactly_once()
        {
            var rendered = Squash(this.Render(this.Students().Select(x => x).Expression));
            foreach (var column in new[] { "Id", "BASE_NM", "BASE_CODE", "DRV_REGION", "Level", "OwnerId", "Grade" })
                Assert.AreEqual(1, CountOf(rendered, $"a_1.{column}as"), $"'{column}' should appear once.\n{rendered}");
        }

        [TestMethod]
        public void OrderBy_and_Select_on_base_members_resolve_to_their_columns()
        {
            var rendered = Squash(this.Render(this.Students().OrderBy(x => x.Code).ThenBy(x => x.Region).Select(x => new { x.Name, x.Grade }).Expression));
            StringAssert.Contains(rendered, "orderbya_1.BASE_CODEasc,a_1.DRV_REGIONasc");
            StringAssert.Contains(rendered, "a_1.BASE_NM");
            StringAssert.Contains(rendered, "a_1.Grade");
        }

        #endregion

        #region query side: calculated property and navigations

        [TestMethod]
        public void Calculated_property_declared_on_the_base_is_expanded()
        {
            var rendered = Squash(this.Render(this.Students().Where(x => x.CalcLabel == "a!").Expression));
            StringAssert.Contains(rendered, "a_1.BASE_NM+");
        }

        [TestMethod]
        public void Calculated_property_declared_on_a_non_generic_base_is_expanded()
        {
            var q = new Queryable<InhPlainDerived>(this.queryProvider).Where(x => x.CalcLabel == "a!");
            var rendered = Squash(this.Render(q.Expression));
            StringAssert.Contains(rendered, "a_1.PLAIN_NM+");
        }

        [TestMethod]
        public void To_parent_navigation_declared_on_the_base_joins_the_parent()
        {
            var rendered = Squash(this.Render(this.Students().Where(x => x.NavOwner().OwnerName == "a").Expression));
            StringAssert.Contains(rendered, "InhOwner");
            StringAssert.Contains(rendered, ".OwnerId=a_1.OwnerId");
        }

        [TestMethod]
        public void To_children_navigation_declared_on_the_base_uses_the_base_key_column()
        {
            var rendered = Squash(this.Render(this.Students().Where(x => x.NavParts.Any()).Expression));
            StringAssert.Contains(rendered, "InhPart");
            StringAssert.Contains(rendered, "a_1.Id=");
        }

        #endregion

        #region persistence side: column kinds

        public abstract class InhCrudBase
        {
            [PrimaryKey]
            [DbIdentityColumn]
            public int Id { get; set; }

            [DbReadOnlyColumn]
            public DateTime? CreatedDate { get; set; }

            [DbNotMapped]
            public string Scratch { get; set; }

            // annotated on the base, overridden without the annotation
            [DbInsertOnly]
            public virtual string CreatedBy { get; set; }

            // not annotated on the base, overridden WITH an annotation
            public virtual string ModifiedBy { get; set; }
        }

        [DbTable("InhCrudStudent")]
        public class InhCrudStudent : InhCrudBase
        {
            public override string CreatedBy { get; set; }

            [DbUpdateOnly]
            public override string ModifiedBy { get; set; }

            public string Name { get; set; }
        }

        private static CrudColumn CrudColumnOf(EntityCrudMetadata metadata, string propertyName)
        {
            Assert.IsTrue(metadata.TryGetColumn(propertyName, out var column), $"Column '{propertyName}' should be mapped");
            return column;
        }

        [TestMethod]
        public void Column_kinds_written_on_base_properties_are_honoured()
        {
            using var dbc = new OrmDbContext();
            var crud = dbc.GetEntityCrudMetadata<InhCrudStudent>();

            Assert.AreEqual(ColumnKind.Identity, CrudColumnOf(crud, nameof(InhCrudBase.Id)).Kind);
            Assert.AreEqual(ColumnKind.ReadOnly, CrudColumnOf(crud, nameof(InhCrudBase.CreatedDate)).Kind);
            Assert.AreEqual(ColumnKind.Regular, CrudColumnOf(crud, nameof(InhCrudStudent.Name)).Kind);
        }

        [TestMethod]
        public void Override_without_annotation_keeps_the_column_kind_of_the_base_property()
        {
            using var dbc = new OrmDbContext();
            var crud = dbc.GetEntityCrudMetadata<InhCrudStudent>();

            Assert.AreEqual(ColumnKind.InsertOnly, CrudColumnOf(crud, nameof(InhCrudBase.CreatedBy)).Kind);
        }

        [TestMethod]
        public void Override_with_annotation_on_the_derived_side_gets_that_column_kind()
        {
            using var dbc = new OrmDbContext();
            var crud = dbc.GetEntityCrudMetadata<InhCrudStudent>();

            Assert.AreEqual(ColumnKind.UpdateOnly, CrudColumnOf(crud, nameof(InhCrudBase.ModifiedBy)).Kind);
        }

        [TestMethod]
        public void Not_mapped_base_property_is_neither_a_persistence_column_nor_a_query_column()
        {
            using var dbc = new OrmDbContext();
            var crud = dbc.GetEntityCrudMetadata<InhCrudStudent>();
            Assert.IsFalse(crud.TryGetColumn(nameof(InhCrudBase.Scratch), out _));

            var students = new Queryable<InhCrudStudent>(this.queryProvider);
            var rendered = Squash(this.Render(students.Select(x => x).Expression));
            Assert.IsFalse(rendered.Contains("Scratch"), rendered);
        }

        [TestMethod]
        public void Persistence_and_query_sides_list_the_same_columns_for_an_inherited_entity()
        {
            using var dbc = new OrmDbContext();
            var crud = dbc.GetEntityCrudMetadata<InhCrudStudent>().Columns.Select(x => x.Path.Alias).OrderBy(x => x).ToArray();
            var query = dbc.GetEntityMetadata<InhCrudStudent>().SqlColumns.Select(x => x.ModelPropertyName).OrderBy(x => x).ToArray();

            CollectionAssert.AreEqual(query, crud);
        }

        #endregion
    }
}
