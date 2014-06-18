﻿using System;
using ICSharpCode.NRefactory.CSharp;
using System.Linq;
using ICSharpCode.NRefactory.Semantics;
using System.Collections.Generic;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.CSharp.Resolver;

namespace Bridge.NET
{
    public class ArgumentsInfo
    {
        public Emitter Emitter
        {
            get;
            private set;
        }

        public Expression Expression
        {
            get;
            private set;
        }

        public InvocationResolveResult ResolveResult
        {
            get;
            private set;
        }

        public OperatorResolveResult OperatorResolveResult
        {
            get;
            private set;
        }

        public Expression[] ArgumentsExpressions
        {
            get;
            private set;
        }

        public string[] ArgumentsNames
        {
            get;
            private set;
        }

        public Expression ParamsExpression
        {
            get;
            private set;
        }

        public NamedParamExpression[] NamedExpressions 
        { 
            get; 
            private set; 
        }

        public TypeParamExpression[] TypeArguments 
        { 
            get; 
            private set; 
        }

        public object ThisArgument
        {
            get;
            set;
        }

        public string ThisName
        {
            get;
            set;
        }

        public ArgumentsInfo(Emitter emitter, InvocationExpression invocationExpression)
        {
            this.Emitter = emitter;
            this.Expression = invocationExpression;            

            var arguments = invocationExpression.Arguments.ToList();
            this.ResolveResult = emitter.Resolver.ResolveNode(invocationExpression, emitter) as InvocationResolveResult;

            this.BuildArgumentsList(arguments);
            this.BuildTypedArguments(invocationExpression.Target);
        }

        public ArgumentsInfo(Emitter emitter, ObjectCreateExpression objectCreateExpression)
        {
            this.Emitter = emitter;
            this.Expression = objectCreateExpression;

            var arguments = objectCreateExpression.Arguments.ToList();
            this.ResolveResult = emitter.Resolver.ResolveNode(objectCreateExpression, emitter) as InvocationResolveResult;

            this.BuildArgumentsList(arguments);
            this.BuildTypedArguments(objectCreateExpression.Type);
        }

        public ArgumentsInfo(Emitter emitter, BinaryOperatorExpression binaryOperatorExpression, OperatorResolveResult operatorResolveResult)
        {
            this.Emitter = emitter;
            this.Expression = binaryOperatorExpression;
            this.OperatorResolveResult = operatorResolveResult;

            this.ArgumentsExpressions = new Expression[] { binaryOperatorExpression.Left, binaryOperatorExpression.Right };
            this.ArgumentsNames = new string[] { "left", "right" };
            this.CreateNamedExpressions(this.ArgumentsNames, this.ArgumentsExpressions);
        }

        private void BuildTypedArguments(AstType type)
        {
            var simpleType = type as SimpleType;

            if (simpleType != null)
            {
                AstNodeCollection<AstType> typedArguments = simpleType.TypeArguments;
                var typeParams = this.ResolveResult.Member.DeclaringTypeDefinition.TypeParameters;
                this.TypeArguments = new TypeParamExpression[typedArguments.Count];
                var list = typedArguments.ToList();
                for (int i = 0; i < list.Count; i++)
                {
                    this.TypeArguments[i] = new TypeParamExpression(typeParams[i].Name, list[i], null);
                }
            }
        }        
        
        private void BuildTypedArguments(Expression expression)
        {
            AstNodeCollection<AstType> typedArguments = null;

            var identifierExpression = expression as IdentifierExpression;            
            if (identifierExpression != null)
            {
                typedArguments = identifierExpression.TypeArguments;
            }
            else
            {
                var memberRefExpression = expression as MemberReferenceExpression;
                if (memberRefExpression != null)
                {
                    typedArguments = memberRefExpression.TypeArguments;
                }
            }

            var method = this.ResolveResult.Member as IMethod;

            if (method != null)
            {                
                this.TypeArguments = new TypeParamExpression[method.TypeParameters.Count];

                if (typedArguments.Count == method.TypeParameters.Count) 
                {
                    var list = typedArguments.ToList();
                    for (int i = 0; i < list.Count; i++)
			        {
                        this.TypeArguments[i] = new TypeParamExpression(method.TypeParameters[i].Name, list[i], null);
			        }      
                }
                else
                {
                    for (int i = 0; i < method.TypeArguments.Count; i++)
                    {
                        this.TypeArguments[i] = new TypeParamExpression(method.TypeParameters[i].Name, null, method.TypeArguments[i]);
                    }
                }
            }
        }

