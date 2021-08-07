using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace NicoVideoSnapshotSearchAssistanceTools.Models.Domain.Scoring
{
    public interface IToken { int Position { get; } }

    public readonly struct NumberToken : IToken
    {
        public NumberToken(int position, double number)
        {
            Position = position;
            Number = number;
        }

        public readonly int Position { get; }
        public readonly double Number { get; }
        public override readonly string ToString()
        {
            return Number.ToString();
        }
    }


    public readonly struct StringToken : IToken
    {
        public StringToken(int position, string str)
        {
            Position = position;
            String = str;
        }

        public readonly int Position { get; }
        public readonly string String { get; }
        public override readonly string ToString() => String;
    }

    public readonly struct OperatorToken: IToken
    {
        public OperatorToken(int position, OperatorType operatorType)
        {
            Position = position;
            Operator = operatorType;
        }

        public readonly int Position { get; }
        public readonly OperatorType Operator { get; }
        public override readonly string ToString() => Operator switch
        {
            OperatorType.Plus => "+",
            OperatorType.Minus => "-",
            OperatorType.Mul => "*",
            OperatorType.Div => "/",
            _ => throw new NotSupportedException(),
        };
    }

    public readonly struct SeparatorToken : IToken
    {
        public SeparatorToken(int position)
        {
            Position = position;
        }

        public readonly int Position { get; }
        public override readonly string ToString() => ",";
    }

    public readonly struct PrioritizingToken : IToken
    {
        public PrioritizingToken(int position, bool isStart)
        {
            Position = position;
            IsStart = isStart;
        }

        public readonly int Position { get; }
        public readonly bool IsStart { get; }
        public override readonly string ToString() => IsStart ? "( " : " )";
    }

    public enum OperatorType
    {
        Plus,
        Minus,
        Mul,
        Div,
    }

    public class InvalidSocreCulcStringTokenExpcetion : Exception
    {
        public InvalidSocreCulcStringTokenExpcetion(string message) : base(message)
        {
        }
    }

    public static class CulcStringTokenizer
    {
        readonly static HashSet<char> _operatorSymbols = "+-*/".ToHashSet();
        readonly static HashSet<char> _prioritizingSymbols = "()".ToHashSet();
        readonly static HashSet<char> _splitCharacters = " (),\n\r\t".Concat(_operatorSymbols).ToHashSet();

        enum BufferStackingMode
        {
            NotArrivalChar,
            Number,
            String,
            MethodArgumentSeparator,
            Operator,
            Prioritizing,
        }

        public static IEnumerable<IToken> Tokenize(string input)
        {
            StringBuilder buffer = new StringBuilder();
            BufferStackingMode bufferStackingMode = BufferStackingMode.NotArrivalChar;
            int currentPosition = 0;

            static void ThrowParseErrorException(char c, int currentPos)
            {
                throw MakeStringParseExpcetion(c, currentPos);
            }

            static InvalidSocreCulcStringTokenExpcetion MakeStringParseExpcetion(char c, int currentPos)
            {
                return new InvalidSocreCulcStringTokenExpcetion($"Invalid character detected: character = {c}, Position = {currentPos}");
            }
            
            foreach (var c in input)
            {
                if (_splitCharacters.Contains(c))
                {
                    if (buffer.Length != 0)
                    {
                        yield return bufferStackingMode switch
                        {
                            BufferStackingMode.String => new StringToken(currentPosition, buffer.ToString()),
                            BufferStackingMode.Number => new NumberToken(currentPosition, double.Parse(buffer.ToString())),
                            _ => throw MakeStringParseExpcetion(c, currentPosition),
                        };

                        buffer.Clear();
                        bufferStackingMode = BufferStackingMode.NotArrivalChar;
                    }

                    if (_operatorSymbols.Contains(c))
                    {                        
                        var operatorType = c switch
                        {
                            '+' => OperatorType.Plus,
                            '-' => OperatorType.Minus,
                            '*' => OperatorType.Mul,
                            '/' => OperatorType.Div,
                            _ => throw MakeStringParseExpcetion(c, currentPosition),
                        };

                        yield return new OperatorToken(currentPosition, operatorType);
                        bufferStackingMode = BufferStackingMode.Operator;
                    }
                    else if (_prioritizingSymbols.Contains(c))
                    {
                        yield return new PrioritizingToken(currentPosition, c == '(');
                        bufferStackingMode = BufferStackingMode.Prioritizing;
                    }
                    else if (c == ',')
                    {
                        if (bufferStackingMode == BufferStackingMode.MethodArgumentSeparator)
                        {
                            ThrowParseErrorException(c, currentPosition);
                        }

                        yield return new SeparatorToken(currentPosition);
                        bufferStackingMode = BufferStackingMode.MethodArgumentSeparator;
                    }
                    else // 空白や改行文字などは無視するだけ
                    {

                    }
                }
                else if (buffer.Length == 0)
                {
                    if (char.IsNumber(c) || c == '.')
                    {
                        bufferStackingMode = BufferStackingMode.Number;
                    }
                    else
                    {
                        bufferStackingMode = BufferStackingMode.String;
                    }

                    buffer.Append(c);
                }
                else if (bufferStackingMode is BufferStackingMode.Number)
                {
                    if (char.IsNumber(c) || c == '.')
                    {
                        buffer.Append(c);
                    }
                    else
                    {
                        ThrowParseErrorException(c, currentPosition);
                    }
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
                    BufferStackingMode.String => new StringToken(currentPosition, buffer.ToString()),
                    BufferStackingMode.Number => new NumberToken(currentPosition, double.Parse(buffer.ToString())),
                    _ => throw MakeStringParseExpcetion('\n', currentPosition),
                };
            }
        }

    }
}
