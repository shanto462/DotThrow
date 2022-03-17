using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotThrow.Extensions
{
    public static class IEunemerableExtensions
    {
        public static bool Any(this IEnumerable collection)
        {
            IEnumerator enumerator = collection.GetEnumerator();
            while (enumerator.MoveNext())
            {
                return true;
            }
            return false;
        }

        public static int Count(this IEnumerable collection)
        {
            IEnumerator enumerator = collection.GetEnumerator();
            int count = 0;
            while (enumerator.MoveNext())
            {
                count++;
            }
            return count;
        }
    }
}
