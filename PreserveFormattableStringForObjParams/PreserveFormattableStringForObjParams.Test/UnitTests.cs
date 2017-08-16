using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using PreserveFormattableStringForObjParams;

namespace PreserveFormattableStringForObjParams.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {

        //No diagnostics expected to show up
        [TestMethod]
        public void EmptyFileDoNothing()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void NotStringDoNothing()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TypeName
        {
            public void Bar()
{
    Foo(null);
}

            public void Foo(object obj)
{
}
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void NotInterpolatedStringDoNothing()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TypeName
        {
            public void Bar()
{
    Foo(""abc"");
}

            public void Foo(object obj)
{
}
        }
    }";

            VerifyCSharpDiagnostic(test);

        }

        [TestMethod]
        public void InerpolatedStringPassedAsObjIsError()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Bar()
        {
            Foo($""abc{1}"");
        }

        public void Foo(object obj)
        {
        }
    }
}";
            var expected = new DiagnosticResult
            {
                Id = "PreserveFormattableStringForObjParams",
                Message = PreserveFormattableStringForObjParamsAnalyzer.MessageFormat,
                Severity = DiagnosticSeverity.Error,
                Locations = new[] 
                            {
                                new DiagnosticResultLocation("Test0.cs", 15, 17)
                            }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Bar()
        {
            Foo((FormattableString)$""abc{1}"");
        }

        public void Foo(object obj)
        {
        }
    }
}";
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void InerpolatedStringPassedAsObjIsErrorAdv()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Bar()
        {
            Foo($""abc{1}"", obj2 : $""abc{2}"");
        }

        public void Foo(object obj, object obj2)
        {
        }
    }
}";
            var expected1 = new DiagnosticResult
            {
                Id = "PreserveFormattableStringForObjParams",
                Message = PreserveFormattableStringForObjParamsAnalyzer.MessageFormat,
                Severity = DiagnosticSeverity.Error,
                Locations = new[]
                            {
                                new DiagnosticResultLocation("Test0.cs", 15, 17)
                            }
            };

            var expected2 = new DiagnosticResult
            {
                Id = "PreserveFormattableStringForObjParams",
                Message = PreserveFormattableStringForObjParamsAnalyzer.MessageFormat,
                Severity = DiagnosticSeverity.Error,
                Locations = new[]
                            {
                                new DiagnosticResultLocation("Test0.cs", 15, 28)
                            }
            };

            VerifyCSharpDiagnostic(test, expected1, expected2);

            var fixtest = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Bar()
        {
            Foo((FormattableString)$""abc{1}"", obj2 : (FormattableString)$""abc{2}"");
        }

        public void Foo(object obj, object obj2)
        {
        }
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new PreserveFormattableStringForObjParamsCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new PreserveFormattableStringForObjParamsAnalyzer();
        }
    }
}