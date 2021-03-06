using Bridge.Test;
using System;

namespace Bridge.ClientTest.CSharp6
{
    [Category(Constants.MODULE_BASIC_CSHARP)]
    [TestFixture(TestNameFormat = "Exception filter - {0}")]
    public class TestExceptionFilter
    {
        private static MyException LogParameter;

        [Test]
        public static void TestFalseFilter()
        {
            var isCaught = false;
            try
            {
                try
                {
                    throw new MyException();
                }
                catch (MyException) when (Log(null, false))
                {
                    Assert.Fail("Flow should not be in catch block");
                }
            }
            catch (MyException)
            {
                isCaught = true;
            }

            Assert.True(isCaught);
        }

        [Test]
        public static void TestTrueFilter()
        {
            var isCaught = false;

            LogParameter = null;

            try
            {
                throw new MyException();
            }
            catch (MyException e) when (Log(e, true))
            {
                isCaught = true;
            }

            Assert.True(isCaught);
            Assert.NotNull(LogParameter, "Log() parameter was MyException");
        }

        [Test] // #2223
        public static void TestMultipleCatchClauses_2223()
        {
            bool b = false;
            try
            {
                DoSomethingThatMightFail();
            }
            catch (Exception) when (b)
            {
                Assert.Fail();
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("DoSomethingThatMightFail", ex.Message);
            }
            catch (Exception)
            {
                Assert.Fail();
            }
        }

        [Test] // #2223
        public static void TestFailedFilter_2223()
        {
            int a = 7;
            int b = 0;
#pragma warning disable 219
            var _e1 = 1; // test autogenerated variables conflict
#pragma warning restore 219

            try
            {
                DoSomethingThatMightFail();
            }
            catch (Exception) when (a / b == 0)
            {
                Assert.Fail();
            }
            catch (DivideByZeroException)
            {
                Assert.Fail();
            }
            catch (Exception ex)
            {
                Assert.AreEqual("DoSomethingThatMightFail", ex.Message);
            }
        }

        [Test] // #2223
        public static void TestFailedFilter2_2223()
        {
            int a = 7;
            int b = 0;

            Assert.Throws<ArgumentNullException>(() =>
            {
                try
                {
                    DoSomethingThatMightFail();
                }
                catch (Exception) when (a / b == 0)
                {
                    Assert.Fail();
                }
                catch (DivideByZeroException)
                {
                    Assert.Fail();
                }
            });
        }

        private static void DoSomethingThatMightFail()
        {
            throw new ArgumentNullException("", "DoSomethingThatMightFail");
        }

        private static bool Log(Exception e, bool result)
        {
            if (e != null)
            {
                LogParameter = e as MyException;
            }

            return result;
        }

        public class MyException : Exception
        {
        }
    }
}