﻿using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using System.Collections.Generic;
using System.Text;

namespace Bridge.NET
{
    public class AsyncBlock : AbstractEmitterBlock
    {
        public AsyncBlock(Emitter emitter, MethodDeclaration methodDeclaration)
        {
            this.Emitter = emitter;
            this.MethodDeclaration = methodDeclaration;
        }

        public AsyncBlock(Emitter emitter, LambdaExpression lambdaExpression) 
        {
            this.Emitter = emitter;
            this.LambdaExpression = lambdaExpression;
        }

        public AsyncBlock(Emitter emitter, AnonymousMethodExpression anonymousMethodExpression)
        {
            this.Emitter = emitter;
            this.AnonymousMethodExpression = anonymousMethodExpression;
        }

        public AstNode Node
        {
            get
            {
                if (this.MethodDeclaration != null)
                {
                    return this.MethodDeclaration;
                }

                if (this.AnonymousMethodExpression != null)
                {
                    return this.AnonymousMethodExpression;
                }

                if (this.LambdaExpression != null)
                {
                    return this.LambdaExpression;
                }

                return null;
            }
        }

        public MethodDeclaration MethodDeclaration 
        { 
            get; 
            set; 
        }

        public LambdaExpression LambdaExpression
        {
            get;
            set;
        }

        public AnonymousMethodExpression AnonymousMethodExpression
        {
            get;
            set;
        }

        public AstNode Body
        {
            get
            {
                if (this.MethodDeclaration != null)
                {
                    return this.MethodDeclaration.Body;
                }

                if (this.LambdaExpression != null) 
                {
                    return this.LambdaExpression.Body;
                }

                if (this.AnonymousMethodExpression != null)
                {
                    return this.AnonymousMethodExpression.Body;
                }

                return null;
            }
        }

        protected bool PreviousIsAync
        {
            get;
            set;
        }

        protected List<string> PreviousAsyncVariables
        {
            get;
            set;
        }

        protected AsyncBlock PreviousAsyncBlock
        {
            get;
            set;
        }

        public Expression[] AwaitExpressions
        {
            get;
            set;
        }

        public IType ReturnType
        {
            get;
            set;
        }

        public bool IsTaskReturn
        {
            get;
            set;
        }

        public int Step 
        { 
            get; 
            set; 
        }

        public List<AsyncStep> Steps
        {
            get;
            set;
        }

        public List<AsyncStep> EmittedAsyncSteps
        {
            get;
            set;
        }

        public bool ReplaceAwaiterByVar
        {
            get;
            set;
        }

        public List<AsyncTryInfo> TryInfos
        {
            get;
            set;
        }

        protected void InitAsyncBlock()
        {
            this.PreviousIsAync = this.Emitter.IsAsync;
            this.Emitter.IsAsync = true;

            this.PreviousAsyncVariables = this.Emitter.AsyncVariables;
            this.Emitter.AsyncVariables = new List<string>();

            this.PreviousAsyncBlock = this.Emitter.AsyncBlock;
            this.Emitter.AsyncBlock = this;

            this.ReplaceAwaiterByVar = this.Emitter.ReplaceAwaiterByVar;
            this.Emitter.ReplaceAwaiterByVar = false;

            this.DetectReturnType();
            this.FindAwaitNodes();

            this.Steps = new List<AsyncStep>();
            this.TryInfos = new List<AsyncTryInfo>();
        }

        protected void DetectReturnType()
        {
            AstNode node = this.MethodDeclaration != null ? this.MethodDeclaration.ReturnType : null;

            if (node == null)
            {
                node = this.AnonymousMethodExpression;
            }

            if (node == null)
            {
                node = this.LambdaExpression;
            }

            var resolveResult = this.Emitter.Resolver.ResolveNode(node, this.Emitter);

            if (resolveResult is LambdaResolveResult)
            {
                this.ReturnType = ((LambdaResolveResult)resolveResult).ReturnType;
            }
            else if (resolveResult is TypeResolveResult)
            {
                this.ReturnType = ((TypeResolveResult)resolveResult).Type;
            }

            this.IsTaskReturn = this.ReturnType != null && this.ReturnType.Name == "Task" && this.ReturnType.FullName.StartsWith("System.Threading.Tasks.Task");
        }

