using NiconicoToolkit.SnapshotSearch.JsonFilters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Models.Domain.Expressions
{
    public class LogicalOperatorExpressionTree 
    {

    }

    public readonly struct LogicalUnaryOperatorToken : IToken
    {
        public LogicalUnaryOperatorToken(int position, LogicalUnaryOperatorType operatorType)
        {
            Position = position;
            Operator = operatorType;
        }

        public readonly int Position { get; }
        public readonly LogicalUnaryOperatorType Operator { get; }
        public override readonly string ToString() => Operator switch
        {
            LogicalUnaryOperatorType.Not => "not",
            _ => throw new NotSupportedException(),
        };
    }

    public readonly struct LogicalBinaryOperatorToken : IToken
    {
        public LogicalBinaryOperatorToken(int position, LogicalBinaryOperatorType operatorType)
        {
            Position = position;
            Operator = operatorType;
        }

        public readonly int Position { get; }
        public readonly LogicalBinaryOperatorType Operator { get; }
        public override readonly string ToString() => Operator switch
        {
            LogicalBinaryOperatorType.And => "and",
            LogicalBinaryOperatorType.Or => "or",
            _ => throw new NotSupportedException(),
        };
    }

    public enum LogicalUnaryOperatorType
    {
        Not,
    }

    public enum LogicalBinaryOperatorType
    {
        And,
        Or,
    }

    public static class LogicalOperatorExpressionTokenizer
    {
        readonly static HashSet<string> _operatorSymbols = new() 
        {
            "not",
            "and", 
            "or", 
        };
        readonly static HashSet<char> _prioritizingSymbols = "()".ToHashSet();
        readonly static HashSet<char> _splitCharacters = " \n\r\t".Concat(_prioritizingSymbols).ToHashSet();

        enum BufferStackingMode
        {
            NotArrivalChar,
            String,
            LogicalOperator,
            Prioritizing,
        }



        public static IJsonSearchFilter Parse(string input, Dictionary<string, IJsonSearchFilter> dataBag)
        {
            return ToJsonSearchFilter(ToRPN(Tokenize(input)), dataBag);
        }



        public static IEnumerable<IToken> Tokenize(string input)
        {
            StringBuilder buffer = new StringBuilder();
            BufferStackingMode bufferStackingMode = BufferStackingMode.NotArrivalChar;
            int currentPosition = 0;

            static void ThrowParseErrorException(char c, int currentPos)
            {
                throw MakeExpressionParseExpcetion(c, currentPos);
            }

            static InvalidExpressionTokenExpcetion MakeExpressionParseExpcetion(char c, int currentPos)
            {
                return new InvalidExpressionTokenExpcetion($"Invalid character detected: character = {c}, Position = {currentPos}");
            }

            foreach (var c in input)
            {
                if (_splitCharacters.Contains(c))
                {
                    if (buffer.Length != 0)
                    {
                        yield return bufferStackingMode switch
                        {
                            BufferStackingMode.String => buffer.ToString() switch
                            {
                                "not" => new LogicalUnaryOperatorToken(currentPosition, LogicalUnaryOperatorType.Not),
                                "and" => new LogicalBinaryOperatorToken(currentPosition, LogicalBinaryOperatorType.And),
                                "or" => new LogicalBinaryOperatorToken(currentPosition, LogicalBinaryOperatorType.Or),
                                _ and var str => new StringToken(currentPosition, str),
                            },
                            _ => throw MakeExpressionParseExpcetion(c, currentPosition),
                        };

                        buffer.Clear();
                        bufferStackingMode = BufferStackingMode.NotArrivalChar;
                    }

                    if (_prioritizingSymbols.Contains(c))
                    {
                        yield return new PrioritizingToken(currentPosition, c == '(');
                        bufferStackingMode = BufferStackingMode.Prioritizing;
                    }
                    else
                    {
                        // 空白や改行文字などは無視するだけ
                    }
                }
                else if (buffer.Length == 0)
                {
                    bufferStackingMode = BufferStackingMode.String;
                    buffer.Append(c);
                }
                else if (bufferStackingMode is BufferStackingMode.String)
                {
                    buffer.Append(c);
                }
                else
                {
                    ThrowParseErrorException(c, currentPosition);
                }

                currentPosition++;
            }

            if (buffer.Length != 0)
            {
                yield return bufferStackingMode switch
                {
                    BufferStackingMode.String => buffer.ToString() switch
                    {
                        "not" => new LogicalUnaryOperatorToken(currentPosition, LogicalUnaryOperatorType.Not),
                        "and" => new LogicalBinaryOperatorToken(currentPosition, LogicalBinaryOperatorType.And),
                        "or" => new LogicalBinaryOperatorToken(currentPosition, LogicalBinaryOperatorType.Or),
                        _ and var str => new StringToken(currentPosition, str),
                    },
                    _ => throw MakeExpressionParseExpcetion('\n', currentPosition),
                };
            }
        }



        public static IEnumerable<IToken> ToRPN(IEnumerable<IToken> tokens)
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
                    LogicalUnaryOperatorToken uOpToken => uOpToken.Operator switch
                    {
                        LogicalUnaryOperatorType.Not => -1,
                        _ => throw new InvalidCulcRPNTokenizingException("Priority not supported OperatorType: " + uOpToken.Operator.ToString()),
                    },
                    LogicalBinaryOperatorToken opToken => opToken.Operator switch
                    {
                        LogicalBinaryOperatorType.And => 3,
                        LogicalBinaryOperatorType.Or => 4,                        
                        _ => throw new InvalidCulcRPNTokenizingException("Priority not supported OperatorType: " + opToken.Operator.ToString()),
                    },
                    _ => throw new InvalidCulcRPNTokenizingException("Priority not supported token: " + token.GetType().Name),
                };
            }

            foreach (var token in tokens)
            {
                if (token is StringToken stringToken)
                {
                    yield return token;

                    while (stack.TryPeek(out var topToken)
                        && topToken is LogicalUnaryOperatorToken
                    )
                    {
                        yield return stack.Pop();
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

                            yield return outToken;
                        }

                        while (stack.TryPeek(out var topToken)
                            && topToken is LogicalUnaryOperatorToken
                        )
                        {
                            yield return stack.Pop();
                        }
                    }
                }
                else if (token is LogicalBinaryOperatorToken)
                {
                    while (stack.TryPeek(out var topToken)
                        && topToken is LogicalBinaryOperatorToken or PrioritizingToken
                        && ComparePriorityToken(topToken, token) > 0
                        )
                    {
                        yield return stack.Pop();
                    }

                    stack.Push(token);
                }
                else if (token is LogicalUnaryOperatorToken)
                {
                    stack.Push(token);
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





        public static IJsonSearchFilter ToJsonSearchFilter(IEnumerable<IToken> rpnTokens, Dictionary<string, IJsonSearchFilter> dataBag)
        {
            Stack<IJsonSearchFilter> buffer = new();
            foreach (var token in rpnTokens)
            {
                if (token is StringToken strToken)
                {
                    buffer.Push(dataBag.TryGetValue(strToken.String, out var filter) ? filter : throw new InvalidCulcNodeException($"Not supported token : {strToken.String}"));
                }
                else if (token is LogicalBinaryOperatorToken biOpToken)
                {
                    var right = buffer.Pop();
                    var left = buffer.Pop();

                    if (biOpToken.Operator is LogicalBinaryOperatorType.And
                        && (right is AndJsonFilter || left is AndJsonFilter)
                        )
                    {
                        if (right is AndJsonFilter rightAnd)
                        {
                            rightAnd.Filters.Insert(0, left);
                            buffer.Push(rightAnd);
                        }
                        else if (left is AndJsonFilter leftAnd)
                        {
                            leftAnd.Filters.Insert(0, right);
                            buffer.Push(leftAnd);
                        }
                        else
                        {
                            throw new InvalidCulcNodeException("And operator aggregation failed.");
                        }
                    }
                    else if (biOpToken.Operator is LogicalBinaryOperatorType.Or
                        && (right is OrJsonFilter || left is OrJsonFilter)
                        )
                    {
                        if (right is OrJsonFilter rightOr)
                        {
                            rightOr.Filters.Insert(0, left);
                            buffer.Push(rightOr);
                        }
                        else if (left is OrJsonFilter leftOr)
                        {
                            leftOr.Filters.Insert(0, right);
                            buffer.Push(leftOr);
                        }
                        else
                        {
                            throw new InvalidCulcNodeException("Or operator aggregation failed.");
                        }
                    }
                    else
                    {
                        buffer.Push(biOpToken.Operator switch
                        {
                            LogicalBinaryOperatorType.And => new AndJsonFilter(new[] { left, right }),
                            LogicalBinaryOperatorType.Or => new OrJsonFilter(new[] { left, right }),
                            _ => throw new InvalidCulcNodeException($"Not supported token : {biOpToken.Operator}")
                        }
                        );
                    }
                }
                else if (token is LogicalUnaryOperatorToken uOpToken)
                {
                    buffer.Push(new NotJsonFilter(buffer.Pop()));
                }
                else
                {
                    throw new InvalidCulcNodeException($"Not supported token : {token.GetType().Name}");
                }
            }

            if (buffer.Count != 1)
            {
                throw new InvalidCulcNodeException($"Invalid parsed node count.: {buffer.Count}");
            }

            return buffer.Pop();
        }
    }

}
