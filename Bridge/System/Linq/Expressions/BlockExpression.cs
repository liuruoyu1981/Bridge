using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Bridge;

namespace System.Linq.Expressions
{
    [External]
    [Name("Object")]
    [Cast("{this}.ntype === 47")]
	public sealed class BlockExpression : Expression
    {
        [FieldProperty]
		public extern ReadOnlyCollection<Expression> Expressions { get; private set; }

	    public extern ReadOnlyCollection<ParameterExpression> Variables
	    {
	        [Template("({this}.variables || Bridge.toList([]))")] get;
            private set;
	    }
        
        public extern Expression Result
        {
            [Template("{this}.expressions.get({this}.expressions.getCount() - 1)")]
            get;
            private set;
        }

		internal extern BlockExpression();
	}
}