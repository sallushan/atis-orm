using System;

namespace Atis.Orm.Annotations
{
    /// <summary>
    ///     <para>
    ///         Marks a property as required. Entity level Insert and Update validate the property
    ///         before touching the database and fail when the value is <c>null</c> — or, for a
    ///         <c>string</c>, when it is <c>null</c>, empty or white space.
    ///     </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class RequiredFieldValidationAttribute : Attribute
    {
        /// <summary>
        ///     Constructs the attribute, taking the field title from the property name.
        /// </summary>
        public RequiredFieldValidationAttribute()
        {
        }

        /// <summary>
        ///     Constructs the attribute with an explicit field title.
        /// </summary>
        /// <param name="fieldTitle">
        ///     The name to use for this field when reporting a validation failure. When <c>null</c> or
        ///     white space, the property name is used instead.
        /// </param>
        public RequiredFieldValidationAttribute(string fieldTitle)
        {
            this.FieldTitle = fieldTitle;
        }

        /// <summary>
        ///     The name to use for this field when reporting a validation failure, or <c>null</c> to
        ///     use the property name.
        /// </summary>
        public string FieldTitle { get; }
    }
}
