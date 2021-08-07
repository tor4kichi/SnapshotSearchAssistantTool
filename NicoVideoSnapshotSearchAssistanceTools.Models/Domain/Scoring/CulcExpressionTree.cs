using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Models.Domain.Scoring
{
  
    public class InvalidCulcRPNTokenizingException : Exception
    {
        public InvalidCulcRPNTokenizingException(string message) : base(message)
        {
        }
    }

    public class InvalidCulcNodeException : Exception
    {
        public InvalidCulcNodeException(string message) : base(message)
        {
        }

        public InvalidCulcNodeException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }


    public interface IMethodPresenter
    {
        bool IsMethodName(in StringToken stringtoken);
    }

    public interface IMethodNodeFactory
    {
        string MethodName { get; }

        int ArgumentCount { get; }

        MethodExpressionTreeNodeBase Create(ICulcExpressionTreeNode[] argumentNodes);
    }

    public interface IMethodNodeFactoryFactory : IMethodPresenter
    {
        IMethodNodeFactory GetFactory(in StringToken stringToken);
    }



    public class MethodNodeFactoryFactory : IMethodNodeFactoryFactory
    {
        public static MethodNodeFactoryFactory CreateDefault()
        {
            return new MethodNodeFactoryFactory(new IMethodNodeFactory[] 
            {
                MaxMethodExpressionTreeNode.Factory,
                MinMethodExpressionTreeNode.Factory,
                ClampMethodExpressionTreeNode.Factory,
                LimitMethodExpressionTreeNode.Factory,
            });
        }

        private readonly Dictionary<string, IMethodNodeFactory> _factories;

        public  MethodNodeFactoryFactory(IEnumerable<IMethodNodeFactory> factories)
        {
            _factories = factories.ToDictionary(x => x.MethodName);
        }

        public IMethodNodeFactory GetFactory(in StringToken stringToken)
        {
            return _factories.GetValueOrDefault(stringToken.String) ?? throw new InvalidOperationException($"{stringToken.String} method is not presented.");
        }

        public bool IsMethodName(in StringToken stringtoken)
        {
            return _factories.ContainsKey(stringtoken.String);
        }
    }

    public static class CulcExpressionTree
    {

        // RPN = 逆ポーランド記法 Reverse Polish Notation
        // 参考：https://www.mk-mode.com/blog/2020/11/18/cpp-convert-infix-to-rpn-with-stack/

        // 上記ページとの違い
        // ・演算子ノードが来た際の、高優先度演算子ノード取り出しを複数回行う点
        // ・関数名と引数セパレータの実装を追加してる

        public static IEnumerable<IToken> ToRPN(IEnumerable<IToken> tokens, IMethodPresenter methodPresenter)
        {
            Stack<IToken> stack = new();
            static int ComparePriorityToken(in IToken left, in IToken right)
            {
                return TokenToPriority(left) - TokenToPriority(right);
            }

            static int TokenToPriority(in IToken token)
            {
                return token switch
                {
                    PrioritizingToken => 0,
                    StringToken => 6,
                    OperatorToken opToken => opToken.Operator switch
                    {
                        OperatorType.Div => 5,
                        OperatorType.Mul => 4,
                        OperatorType.Plus or OperatorType.Minus => 3,
                        _ => throw new InvalidCulcRPNTokenizingException("Priority not supported OperatorType: " + opToken.Operator.ToString()),
                    },
                    _ => throw new InvalidCulcRPNTokenizingException("Priority not supported token: " + token.GetType().Name),
                };
            }

            foreach (var token in tokens)
            {
                if (token is NumberToken)
                {
                    yield return token;
                }
                else if (token is StringToken stringToken)
                {
                    if (methodPresenter.IsMethodName(in stringToken))
                    {
                        stack.Push(token);
                    }
                    else
                    {
                        yield return token;
                    }
                }
                else if (token is PrioritizingToken pToken)
                {
                    if (pToken.IsStart)
                    {
                        stack.Push(pToken);
                    }
                    else
                    {
                        while (stack.TryPop(out var outToken))
                        {
                            if (outToken is PrioritizingToken)
                            {
                                break;
                            }

                            if (outToken is SeparatorToken)
                            {
                                continue;
                            }

                            yield return outToken;
                        }
                    }
                }
                else if (token is OperatorToken opToken)
                {
                    while (stack.TryPeek(out var topToken) 
                        && topToken is OperatorToken or PrioritizingToken or StringToken
                        && ComparePriorityToken(topToken, token) > 0
                        )
                    {
                        yield return stack.Pop();
                    }

                    stack.Push(token);
                }
                else if (token is SeparatorToken)
                {
                    while (stack.TryPeek(out var outToken))
                    {
                        if (outToken is PrioritizingToken)
                        {
                            break;
                        }

                        if (outToken is SeparatorToken)
                        {
                            continue;
                        }

                        yield return stack.Pop();
                    }
                }
                else
                {
                    throw new InvalidCulcRPNTokenizingException("not supported (or not implemented) token: " + token.GetType().Name);
                }
            }

            while (stack.TryPop(out var outToken))
            {
                yield return outToken;
            }
        }

        public static ICulcExpressionTreeNode ToNode(IEnumerable<IToken> rpnTokens, IMethodNodeFactoryFactory methodNodeFactoryFactory)
        {
            Stack<ICulcExpressionTreeNode> buffer = new();
            foreach (var token in rpnTokens)
            {
                if (token is OperatorToken opToken)
                {
                    if (buffer.Count <= 1)
                    {
                        throw new InvalidCulcNodeException("OperatorToken required two or more child node.");
                    }
                    else 
                    {
                        var right = buffer.Pop();
                        var left = buffer.Pop();

                        ICulcExpressionTreeNode opNode = opToken.Operator switch
                        {
                            OperatorType.Plus => new PlusOperaterExpressionTreeNode(left, right),
                            OperatorType.Minus => new MinusOperaterExpressionTreeNode(left, right),
                            OperatorType.Mul => new MultipliedOperaterExpressionTreeNode(left, right),
                            OperatorType.Div => new DivideOperaterExpressionTreeNode(left, right),
                            _ => throw new InvalidCulcNodeException($"Not implemented OperatorType: {opToken.Operator}"),
                        };

                        buffer.Push(opNode);
                    }
                }
                else if (token is NumberToken numberToken)
                {
                    buffer.Push(new ConstValueExpressionTreeNode(numberToken.Number));
                }
                else if (token is StringToken stringToken)
                {
                    static ICulcExpressionTreeNode[] PopAll(Stack<ICulcExpressionTreeNode> stack, int count)
                    {
                        ICulcExpressionTreeNode[] nodes = new ICulcExpressionTreeNode[count];
                        foreach (var i in Enumerable.Range(0, count).Reverse())
                        {
                            nodes[i] = stack.Pop();
                        }

                        return nodes;
                    }

                    if (methodNodeFactoryFactory.IsMethodName(stringToken))
                    {
                        var methodNodeFactory = methodNodeFactoryFactory.GetFactory(in stringToken);
                        buffer.Push(methodNodeFactory.Create(PopAll(buffer, methodNodeFactory.ArgumentCount)));
                    }
                    else
                    {
                        buffer.Push(new VariableValueExpressionTreeNode(stringToken.String));
                    }
                }
                else
                {
                    throw new InvalidCulcNodeException($"Not supported token : {token.GetType().Name}");
                }
            }


            if (buffer.Count != 1)
            {
#if DEBUG
                foreach (var node in buffer)
                {
                    Debug.WriteLine(node.ToString());
                }
#endif
                throw new InvalidCulcNodeException($"Invalid parsed node count.: {buffer.Count}");
            }

            return buffer.Pop();
        }

        public static ICulcExpressionTreeNode CreateCulcExpressionTree(string input, IMethodNodeFactoryFactory methodNodeFactoryFactory)
        {
            // 1. トーカナイズ
            // 2. 逆ポーランド記法でスタックに積む
            // 3. スタックから取り出してNodeに変換
            return ToNode(ToRPN(CulcStringTokenizer.Tokenize(input), methodNodeFactoryFactory), methodNodeFactoryFactory);
        }

        public static ICulcExpressionTreeNode CreateCulcExpressionTree(string input)
        {
            return CreateCulcExpressionTree(input, MethodNodeFactoryFactory.CreateDefault());
        }
    }

    public sealed class CulcExpressionTreeCreationResult
    {
        public bool IsSuccess { get; }
        public ICulcExpressionTreeNode RootNode { get; }
    }


    public interface ICulcExpressionTreeContext
    {
        double GetValue(string variableName);
    }
    public interface ICulcExpressionTreeNode
    {
        double Culc(ICulcExpressionTreeContext context);
    }

    public class CulcExpressionTreeContext : ICulcExpressionTreeContext
    {
        public Dictionary<string, double> VariableToValueMap { get; } = new();

        public double GetValue(string variableName)
        {
            return VariableToValueMap.TryGetValue(variableName, out var val) ? val : throw new ArgumentException(variableName);
        }
    }

    public sealed class ConstValueExpressionTreeNode : ICulcExpressionTreeNode
    {
        public double Value { get; }

        public ConstValueExpressionTreeNode(double value)
        {
            Value = value;
        }

        public double Culc(ICulcExpressionTreeContext context)
        {
            return Value;
        }

        public override string ToString()
        {
            return $"{{Value:{Value}}}";
        }
    }

    public sealed class VariableValueExpressionTreeNode : ICulcExpressionTreeNode
    {
        public string VariableName { get; }

        public VariableValueExpressionTreeNode(string variableName)
        {
            VariableName = variableName;
        }

        public double Culc(ICulcExpressionTreeContext context)
        {
            return context.GetValue(VariableName);
        }

        public override string ToString()
        {
            return $"{{Variable:{VariableName}}}";
        }
    }


    public abstract class SingleOperatorExpressionTreeNodeBase : ICulcExpressionTreeNode
    {
        public SingleOperatorExpressionTreeNodeBase(ICulcExpressionTreeNode child)
        {
            Child = child;
        }

        public ICulcExpressionTreeNode Child { get; }

        public abstract double Culc(ICulcExpressionTreeContext context);
    }

    public sealed class MinusSingleOperaterExpressionTreeNode : SingleOperatorExpressionTreeNodeBase
    {
        public MinusSingleOperaterExpressionTreeNode(ICulcExpressionTreeNode child) : base(child)
        {
        }

        public override double Culc(ICulcExpressionTreeContext context)
        {
            return -Child.Culc(context);
        }

        public override string ToString()
        {
            return $"-{Child}";
        }
    }



    public abstract class BinaryOperatorExpressionTreeNodeBase : ICulcExpressionTreeNode
    {
        public BinaryOperatorExpressionTreeNodeBase(ICulcExpressionTreeNode left, ICulcExpressionTreeNode right)
        {
            Left = left;
            Right = right;
        }

        public ICulcExpressionTreeNode Left { get; }
        public ICulcExpressionTreeNode Right { get; }

        public abstract double Culc(ICulcExpressionTreeContext context);        
    }

    public sealed class PlusOperaterExpressionTreeNode : BinaryOperatorExpressionTreeNodeBase
    {
        public PlusOperaterExpressionTreeNode(ICulcExpressionTreeNode left, ICulcExpressionTreeNode right) : base(left, right)
        {
        }

        public override double Culc(ICulcExpressionTreeContext context)
        {
            return Left.Culc(context) + Right.Culc(context);
        }

        public override string ToString()
        {
            return $"({Left} + {Right})";
        }
    }

    public sealed class MinusOperaterExpressionTreeNode : BinaryOperatorExpressionTreeNodeBase
    {
        public MinusOperaterExpressionTreeNode(ICulcExpressionTreeNode left, ICulcExpressionTreeNode right) : base(left, right)
        {
        }

        public override double Culc(ICulcExpressionTreeContext context)
        {
            return Left.Culc(context) - Right.Culc(context);
        }

        public override string ToString()
        {
            return $"({Left} - {Right})";
        }
    }

    public sealed class DivideOperaterExpressionTreeNode : BinaryOperatorExpressionTreeNodeBase
    {
        public DivideOperaterExpressionTreeNode(ICulcExpressionTreeNode left, ICulcExpressionTreeNode right) : base(left, right)
        {
        }

        public override double Culc(ICulcExpressionTreeContext context)
        {
            return Left.Culc(context) / Right.Culc(context);
        }

        public override string ToString()
        {
            return $"({Left} / {Right})";
        }
    }

    public sealed class MultipliedOperaterExpressionTreeNode : BinaryOperatorExpressionTreeNodeBase
    {
        public MultipliedOperaterExpressionTreeNode(ICulcExpressionTreeNode left, ICulcExpressionTreeNode right) : base(left, right)
        {
        }

        public override double Culc(ICulcExpressionTreeContext context)
        {
            return Left.Culc(context) * Right.Culc(context);
        }

        public override string ToString()
        {
            return $"({Left} * {Right})";
        }
    }

    public abstract class MethodExpressionTreeNodeBase : ICulcExpressionTreeNode
    {

        public abstract double Culc(ICulcExpressionTreeContext context);
    }

    public sealed class MaxMethodExpressionTreeNode : MethodExpressionTreeNodeBase
    {
        public static readonly MethodNodeFactory Factory = new MethodNodeFactory();

        public class MethodNodeFactory : IMethodNodeFactory
        {
            internal MethodNodeFactory() { }

            public string MethodName => "Max";

            public int ArgumentCount => 2;

            public MethodExpressionTreeNodeBase Create(ICulcExpressionTreeNode[] argumentNodes)
            {
                return new MaxMethodExpressionTreeNode(argumentNodes);
            }
        }


        private readonly ICulcExpressionTreeNode[] _arguments;


        public MaxMethodExpressionTreeNode(ICulcExpressionTreeNode[] arguments)
        {
            _arguments = arguments;
        }

        public override double Culc(ICulcExpressionTreeContext context)
        {
            return _arguments.Select(x => x.Culc(context)).Max();
        }

        public override string ToString()
        {
            return $"{Factory.MethodName}({string.Join(',', _arguments.Select(x => x.ToString()))})";
        }
    }

    public sealed class MinMethodExpressionTreeNode : MethodExpressionTreeNodeBase
    {
        public static readonly MethodNodeFactory Factory = new MethodNodeFactory();

        public class MethodNodeFactory : IMethodNodeFactory
        {
            internal MethodNodeFactory() { }

            public string MethodName => "Min";

            public int ArgumentCount => 2;

            public MethodExpressionTreeNodeBase Create(ICulcExpressionTreeNode[] argumentNodes)
            {
                return new MinMethodExpressionTreeNode(argumentNodes);
            }
        }

        private readonly ICulcExpressionTreeNode[] _arguments;

        public MinMethodExpressionTreeNode(ICulcExpressionTreeNode[] arguments)
        {
            _arguments = arguments;
        }

        public override double Culc(ICulcExpressionTreeContext context)
        {
            return _arguments.Select(x => x.Culc(context)).Min();
        }

        public override string ToString()
        {
            return $"{Factory.MethodName}({string.Join(',', _arguments.Select(x => x.ToString()))})";
        }
    }

    public sealed class ClampMethodExpressionTreeNode : MethodExpressionTreeNodeBase
    {
        public static readonly MethodNodeFactory Factory = new MethodNodeFactory();

        public class MethodNodeFactory : IMethodNodeFactory
        {
            internal MethodNodeFactory() { }

            public string MethodName => "Clamp";

            public int ArgumentCount => 3;

            public MethodExpressionTreeNodeBase Create(ICulcExpressionTreeNode[] argumentNodes)
            {
                return new ClampMethodExpressionTreeNode(argumentNodes);
            }
        }

        private readonly ICulcExpressionTreeNode[] _arguments;

        public ClampMethodExpressionTreeNode(ICulcExpressionTreeNode[] arguments)
        {
            _arguments = arguments;
        }

        public override double Culc(ICulcExpressionTreeContext context)
        {
            var value = _arguments[0].Culc(context);
            var min = _arguments[1].Culc(context);
            var max = _arguments[2].Culc(context);

            return Math.Clamp(value, min, max);
        }

        public override string ToString()
        {
            return $"{Factory.MethodName}({string.Join(',', _arguments.Select(x => x.ToString()))})";
        }
    }

    public sealed class LimitMethodExpressionTreeNode : MethodExpressionTreeNodeBase
    {
        public static readonly MethodNodeFactory Factory = new MethodNodeFactory();

        public class MethodNodeFactory : IMethodNodeFactory
        {
            internal MethodNodeFactory() { }

            public string MethodName => "Limit";

            public int ArgumentCount => 2;

            public MethodExpressionTreeNodeBase Create(ICulcExpressionTreeNode[] argumentNodes)
            {
                return new LimitMethodExpressionTreeNode(argumentNodes);
            }
        }

        private readonly ICulcExpressionTreeNode[] _arguments;

        public LimitMethodExpressionTreeNode(ICulcExpressionTreeNode[] arguments)
        {
            _arguments = arguments;
        }

        public override double Culc(ICulcExpressionTreeContext context)
        {
            var value = _arguments[0].Culc(context);
            var max = _arguments[1].Culc(context);

            return Math.Clamp(value, 0, max);
        }

        public override string ToString()
        {
            return $"{Factory.MethodName}({string.Join(',', _arguments.Select(x => x.ToString()))})";
        }
    }
}
