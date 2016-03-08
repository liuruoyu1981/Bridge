using System;
using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using Object.Net.Utilities;

namespace Bridge.Translator
{
    public class InlineArgumentsBlock : AbstractEmitterBlock
    {
        public InlineArgumentsBlock(IEmitter emitter, ArgumentsInfo argsInfo, string inline, IMethod method = null, ResolveResult targetResolveResult = null)
            : base(emitter, argsInfo.Expression)
        {
            this.Emitter = emitter;
            this.ArgumentsInfo = argsInfo;
            this.InlineCode = inline;

            argsInfo.AddExtensionParam();
            this.Method = method;
            this.TargetResolveResult = targetResolveResult;
        }

        public IMethod Method
        {
            get; set;
        }

        public ResolveResult TargetResolveResult { get; set; }

        public ArgumentsInfo ArgumentsInfo
        {
            get;
            set;
        }

        public string InlineCode
        {
            get;
            set;
        }

        protected override void DoEmit()
        {
            this.EmitInlineExpressionList(this.ArgumentsInfo, this.InlineCode);
        }

        private static Regex _formatArg = new Regex(@"\{(\*?)(\w+)(\:(\w+))?\}");
        private static Regex _inlineMethod = new Regex(@"([$\w\.]+)\(\s*(.*)\)");

        protected virtual IList<Expression> GetExpressionsByKey(IEnumerable<NamedParamExpression> expressions, string key)
        {
            if (expressions == null)
            {
                return new List<Expression>();
            }

            if (Regex.IsMatch(key, "^\\d+$"))
            {
                var list = new List<Expression>();
                list.Add(expressions.Skip(int.Parse(key)).First().Expression);

                return list;
            }

            return expressions.Where(e => e.Name == key).Select(e => e.Expression).ToList();
        }

        protected virtual AstType GetAstTypeByKey(IEnumerable<TypeParamExpression> types, string key)
        {
            return types.Where(e => e.Name == key && e.AstType != null).Select(e => e.AstType).FirstOrDefault();
        }

        protected virtual IType GetITypeByKey(IEnumerable<TypeParamExpression> types, string key)
        {
            return types.Where(e => e.Name == key && e.IType != null).Select(e => e.IType).FirstOrDefault();
        }

        public static string ReplaceInlineArgs(AbstractEmitterBlock block, string inline, Expression[] args)
        {
            var emitter = block.Emitter;
            inline = _formatArg.Replace(inline, delegate(Match m)
            {
                int count = emitter.Writers.Count;
                string key = m.Groups[2].Value;
                string modifier = m.Groups[1].Success ? m.Groups[4].Value : null;

                StringBuilder oldSb = emitter.Output;
                emitter.Output = new StringBuilder();

                Expression expr = null;

                if (Regex.IsMatch(key, "^\\d+$"))
                {
                    expr = args.Skip(int.Parse(key)).FirstOrDefault();
                }
                else
                {
                    expr = args.FirstOrDefault(e => e.ToString() == key);
                }

                string s = "";
                if (expr != null)
                {
                    var writer = block.SaveWriter();
                    block.NewWriter();
                    expr.AcceptVisitor(emitter);
                    s = emitter.Output.ToString();
                    block.RestoreWriter(writer);

                    if (modifier == "raw")
                    {
                        s = s.Trim('"');
                    }
                }

                block.Write(block.WriteIndentToString(s));

                if (emitter.Writers.Count != count)
                {
                    block.PopWriter();
                }

                string replacement = emitter.Output.ToString();
                emitter.Output = oldSb;

                return replacement;
            });

            return inline;
        }

        protected virtual void WriteParamName(string name)
        {
            if (Helpers.IsReservedWord(name))
            {
                name = Helpers.ChangeReservedWord(name);
            }

            this.Write(name);
        }

