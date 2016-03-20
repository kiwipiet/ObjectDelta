using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ObjectDelta
{
    internal class JTokenComparer : IEqualityComparer<JToken>
    {
        public bool Equals(JToken x, JToken y)
        {
            if (x == null && y == null)
                return true;
            if (x == null || y == null)
                return false;
            return JToken.DeepEquals(x, y);
        }
        public int GetHashCode(JToken i)
        {
            return i.ToString().GetHashCode();
        }
    }
}