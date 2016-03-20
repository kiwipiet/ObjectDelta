using System;
using Newtonsoft.Json.Linq;

namespace ObjectDelta
{
    public class ObjectDeltaResult
    {
        private static readonly JTokenComparer JTokenComparer = new JTokenComparer();
        public bool AreEqual => JTokenComparer.Equals(OldValues, NewValues);

        /// <summary>
        /// The type of the compared objects.
        /// </summary>
        public Type Type { get; set; }

        public JObject OldValues { get; set; }

        public JObject NewValues { get; set; }
    }
}