        private void BuildArgumentsList(IList<Expression> arguments)
        {
            Expression paramsArg = null;
            string paramArgName = null;
            var resolveResult = this.ResolveResult;

            if (resolveResult != null)
            {
                var parameters = resolveResult.Member.Parameters;
                var resolvedMethod = resolveResult.Member as IMethod;
                var invocationResult = resolveResult as CSharpInvocationResolveResult;
                int shift = 0;

                if (resolvedMethod != null && invocationResult != null &&
                    resolvedMethod.IsExtensionMethod && invocationResult.IsExtensionMethodInvocation)
                {
                    shift = 1;
                    this.ThisName = resolvedMethod.Parameters[0].Name;
                }

                Expression[] result = new Expression[parameters.Count - shift];
                string[] names = new string[result.Length];

                int i = 0;
                foreach (var arg in arguments)
                {
                    if (arg is NamedArgumentExpression)
                    {
                        NamedArgumentExpression namedArg = (NamedArgumentExpression)arg;
                        var namedParam = parameters.First(p => p.Name == namedArg.Name);
                        var index = parameters.IndexOf(namedParam);

                        result[index] = namedArg.Expression;
                        names[index] = namedArg.Name;
                    }
                    else
                    {
                        if (paramsArg == null && parameters[i + shift].IsParams && !this.Emitter.Validator.IsIgnoreType(resolvedMethod.DeclaringTypeDefinition))
                        {
                            paramsArg = arg;
                            paramArgName = parameters[i + shift].Name;
                        }

                        if (i >= result.Length)
                        {
                            var list = result.ToList();
                            list.AddRange(new Expression[arguments.Count - i]);

                            var strList = names.ToList();
                            strList.AddRange(new string[arguments.Count - i]);

                            result = list.ToArray();
                            names = strList.ToArray();
                        }

                        result[i] = arg;
                        names[i] = i < parameters.Count ? parameters[i + shift].Name : paramArgName;
                    }

                    i++;
                }

                for (i = 0; i < result.Length; i++)
                {
                    if (result[i] == null)
                    {
                        result[i] = new PrimitiveExpression(parameters[i + shift].ConstantValue);
                        names[i] = parameters[i + shift].Name;
                    }
                }

                this.ArgumentsExpressions = result;
                this.ArgumentsNames = names;
                this.ParamsExpression = paramsArg;
                this.NamedExpressions = this.CreateNamedExpressions(names, result);
            }
            else
            {
                this.ArgumentsExpressions = arguments.ToArray();
            }            
        }

        private NamedParamExpression[] CreateNamedExpressions(string[] names, Expression[] expressions)
        {
            var result = new NamedParamExpression[names.Length];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new NamedParamExpression(names[i], expressions[i]);
            }

            return result;
        }

        public string GetThisValue()
        {
            if (this.ThisArgument != null)
            {
                if (this.ThisArgument is string)
                {
                    return this.ThisArgument.ToString();
                }

                if (this.ThisArgument is Expression)
                {
                    ((Expression)this.ThisArgument).AcceptVisitor(this.Emitter);
                    return null;
                }
            }

            return "null";
        }
    }

    public class NamedParamExpression
    {
        public NamedParamExpression(string name, Expression expression)
        {
            this.Name = name;
            this.Expression = expression;
        }

        public string Name 
        { 
            get; 
            private set; 
        }

        public Expression Expression 
        { 
            get; 
            private set; 
        }
    }

    public class TypeParamExpression
    {
        public TypeParamExpression(string name, AstType type, IType iType)
        {
            this.Name = name;
            this.AstType = type;
            this.IType = iType;
        }

        public string Name
        {
            get;
            private set;
        }

        public AstType AstType
        {
            get;
            private set;
        }

        public IType IType
        {
            get;
            private set;
        }
    }
}