        protected virtual void EmitInlineExpressionList(ArgumentsInfo argsInfo, string inline, bool asRef = false, bool isNull = false)
        {
            IEnumerable<NamedParamExpression> expressions = argsInfo.NamedExpressions;
            IEnumerable<TypeParamExpression> typeParams = argsInfo.TypeArguments;

            this.Write("");

            if (asRef)
            {
                this.Write("function (");
                this.EmitMethodParameters(this.Method, this.Method.Parameters, this.Method.TypeParameters, isNull);
                this.Write(") { return ");
            }

            bool needExpand = false;

            string paramsName = null;
            IType paramsType = null;
            int paramsIndex = 0;
            if (argsInfo.ResolveResult != null)
            {
                var paramsParam = argsInfo.ResolveResult.Member.Parameters.FirstOrDefault(p => p.IsParams);
                if (paramsParam != null)
                {
                    paramsIndex = argsInfo.ResolveResult.Member.Parameters.IndexOf(paramsParam);
                    paramsName = paramsParam.Name;
                    paramsType = paramsParam.Type;
                }
            }

            if (paramsName != null)
            {
                var matches = _formatArg.Matches(inline);
                bool ignoreArray = false;
                foreach (Match m in matches)
                {
                    if (m.Groups[2].Value == paramsName)
                    {
                        bool isRaw = m.Groups[1].Success && m.Groups[1].Value == "*";
                        ignoreArray = isRaw || argsInfo.ParamsExpression == null;
                        string modifier = m.Groups[1].Success ? m.Groups[4].Value : null;

                        if (modifier == "array")
                        {
                            ignoreArray = false;
                        }

                        break;
                    }
                }

                if (argsInfo.ResolveResult is CSharpInvocationResolveResult)
                {
                    needExpand = !((CSharpInvocationResolveResult) argsInfo.ResolveResult).IsExpandedForm;
                }

                if (needExpand && ignoreArray && !asRef)
                {
                    IList<Expression> exprs = this.GetExpressionsByKey(expressions, paramsName);

                    if (exprs.Count == 1 && exprs[0] != null && exprs[0].Parent != null)
                    {
                        var exprrr = this.Emitter.Resolver.ResolveNode(exprs[0], this.Emitter);
                        if (exprrr.Type.Kind == TypeKind.Array)
                        {
                            var match = _inlineMethod.Match(inline);

                            if (match.Success)
                            {
                                string target = null;
                                var methodName = match.Groups[1].Value;

                                if (methodName.Contains("."))
                                {
                                    target = methodName.LeftOfRightmostOf('.');
                                }

                                string args = match.Groups[2].Value;

                                StringBuilder sb = new StringBuilder();
                                sb.Append(methodName);
                                sb.Append(".apply(");
                                sb.Append(target ?? "null");

                                if (args.Contains(","))
                                {
                                    sb.Append(", [");
                                    sb.Append(args.LeftOfRightmostOf(',').Trim());
                                    sb.Append("].concat(");
                                    sb.Append(args.RightOfRightmostOf(',').Trim());
                                    sb.Append(")");
                                }
                                else
                                {
                                    sb.Append(",");
                                    sb.Append(args);
                                }
                                
                                sb.Append(")");

                                inline = inline.Remove(match.Index, match.Length);
                                inline = inline.Insert(match.Index, sb.ToString());
                            }
                        }
                    }
                }
            }

            inline = _formatArg.Replace(inline, delegate(Match m)
            {
                int count = this.Emitter.Writers.Count;
                string key = m.Groups[2].Value;
                bool isRaw = m.Groups[1].Success && m.Groups[1].Value == "*";
                bool ignoreArray = isRaw || argsInfo.ParamsExpression == null;
                string modifier = m.Groups[1].Success ? m.Groups[4].Value : null;
                

                if (modifier == "array")
                {
                    ignoreArray = false;
                }

                StringBuilder oldSb = this.Emitter.Output;
                this.Emitter.Output = new StringBuilder();

                if (asRef)
                {
                    if (Regex.IsMatch(key, "^\\d+$"))
                    {
                        var index = int.Parse(key);
                        key = this.Method.Parameters[index].Name;
                    }

                    if (key == "this")
                    {
                        if (isNull)
                        {
                            this.Write("$t");
                        }
                        else if (this.Method.IsExtensionMethod && this.TargetResolveResult is TypeResolveResult)
                        {
                            this.WriteParamName(this.Method.Parameters.First().Name);
                        }
                        else
                        {
                            ((MemberReferenceExpression)argsInfo.Expression).Target.AcceptVisitor(this.Emitter);    
                        }
                    }
                    else if (this.Method.IsExtensionMethod && key == this.Method.Parameters.First().Name)
                    {
                        if (this.TargetResolveResult is TypeResolveResult)
                        {
                            this.WriteParamName(key);
                        }
                        else
                        {
                            ((MemberReferenceExpression)argsInfo.Expression).Target.AcceptVisitor(this.Emitter);
                        }
                    }
                    else if (paramsName == key && !ignoreArray)
                    {
                        this.Write("Array.prototype.slice.call(arguments, " + paramsIndex + ")");
                    }
                    else
                    {
                        this.WriteParamName(key);
                    }
                }
                else if (key == "this" || key == argsInfo.ThisName || (key == "0" && argsInfo.IsExtensionMethod))
                {
                    if (modifier == "type")
                    {
                        AstNode node = null;
                        if (argsInfo.ThisArgument is AstNode)
                        {
                            node = (AstNode) argsInfo.ThisArgument;
                        }
                        else
                        {
                            node = argsInfo.Expression;
                        }

                        if (node != null)
                        {
                            var rr = this.Emitter.Resolver.ResolveNode(node, this.Emitter);
                            var type = rr.Type;
                            if (rr is MemberResolveResult)
                            {
                                type = ((MemberResolveResult) rr).TargetResult.Type;
                            }

                            this.Write(BridgeTypes.ToJsName(type, this.Emitter));
                        }
                    }
                    else
                    {
                        string thisValue = argsInfo.GetThisValue();

                        if (thisValue != null)
                        {
                            this.Write(thisValue);
                        }    
                    }
                }
                else
                {
                    IList<Expression> exprs = this.GetExpressionsByKey(expressions, key);
                    
                    if (exprs.Count > 0)
                    {
                        if (modifier == "type")
                        {
                            if (paramsName == key && paramsType != null)
                            {
                                this.Write(BridgeTypes.ToJsName(paramsType, this.Emitter));
                            }
                            else
                            {
                                var rr = this.Emitter.Resolver.ResolveNode(exprs[0], this.Emitter);
                                this.Write(BridgeTypes.ToJsName(rr.Type, this.Emitter));
                            }
                        }
                        else if (exprs.Count > 1 || paramsName == key)
                        {
                            if (needExpand)
                            {
                                ignoreArray = true;
                            }

                            if (!ignoreArray)
                            {
                                this.Write("[");
                            }

                            if (exprs.Count == 1 && exprs[0] == null)
                            {
                                this.Write("null");
                            }
                            else
                            {
                                new ExpressionListBlock(this.Emitter, exprs, null).Emit();
                            }

                            if (!ignoreArray)
                            {
                                this.Write("]");
                            }   
                        }
                        else
                        {
                            string s;
                            if (exprs[0] != null)
                            {
                                var writer = this.SaveWriter();
                                this.NewWriter();
                                exprs[0].AcceptVisitor(this.Emitter);
                                s = this.Emitter.Output.ToString();
                                this.RestoreWriter(writer);

                                if (modifier == "raw")
                                {
                                    s = s.Trim('"');
                                }
                            }
                            else
                            {
                                s = "null";
                            }

                            this.Write(this.WriteIndentToString(s));
                        }
                    }
                    else if (typeParams != null)
                    {
                        var type = this.GetAstTypeByKey(typeParams, key);

                        if (type != null)
                        {
                            type.AcceptVisitor(this.Emitter);
                        }
                        else
                        {
                            var iType = this.GetITypeByKey(typeParams, key);

                            if (iType != null)
                            {
                                new CastBlock(this.Emitter, iType).Emit();
                            }
                        }
                    }
                }

                if (this.Emitter.Writers.Count != count)
                {
                    this.PopWriter();
                }

                string replacement = this.Emitter.Output.ToString();
                this.Emitter.Output = oldSb;

                return replacement;
            });

            this.Write(inline);

            if (asRef)
            {
                this.Write("; }");
            }
        }
        