        protected void FindAwaitNodes()
        {
            this.AwaitExpressions = this.GetAwaiters(this.Body);

            for (int i = 0; i < this.AwaitExpressions.Length; i++)
            {
                this.Emitter.AsyncVariables.Add("$task" + (i+1));
                if (this.IsTaskResult(this.AwaitExpressions[i]))
                {
                    this.Emitter.AsyncVariables.Add("$taskResult" + (i + 1));
                }
            }            
        }

        protected bool IsTaskResult(Expression expression)
        {
            var resolveResult = this.Emitter.Resolver.ResolveNode(expression, this.Emitter);

            if (resolveResult is InvocationResolveResult)
            {
                var type = ((InvocationResolveResult)resolveResult).Member.ReturnType;

                if (type.Name == "Task" && type.Namespace == "System.Threading.Tasks" && type.TypeParameterCount > 0)
                {
                    return true;
                }
            }

            return false;
        }

        protected void FinishAsyncBlock()
        {
            this.Emitter.IsAsync = this.PreviousIsAync;
            this.Emitter.AsyncVariables = this.PreviousAsyncVariables;
            this.Emitter.AsyncBlock = this.PreviousAsyncBlock;
            this.Emitter.ReplaceAwaiterByVar = this.ReplaceAwaiterByVar;
        }

        public override void Emit()
        {
            this.InitAsyncBlock();
            this.EmitAsyncBlock();
            this.FinishAsyncBlock();
        }       

        protected void EmitAsyncBlock()
        {
            var pos = 0;
            this.BeginBlock();
            this.WriteVar(true);
            this.Write("$step = 0,");
            pos = this.Emitter.Output.Length;
            this.WriteNewLine();

            this.Indent();
            this.Write("$asyncBody = ");
            this.WriteFunction();
            this.Write("() ");

            this.EmitAsyncBody();

            string temp = this.Emitter.Output.ToString(pos, this.Emitter.Output.Length - pos);
            this.Emitter.Output.Length = pos;

            foreach (var localVar in this.Emitter.AsyncVariables)
            {
                this.WriteNewLine();
                this.Write(localVar);
                this.WriteComma();
            }

            this.Emitter.Output.Append(temp);

            this.WriteSemiColon();
            this.WriteNewLine();
            this.WriteNewLine();
            this.Outdent();
            this.Write("$asyncBody();");            

            if (this.IsTaskReturn)
            {
                this.WriteNewLine();
                this.Write("return $returnTask;");
            }

            this.WriteNewLine();

            this.EndBlock();
        }

        protected void EmitAsyncBody()
        {            
            var isLambda = this.LambdaExpression != null || this.AnonymousMethodExpression != null;

            this.BeginBlock();

            var asyncTryVisitor = new AsyncTryVisitor();
            this.Node.AcceptVisitor(asyncTryVisitor);
            var needTry = asyncTryVisitor.Found || this.IsTaskReturn;

            if (needTry)
            {
                if (this.IsTaskReturn)
                {
                    this.Emitter.AsyncVariables.Add("$returnTask = new Bridge.Task()");
                }
                this.Write("try");
                this.WriteSpace();
                this.BeginBlock();
            }
            
            this.Write("for (;;) ");
            this.BeginBlock();
            this.Write("switch ($step) ");

            this.BeginBlock();

            this.Step = 0;
            var writer = this.SaveWriter();
            this.AddAsyncStep();
            
            this.Body.AcceptVisitor(this.Emitter);

            this.RestoreWriter(writer);

            this.InjectSteps();

            this.WriteNewLine();
            this.EndBlock();

            this.WriteNewLine();
            this.EndBlock();
            if (needTry)
            {
                this.WriteNewLine();
                this.EndBlock();
                this.Write(" catch($e1) ");
                this.BeginBlock();
                this.InjectCatchHandlers();                

                this.WriteNewLine();
                this.EndBlock();
            }            

            this.WriteNewLine();
            this.EndBlock();
        }

        protected void InjectCatchHandlers()
        {
            var infos = this.TryInfos;

            foreach (var info in infos)
            {
                this.WriteIf();
                this.WriteOpenParentheses(true);
                this.Write(string.Format("$step >= {0} && $step <= {1}", info.StartStep, info.EndStep));
                this.WriteCloseParentheses(true);
                this.BeginBlock();

                if (!string.IsNullOrWhiteSpace(info.VarName))
                {
                    this.Write(info.VarName + " = $e1;");                    
                }
                else
                {
                    this.Write("$e = $e1;");                    
                }

                this.WriteNewLine();
                this.Write("$step = " + info.JumpToStep + ";");
                this.WriteNewLine();
                this.Write("setTimeout($asyncBody, 0);");
                this.WriteNewLine();
                this.Write("return;");

                this.WriteNewLine();
                this.EndBlock();
                this.WriteNewLine();
            }

            if (this.IsTaskReturn)
            {
                this.Write("$returnTask.setError($e1);");
            }
            else
            {
                this.Write("throw $e;");
            }
        }

