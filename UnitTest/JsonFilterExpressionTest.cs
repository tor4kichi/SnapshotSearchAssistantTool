using Microsoft.VisualStudio.TestTools.UnitTesting;
using NiconicoToolkit.SnapshotSearch.JsonFilters;
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
    public class JsonFilterExpressionTest
    {
        [TestMethod]
        [DataRow("A and B and C or D")]
        public void Tokenize(string input)
        {
            var tokens = LogicalOperatorExpressionTokenizer.Tokenize(input);

            Debug.WriteLine(string.Join(" ", tokens));
        }

        [TestMethod]
        [DataRow("A and B and C or D")]
        [DataRow("A and (B and C or D)")]
        [DataRow("(A and (B and C) or D)")]
        [DataRow("not (A and not (not B and not C) or not D)")]
        public void ToRPN(string input)
        {
            var tokens = LogicalOperatorExpressionTokenizer.Tokenize(input);
            var rpnTokens = LogicalOperatorExpressionTokenizer.ToRPN(tokens);

            Debug.WriteLine(string.Join(" ", tokens));
            Debug.WriteLine(string.Join(" ", rpnTokens));
        }

        [TestMethod]
        [DataRow("A and B and C or D")]
        [DataRow("A and (B and C or D)")]
        [DataRow("(A and (B and C) or D)")]
        [DataRow("not (A and not (not B and not C) or not D)")]
        public void ToJsonFilter(string input)
        {
            var tokens = LogicalOperatorExpressionTokenizer.Tokenize(input);
            var rpnTokens = LogicalOperatorExpressionTokenizer.ToRPN(tokens);


            Dictionary<string, IJsonSearchFilter> dataBag = new()
            {
                { "A", new EqualJsonFilter(NiconicoToolkit.SnapshotSearch.SearchFieldType.ContentId, "sm12345" ) },
                { "B", new RangeJsonFilter(NiconicoToolkit.SnapshotSearch.SearchFieldType.StartTime, DateTimeOffset.Now, DateTimeOffset.Now, true, true) },
                { "C", new RangeJsonFilter(NiconicoToolkit.SnapshotSearch.SearchFieldType.StartTime, DateTimeOffset.Now, DateTimeOffset.Now, true, true) },
                { "D", new RangeJsonFilter(NiconicoToolkit.SnapshotSearch.SearchFieldType.StartTime, DateTimeOffset.Now, DateTimeOffset.Now, true, true) },
            };

            var filter = LogicalOperatorExpressionTokenizer.ToJsonSearchFilter(rpnTokens, dataBag);

            Debug.WriteLine(string.Join(" ", tokens));
            Debug.WriteLine(string.Join(" ", rpnTokens));
            Debug.WriteLine(filter.ToString());
        }

    }
}
