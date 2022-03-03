using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DotThrow.Helpers
{
    public static class XmlHelpers
    {
        public static bool IsValidXml(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return false;
            try
            {
                XmlDocument xmlDoc = new();
                xmlDoc.LoadXml(str);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }
    }
}
