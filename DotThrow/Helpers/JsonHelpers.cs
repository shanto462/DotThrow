using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotThrow.Helpers
{
    public static class JsonHelpers
    {
        public static bool IsValidJson(string strInput)
        {
            if (string.IsNullOrWhiteSpace(strInput))
                return false;

            strInput = strInput.Trim();

            if (IsJsonObject(strInput) || IsJsonArray(strInput))
            {
                return ThrowIfParseError(strInput);
            }
            return false;
        }

        private static bool ThrowIfParseError(string strInput)
        {
            try
            {
                JToken.Parse(strInput);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsJsonArray(string strInput)
        {
            return strInput.StartsWith("[") && strInput.EndsWith("]");
        }

        private static bool IsJsonObject(string strInput)
        {
            return strInput.StartsWith("{") && strInput.EndsWith("}");
        }
    }
}
