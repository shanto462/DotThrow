using DotThrow.ExceptionExtensions;
using NUnit.Framework;
using System.Linq;

namespace DotThrow.Tests
{
    public class DotThrowStringTest
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestPredicate()
        {
            string a = "aaaaaaaaaaaaaaaaaaaaaaa";

            a.CreateThrower().ThrowIf(a => a.Any(c => c == 'a'), "Char a found on string");
        }
    }
}