using System.ComponentModel;
using System.Runtime.CompilerServices;
using Bridge;

namespace System.Reflection
{
	[External]
    [Name("Object")]
    public class ParameterInfo
    {
	    [Name("sname")]
	    [FieldProperty]
	    public extern string ScriptName
	    {
	        get;
	    }

        [FieldProperty]
        public extern string Name
        {
            get;
        }

        [FieldProperty]
        public extern string DefaultValue
        {
            get;
        }

        public extern bool HasDefaultValue
        {
            [Template("({this}.isOptional || false)")]
            get;
        }

        public extern bool IsOptional
        {
            [Template("({this}.isOptional || false)")]
            get;
        }

        public extern bool IsOut
        {
            [Template("({this}.isOut || false)")]
            get;
        }

        public extern bool IsRef
        {
            [Template("({this}.isRef || false)")]
            get;
        }

        public extern bool IsParams
        {
            [Template("({this}.isParams || false)")]
            get;
        }

        [FieldProperty]
        public extern Type ParameterType
        {
            get;
        }

        [FieldProperty]
        public extern int Position
        {
            get;
        }

        /// <summary>
		/// Returns an array of all custom attributes applied to this member.
		/// </summary>
		/// <param name="inherit">Ignored for members. Base members will never be considered.</param>
		/// <returns>An array that contains all the custom attributes applied to this member, or an array with zero elements if no attributes are defined. </returns>
		[Template("({this}.attr || [])")]
        public extern object[] GetCustomAttributes(bool inherit);

        /// <summary>
        /// Returns an array of custom attributes applied to this member and identified by <see cref="T:System.Type"/>.
        /// </summary>
        /// <param name="attributeType">The type of attribute to search for. Only attributes that are assignable to this type are returned. </param>
        /// <param name="inherit">Ignored for members. Base members will never be considered.</param>
        /// <returns>An array that contains all the custom attributes applied to this member, or an array with zero elements if no attributes are defined.</returns>
        [Template("({this}.attr || []).filter(function(a) { return Bridge.is(a, {attributeType}); })")]
        public extern object[] GetCustomAttributes(Type attributeType, bool inherit);

        /// <summary>
        /// Returns an array of all custom attributes applied to this member.
        /// </summary>
        /// <returns>An array that contains all the custom attributes applied to this member, or an array with zero elements if no attributes are defined. </returns>
        [Template("({this}.attr || [])")]
        public extern object[] GetCustomAttributes();

        /// <summary>
        /// Returns an array of custom attributes applied to this member and identified by <see cref="T:System.Type"/>.
        /// </summary>
        /// <param name="attributeType">The type of attribute to search for. Only attributes that are assignable to this type are returned. </param>
        /// <returns>An array that contains all the custom attributes applied to this member, or an array with zero elements if no attributes are defined.</returns>
        [Template("({this}.attr || []).filter(function(a) { return Bridge.is(a, {attributeType}); })")]
        public extern object[] GetCustomAttributes(Type attributeType);
    }
}