        protected virtual void EmitMethodParameters(IMethod method, IEnumerable<IParameter> parameters, IEnumerable<ITypeParameter> typeParameters, bool isNull)
        {
            bool needComma = false;

            if (typeParameters != null && typeParameters.Any())
            {
                foreach (var tp in typeParameters)
                {
                    this.Emitter.Validator.CheckIdentifier(tp.Name, this.ArgumentsInfo.Expression);

                    if (needComma)
                    {
                        this.WriteComma();
                    }

                    needComma = true;
                    this.Write(tp.Name);
                }
            }

            if (isNull)
            {
                this.Write("$t");
                needComma = true;
            }
            else if (!(this.TargetResolveResult is TypeResolveResult))
            {
                parameters = parameters.Skip(1);
            }

            foreach (var p in parameters)
            {
                var name = p.Name;

                if (Helpers.IsReservedWord(name))
                {
                    name = Helpers.ChangeReservedWord(name);
                }

                if (this.Emitter.LocalsNamesMap != null && this.Emitter.LocalsNamesMap.ContainsKey(name))
                {
                    name = this.Emitter.LocalsNamesMap[name];
                }

                if (needComma)
                {
                    this.WriteComma();
                }

                needComma = true;
                this.Write(name);
            }
        }

        public virtual void EmitFunctionReference()
        {
            this.EmitInlineExpressionList(this.ArgumentsInfo, this.InlineCode, true);
        }

        public virtual void EmitNullableReference()
        {
            this.EmitInlineExpressionList(this.ArgumentsInfo, this.InlineCode, true, true);
        }
    }
}
