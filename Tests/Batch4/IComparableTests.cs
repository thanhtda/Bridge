using Bridge.Test;
using System;

namespace Bridge.ClientTest.Batch4
{
    [TestFixture(TestNameFormat = "IComparableTests - {0}")]
    public class IComparableTests
    {
        private class MyComparable : IComparable<MyComparable>
        {
            public int result;
            public MyComparable other;

            public int CompareTo(MyComparable other)
            {
                this.other = other;
                return result;
            }
        }

        [Test]
        public void CallingMethodThroughIComparableInterfaceInvokesImplementingMethod()
        {
            MyComparable a = new MyComparable(), b = new MyComparable();
            a.result = 534;
            Assert.AreEqual(534, ((IComparable<MyComparable>)a).CompareTo(b));
            Assert.AreStrictEqual(b, a.other);

            a.result = -42;
            Assert.AreEqual(-42, ((IComparable<MyComparable>)a).CompareTo(null));
            Assert.AreStrictEqual(null, a.other);

            a.result = -534;
            Assert.AreEqual(-534, a.CompareTo(b));
            Assert.AreStrictEqual(b, a.other);

            a.result = 42;
            Assert.AreEqual(42, a.CompareTo(null));
            Assert.AreStrictEqual(null, a.other);
        }
    }
}
