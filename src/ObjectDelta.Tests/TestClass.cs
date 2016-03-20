using System.Collections.Generic;

namespace ObjectDelta.Tests
{
    public class TestClass
    {
        public string StringProperty { get; set; }
        public int IntProperty { get; set; }
        public double DoubleProperty { get; set; }
        public List<TestClass> ListOfObjectProperty { get; set; }
        public List<string> ListOfStringProperty { get; set; }
        public List<int> ListOfIntProperty { get; set; }
        public List<double> ListOfDoubleProperty { get; set; }
    }
}