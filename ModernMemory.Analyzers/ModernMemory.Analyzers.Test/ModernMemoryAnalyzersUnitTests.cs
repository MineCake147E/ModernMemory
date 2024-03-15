using System.Threading.Tasks;

using NUnit.Framework;

using VerifyCS = ModernMemory.Analyzers.Test.CSharpCodeFixVerifier<
    ModernMemory.Analyzers.NativeSpanCollectionExpressionAnalyzer,
    ModernMemory.Analyzers.ModernMemoryAnalyzersCodeFixProvider>;

namespace ModernMemory.Analyzers.Test
{
    [TestFixture]
    public class ModernMemoryAnalyzersUnitTest
    {
        //No diagnostics expected to show up
        [Test]
        public async Task CompliantCaseDoesNotGetAnyDiagnosticsAsync()
        {
            var test = @"
using System;
using ModernMemory;
namespace TestProgram
{
    internal class Program
    {
        public static void A()
        {
            NativeSpan<int> a = new([0, 1, 2, 3]);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [Test]
        public async Task NonCompliantCaseGetsDiagnosticsAsync()
        {
            var test = @"
using System;
using ModernMemory;
namespace TestProgram
{
    internal class Program
    {
        public static void A()
        {
            NativeSpan<int> a = {|#0:[0, 1, 2, 3]|};
        }
    }
}
";

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TYPENAME
        {   
        }
    }";

            var expected = VerifyCS.Diagnostic("NativeSpanCollectionExpression").WithLocation(0);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}