        protected void InjectSteps()
        {
            for (int i = 0; i < this.Steps.Count; i++)
            {
                var step = this.Steps[i];

                if (i != 0)
                {
                    this.WriteNewLine();
                }
                this.Write("case " + i + ": ");
                this.BeginBlock();                
                
                bool addNewLine = false;

                if (step.FromTaskNumber > -1)
                {
                    var expression = this.AwaitExpressions[step.FromTaskNumber - 1];

                    if (this.IsTaskResult(expression))
                    {
                        this.Write(string.Format("$taskResult{0} = $task{0}.getResult();", step.FromTaskNumber));
                    }
                    else
                    {
                        this.Write(string.Format("$task{0}.getResult();", step.FromTaskNumber));
                    }

                    addNewLine = true;
                }

                var output = step.Output.ToString();

                if (!string.IsNullOrWhiteSpace(output))
                {
                    if (addNewLine)
                    {
                        this.WriteNewLine();
                    }

                    this.Write(this.WriteIndentToString(output.TrimEnd()));                    
                }

                if (!this.IsOnlyWhitespaceOnPenultimateLine(false))
                {
                    addNewLine = true;
                }

                if (step.JumpToStep > -1 && !AbstractEmitterBlock.IsJumpStatementLast(output))
                {
                    if (addNewLine)
                    {
                        this.WriteNewLine();
                    }

                    this.Write("$step = " + step.JumpToStep + ";");
                    this.WriteNewLine();
                    this.Write("continue;");
                }
                else if (i == (this.Steps.Count - 1) && !AbstractEmitterBlock.IsReturnLast(output))
                {
                    if (addNewLine)
                    {
                        this.WriteNewLine();
                    }

                    if (this.IsTaskReturn)
                    {
                        this.Write("$returnTask.setResult(null);");
                        this.WriteNewLine();
                    }
                    this.Write("return;");
                }

                this.WriteNewLine();
                this.EndBlock();
            }

            this.WriteNewLine();
            this.Write("default: ");
            this.BeginBlock();
            if (this.IsTaskReturn)
            {
                this.Write("$returnTask.setResult(null);");
                this.WriteNewLine();
            }
            this.Write("return;");
            this.WriteNewLine();
            this.EndBlock();
        }

        public AsyncStep AddAsyncStep(int fromTaskNumber = -1)
        {
            var step = this.Step++;
            var asyncStep = new AsyncStep(this.Emitter, step, fromTaskNumber);
            this.Steps.Add(asyncStep);

            return asyncStep;
        }

        public bool IsParentForAsync(AstNode child)
        {
            if (child is IfElseStatement)
            {
                return false;
            }
            else
            {
                foreach (var awaiter in this.AwaitExpressions)
                {
                    if (child.IsInside(awaiter.StartLocation))
                    {
                        return true;
                    }
                }
            }            

            return false;
        }        
    }

    public class AsyncStep
    {
        public AsyncStep(Emitter emitter, int step, int fromTaskNumber)
        {
            this.Step = step;
            this.Emitter = emitter;
            this.JumpToStep = -1;
            this.FromTaskNumber = -1;


            if (this.Emitter.LastSavedWriter != null)
            {
                this.Emitter.LastSavedWriter.Comma = this.Emitter.Comma;
                this.Emitter.LastSavedWriter.IsNewLine = this.Emitter.IsNewLine;
                this.Emitter.LastSavedWriter.Level = this.Emitter.Level;
                this.Emitter.LastSavedWriter = null;
            }

            this.Output = new StringBuilder();
            this.Emitter.Output = this.Output;
            this.Emitter.IsNewLine = false;
            this.Emitter.Level = 0;
            this.Emitter.Comma = false;

            this.FromTaskNumber = fromTaskNumber;
        }

        public int FromTaskNumber
        {
            get;
            set;
        }

        public int JumpToStep
        {
            get;
            set;
        }

        public int Step
        {
            get;
            set;
        }

        protected Emitter Emitter
        {
            get;
            set;
        }

        public StringBuilder Output
        {
            get;
            set;
        }
    }
}