using Microsoft.VisualStudio.TestTools.UnitTesting;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain.Expressions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest
{
    [TestClass]
    public sealed class ScoringTextParseTest
    {
        [TestMethod]
        [DataRow( "1 + 2 * 3 / 4")]
        [DataRow("Max(5 + 6, 1 * 10)")]
        public void Tokenize(string input)
        {
            var tokens = ExpressionTokenizer.Tokenize(input);

            Debug.WriteLine(string.Join(' ', tokens.Select(x => x.ToString())));
        }

        [TestMethod]
        [DataRow("1 + 2 * 3 / 4")]
        [DataRow("Max(5 + 6, 1 * 10)")]
        public void TokenizeWithRPN(string input)
        {
            var methodNodeFactoryFactory = MethodNodeFactoryFactory.CreateDefault();

            var tokens = ExpressionTokenizer.Tokenize(input);
            var rpnTokens = CulcExpressionTree.ToRPN(tokens, methodNodeFactoryFactory);

            Debug.WriteLine(string.Join(' ', tokens.Select(x => x.ToString())));
            Debug.WriteLine(string.Join(' ', rpnTokens.Select(x => x.ToString())));
        }


        [TestMethod]
        [DataRow("1 + 2 * 3 / 4")]
        [DataRow("Max(5 + 6, 1 * 10)")]
        public void ToNode(string input)
        {
            var methodNodeFactoryFactory = MethodNodeFactoryFactory.CreateDefault();

            var tokens = ExpressionTokenizer.Tokenize(input);
            var rpnTokens = CulcExpressionTree.ToRPN(tokens, methodNodeFactoryFactory);
            var node = CulcExpressionTree.ToNode(rpnTokens, methodNodeFactoryFactory);

            Debug.WriteLine(node.ToString());
        }

        [TestMethod]
        [DataRow("V + C + M + L")]
        [DataRow("V + C + M * 20 + L")]
        [DataRow("Max(V, Max(C, M * 40))")]
        [DataRow("Clamp(V, 0, 1000)")]
        [DataRow("Limit(V, M * 200) + C * (V + M) / (V + M + C) + Limit((M + L) * 25, V * 2)")]
        public void CulcExpressionTreeTest(string input)
        {
            var node = CulcExpressionTree.CreateCulcExpressionTree(input);

            var context = new CulcExpressionTreeContext();
            context.VariableToValueMap.Add("V", 20);
            context.VariableToValueMap.Add("C", 3);
            context.VariableToValueMap.Add("M", 1);
            context.VariableToValueMap.Add("L", 0.1);

            Debug.WriteLine(node.ToString());

            var result = node.Culc(context);
            Debug.WriteLine(result);
        }


        [TestMethod]
        [DataRow("(1 + + 2")]
        [DataRow("(1 + 2))")]
        [DataRow(", ,")]
        [DataRow("Max(V, C, M * 40))")]
        public void InvalidParsing(string input)
        {
            Assert.ThrowsException<Exception>(
                () => CulcExpressionTree.CreateCulcExpressionTree(input));
        }
    }
}
