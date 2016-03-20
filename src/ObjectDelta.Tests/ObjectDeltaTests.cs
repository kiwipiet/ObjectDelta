using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Should;

namespace ObjectDelta.Tests
{
    public class ObjectDeltaTests
    {
        public void AbleToDeltaAndPatchSimpleObject()
        {
            var testObj = GetSimpleTestObject();

            var updatedTestObj = GetSimpleTestObject();
            updatedTestObj.StringProperty = "this is an updated string";
            updatedTestObj.IntProperty = 5678;
            updatedTestObj.DoubleProperty = 123.456;

            var delta = ObjectDelta.GenerateDelta(testObj, updatedTestObj);

            var revertPatch = JsonConvert.SerializeObject(delta.OldValues);

            var revertedObj = ObjectDelta.PatchObject(updatedTestObj, revertPatch);

            testObj.StringProperty.ShouldEqual(revertedObj.StringProperty);
            testObj.IntProperty.ShouldEqual(revertedObj.IntProperty);
            testObj.DoubleProperty.ShouldEqual(revertedObj.DoubleProperty);
        }

        public void AbleToDeleteStringListItemThenRevertViaPatch()
        {
            var testObj = GetSimpleTestObject();
            PopulateStringListOnTestClass(testObj);

            var updatedTestObj = GetSimpleTestObject();
            PopulateStringListOnTestClass(updatedTestObj);

            updatedTestObj.ListOfStringProperty.Remove("list");

            testObj.ListOfStringProperty.SequenceEqual(updatedTestObj.ListOfStringProperty).ShouldBeFalse();

            var deltaResult = ObjectDelta.GenerateDelta(testObj, updatedTestObj);

            var revertPatch = JsonConvert.SerializeObject(deltaResult.OldValues);

            var revertedObj = ObjectDelta.PatchObject(updatedTestObj, revertPatch);

            testObj.ListOfStringProperty.SequenceEqual(revertedObj.ListOfStringProperty).ShouldBeTrue();
        }

        public void AbleToDeleteObjectListItemThenRevertViaPatch()
        {
            var testObj = GetSimpleTestObject();
            PopulateObjectListOnTestClass(testObj);

            var updatedTestObj = GetSimpleTestObject();
            PopulateObjectListOnTestClass(updatedTestObj);

            updatedTestObj.ListOfObjectProperty.RemoveAt(1);

            testObj.ListOfObjectProperty.Count.ShouldNotEqual(updatedTestObj.ListOfObjectProperty.Count);

            var deltaResult = ObjectDelta.GenerateDelta(testObj, updatedTestObj);

            var revertedObj = ObjectDelta.PatchObject(updatedTestObj, deltaResult.OldValues);

            testObj.ListOfObjectProperty.Count.ShouldEqual(revertedObj.ListOfObjectProperty.Count);
        }

        public void AbleToEditObjectInListThenRevertViaPatch()
        {
            var testObj = GetSimpleTestObject();
            PopulateObjectListOnTestClass(testObj);

            var updatedTestObj = GetSimpleTestObject();
            PopulateObjectListOnTestClass(updatedTestObj);

            updatedTestObj.ListOfObjectProperty[2].IntProperty = 30;
            updatedTestObj.ListOfObjectProperty[2].StringProperty = "this is an update to the last object in the list";
            updatedTestObj.ListOfObjectProperty[2].DoubleProperty = 33.333;

            var deltaResult = ObjectDelta.GenerateDelta(testObj, updatedTestObj);

            var revertedObj = ObjectDelta.PatchObject(updatedTestObj, deltaResult.OldValues);

            testObj.ListOfObjectProperty[2].IntProperty.ShouldEqual(revertedObj.ListOfObjectProperty[2].IntProperty);
            testObj.ListOfObjectProperty[2].StringProperty.ShouldEqual(revertedObj.ListOfObjectProperty[2].StringProperty);
            testObj.ListOfObjectProperty[2].DoubleProperty.ShouldEqual(revertedObj.ListOfObjectProperty[2].DoubleProperty);
        }

