using System.Linq;

using Atis.Orm.Metadata;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Covers the persistence side of the mapping — column kinds, required fields and column
    ///         exclusion — and the invariant that it never disturbs the query side.
    ///     </para>
    ///     <para>
    ///         No database is involved: everything here is metadata.
    ///     </para>
    /// </summary>
    [TestClass]
    public class ColumnKindMetadataTests
    {
        private static CrudColumn Column(EntityCrudMetadata metadata, string propertyName)
        {
            Assert.IsTrue(metadata.TryGetColumn(propertyName, out var column), $"Column '{propertyName}' should be mapped");
            return column;
        }

        [TestMethod]
        public void Annotations_produce_expected_column_kinds()
        {
            using var dbc = new OrmDbContext();

            // CrudAnnotatedEmployee is never mentioned in OnModelCreating, so this also exercises the
            // on-demand build for an entity the model has not seen.
            var crud = dbc.GetEntityCrudMetadata<CrudAnnotatedEmployee>();

            Assert.AreEqual(ColumnKind.Identity, Column(crud, nameof(CrudAnnotatedEmployee.EmployeeId)).Kind);
            Assert.AreEqual(ColumnKind.InsertOnly, Column(crud, nameof(CrudAnnotatedEmployee.CreatedBy)).Kind);
            Assert.AreEqual(ColumnKind.UpdateOnly, Column(crud, nameof(CrudAnnotatedEmployee.ModifiedBy)).Kind);
            Assert.AreEqual(ColumnKind.ReadOnly, Column(crud, nameof(CrudAnnotatedEmployee.CreatedDate)).Kind);
            Assert.AreEqual(ColumnKind.RowVersion, Column(crud, nameof(CrudAnnotatedEmployee.RowVer)).Kind);

            // an un-annotated property is an ordinary column
            Assert.AreEqual(ColumnKind.Regular, Column(crud, nameof(CrudAnnotatedEmployee.Salary)).Kind);
            Assert.AreEqual(ColumnKind.Regular, Column(crud, nameof(CrudAnnotatedEmployee.FirstName)).Kind);
        }

        [TestMethod]
        public void Required_field_annotation_surfaces_with_its_title()
        {
            using var dbc = new OrmDbContext();
            var crud = dbc.GetEntityCrudMetadata<CrudAnnotatedEmployee>();

            var firstName = Column(crud, nameof(CrudAnnotatedEmployee.FirstName));
            Assert.IsTrue(firstName.IsRequired);
            Assert.AreEqual("First Name", firstName.RequiredFieldTitle);

            // no explicit title means the property name is used at validation time
            var lastName = Column(crud, nameof(CrudAnnotatedEmployee.LastName));
            Assert.IsTrue(lastName.IsRequired);
            Assert.IsNull(lastName.RequiredFieldTitle);

            Assert.IsFalse(Column(crud, nameof(CrudAnnotatedEmployee.Salary)).IsRequired);
        }

        [TestMethod]
        public void Crud_column_exposes_the_property_it_maps_to()
        {
            using var dbc = new OrmDbContext();
            var crud = dbc.GetEntityCrudMetadata<CrudAnnotatedEmployee>();

            var rowVer = Column(crud, nameof(CrudAnnotatedEmployee.RowVer));
            Assert.AreEqual(typeof(byte[]), rowVer.ClrType);
            Assert.AreEqual(typeof(CrudAnnotatedEmployee), rowVer.Property.DeclaringType);
        }

        [TestMethod]
        public void DbNotMapped_property_is_absent_from_both_sides_of_the_mapping()
        {
            using var dbc = new OrmDbContext();
            // registers the query side for an entity that is configured purely by annotation
            dbc.CreateQuery<CrudAnnotatedEmployee>();

            var crud = dbc.GetEntityCrudMetadata<CrudAnnotatedEmployee>();
            var query = dbc.GetEntityMetadata<CrudAnnotatedEmployee>();

            Assert.IsFalse(crud.TryGetColumn(nameof(CrudAnnotatedEmployee.RecordState), out _),
                "A [DbNotMapped] property must not be a CRUD column");
            Assert.IsFalse(query.SqlColumns.Any(c => c.ModelPropertyName == nameof(CrudAnnotatedEmployee.RecordState)),
                "A [DbNotMapped] property must not be a query column either");
        }

        [TestMethod]
        public void Fluent_configuration_produces_expected_column_kinds()
        {
            using var dbc = new OrmDbContext();
            // touch the model so OnModelCreating runs
            dbc.CreateQuery<CrudFluentEmployee>();

            var crud = dbc.GetEntityCrudMetadata<CrudFluentEmployee>();

            Assert.AreEqual(ColumnKind.Identity, Column(crud, nameof(CrudFluentEmployee.EmployeeId)).Kind);
            Assert.AreEqual(ColumnKind.ReadOnly, Column(crud, nameof(CrudFluentEmployee.CreatedDate)).Kind);
            Assert.AreEqual(ColumnKind.RowVersion, Column(crud, nameof(CrudFluentEmployee.RowVer)).Kind);
            Assert.AreEqual(ColumnKind.Regular, Column(crud, nameof(CrudFluentEmployee.Name)).Kind);

            var name = Column(crud, nameof(CrudFluentEmployee.Name));
            Assert.IsTrue(name.IsRequired);
            Assert.AreEqual("Employee Name", name.RequiredFieldTitle);
        }

        [TestMethod]
        public void Fluent_configuration_overrides_the_annotation()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<CrudFluentEmployee>();

            var crud = dbc.GetEntityCrudMetadata<CrudFluentEmployee>();

            // LegacyId carries [DbIdentityColumn], but OnModelCreating sets it back to Regular
            Assert.AreEqual(ColumnKind.Regular, Column(crud, nameof(CrudFluentEmployee.LegacyId)).Kind);
        }

        [TestMethod]
        public void Ignore_removes_the_property_from_both_sides_of_the_mapping()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<CrudFluentEmployee>();

            var crud = dbc.GetEntityCrudMetadata<CrudFluentEmployee>();
            var query = dbc.GetEntityMetadata<CrudFluentEmployee>();

            Assert.IsFalse(crud.TryGetColumn(nameof(CrudFluentEmployee.ScratchNote), out _));
            Assert.IsFalse(query.SqlColumns.Any(c => c.ModelPropertyName == nameof(CrudFluentEmployee.ScratchNote)));
        }

        [TestMethod]
        public void Query_and_crud_metadata_describe_the_same_columns_in_the_same_order()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<CrudFluentEmployee>();
            dbc.CreateQuery<CrudAnnotatedEmployee>();

            AssertColumnsAgree<CrudFluentEmployee>(dbc);
            AssertColumnsAgree<CrudAnnotatedEmployee>(dbc);
        }

        private static void AssertColumnsAgree<T>(OrmDbContext dbc)
        {
            var query = dbc.GetEntityMetadata<T>();
            var crud = dbc.GetEntityCrudMetadata<T>();

            CollectionAssert.AreEqual(
                query.SqlColumns.Select(x => x.ModelPropertyName).ToArray(),
                crud.Columns.Select(x => x.ModelPropertyName).ToArray(),
                $"The two sides of the {typeof(T).Name} mapping must describe the same columns");
        }

        [TestMethod]
        public void Column_kinds_do_not_leak_into_the_query_metadata()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<CrudFluentEmployee>();

            var query = dbc.GetEntityMetadata<CrudFluentEmployee>();

            // The query side is unchanged by any of the CRUD configuration above: the table, the
            // column name override and the primary key are all still exactly what was asked for.
            Assert.AreEqual("CRUD_FLUENT_EMPLOYEE", query.Table.TableName);
            Assert.AreEqual("EMP_NAME", query.SqlColumns.Single(c => c.ModelPropertyName == nameof(CrudFluentEmployee.Name)).DatabaseColumnName);

            var id = query.SqlColumns.Single(c => c.ModelPropertyName == nameof(CrudFluentEmployee.EmployeeId));
            Assert.IsTrue(id.IsPrimaryKey);
        }

        [TestMethod]
        public void Annotated_column_names_and_keys_are_unaffected()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<CrudAnnotatedEmployee>();

            var query = dbc.GetEntityMetadata<CrudAnnotatedEmployee>();

            Assert.AreEqual("CRUD_ANNOTATED_EMPLOYEE", query.Table.TableName);
            Assert.AreEqual("dbo", query.Table.Schema);

            var id = query.SqlColumns.Single(c => c.ModelPropertyName == nameof(CrudAnnotatedEmployee.EmployeeId));
            Assert.AreEqual("EMP_ID", id.DatabaseColumnName);
            Assert.IsTrue(id.IsPrimaryKey);
        }
    }
}