        public void AbleToAddAndDeleteObjectListItemThenApplyViaPatch()
        {
            var testObj = GetSimpleTestObject();
            PopulateObjectListOnTestClass(testObj);
            PopulateStringListOnTestClass(testObj);

            var updatedTestObj = GetSimpleTestObject();
            PopulateObjectListOnTestClass(updatedTestObj);
            PopulateStringListOnTestClass(updatedTestObj);

            updatedTestObj.ListOfStringProperty.RemoveAt(1);
            updatedTestObj.ListOfStringProperty.Remove("list");
            updatedTestObj.ListOfObjectProperty.RemoveAt(1);
            updatedTestObj.ListOfObjectProperty.Add(new TestClass { StringProperty = "added" });

            var delta = ObjectDelta.GenerateDelta(testObj, updatedTestObj);

            var objToUpdate = GetSimpleTestObject();
            PopulateObjectListOnTestClass(objToUpdate);
            PopulateStringListOnTestClass(objToUpdate);

            var updatedObj = ObjectDelta.PatchObject(objToUpdate, delta);

            updatedTestObj.ListOfObjectProperty.Count.ShouldEqual(updatedObj.ListOfObjectProperty.Count);
            updatedTestObj.ListOfStringProperty.Count.ShouldEqual(updatedObj.ListOfStringProperty.Count);

            updatedObj.ListOfStringProperty.Skip(1).First().ShouldEqual("a");
            updatedObj.ListOfStringProperty.ShouldNotContain("list");
            var addedListItem = updatedObj.ListOfObjectProperty.SingleOrDefault(obj => obj != null && obj.StringProperty == "added");

            addedListItem.ShouldNotBeNull();
        }

        public void AbleToAddObjectListItemThenApplyViaPatch()
        {
            var testObj = GetSimpleTestObject();
            PopulateObjectListOnTestClass(testObj);

            var updatedTestObj = GetSimpleTestObject();
            PopulateObjectListOnTestClass(updatedTestObj);

            updatedTestObj.ListOfObjectProperty.Add(new TestClass { StringProperty = "added" });

            var diff = ObjectDelta.GenerateDelta(testObj, updatedTestObj);

            var updatePatch = JsonConvert.SerializeObject(diff.NewValues);

            var objToUpdate = GetSimpleTestObject();
            PopulateObjectListOnTestClass(objToUpdate);

            var updatedObj = ObjectDelta.PatchObject(objToUpdate, updatePatch);

            updatedTestObj.ListOfObjectProperty.Count.ShouldEqual(updatedObj.ListOfObjectProperty.Count);

            var addedListItem = updatedObj.ListOfObjectProperty.SingleOrDefault(obj => obj != null && obj.StringProperty == "added");

            addedListItem.ShouldNotBeNull();
        }

        private static TestClass GetSimpleTestObject()
        {
            return new TestClass
            {
                StringProperty = "this is a string",
                IntProperty = 1234,
                DoubleProperty = 56.789
            };
        }

        private static void PopulateStringListOnTestClass(TestClass testObject)
        {
            testObject.ListOfStringProperty = new List<string>
            {
                "this", "is", "a", "list", "of", "strings"
            };
        }

        private static void PopulateObjectListOnTestClass(TestClass testObject)
        {
            testObject.ListOfObjectProperty = new List<TestClass>
            {
                new TestClass
                {
                    StringProperty = "this is the first object",
                    IntProperty = 1,
                    DoubleProperty = 1.01
                },
                new TestClass
                {
                    StringProperty = "this is the second object",
                    IntProperty = 2,
                    DoubleProperty = 2.02
                },
                new TestClass
                {
                    StringProperty = "this is the third object",
                    IntProperty = 3,
                    DoubleProperty = 3.03
                }
            };
        }
    }
}