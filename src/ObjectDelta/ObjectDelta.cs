using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ObjectDelta
{
    public static class ObjectDelta
    {
        private const string PREFIX_ARRAY_SIZE = "@@ Count";
        private const string PREFIX_REMOVED_FIELDS = "@@ Removed";

        private static readonly JsonSerializer JsonSerializer = GetJsonSerializer();

        public static ObjectDeltaResult GenerateDelta<T>(T original, T updated) where T : class
        {
            var writer = JsonSerializer;
            JObject originalJson, updatedJson;
            if (typeof(JObject).IsAssignableFrom(typeof(T)))
            {
                originalJson = original as JObject;
                updatedJson = updated as JObject;
            }
            else
            {
                originalJson = original != null ? JObject.FromObject(original, writer) : null;
                updatedJson = updated != null ? JObject.FromObject(updated, writer) : null;
            }
            var result = Delta(originalJson, updatedJson);
            result.Type = typeof(T);
            return result;
        }

        public static T PatchObject<T>(T source, string deltaJson) where T : class
        {
            var deltaObject = JObject.Parse(deltaJson);
            return PatchObject(source, deltaObject);
        }

        public static T PatchObject<T>(T source, JObject deltaJson) where T : class
        {
            var sourceJson = source != null ? JObject.FromObject(source, JsonSerializer) : null;
            var resultJson = Patch(sourceJson, deltaJson);

            return resultJson?.ToObject<T>();
        }

        public static T PatchObject<T>(T source, ObjectDeltaResult deltaResult) where T : class
        {
            var patched = PatchObject(source, deltaResult.OldValues);
            return PatchObject(patched, deltaResult.NewValues);
        }

        private static ObjectDeltaResult Delta(JObject source, JObject target)
        {
            var result = new ObjectDeltaResult();
            // check for null values
            if (source == null && target == null)
            {
                return result;
            }
            if (source == null || target == null)
            {
                result.OldValues = source;
                result.NewValues = target;
                return result;
            }

            // compare internal fields           
            var removedNew = new JArray();
            var removedOld = new JArray();
            JToken token;
            // start by iterating in source fields
            foreach (var i in source)
            {
                // check if field exists
                if (!target.TryGetValue(i.Key, out token))
                {
                    AddOldValuesToken(result, i.Value, i.Key);
                    removedNew.Add(i.Key);
                }
                // compare field values
                else
                {
                    DeltaField(i.Key, i.Value, token, result);
                }
            }
            // then iterate in target fields that are not present in source
            foreach (var i in target)
            {
                // ignore alredy compared values
                if (source.TryGetValue(i.Key, out token))
                    continue;
                // add missing tokens
                removedOld.Add(i.Key);
                AddNewValuesToken(result, i.Value, i.Key);
            }

            if (removedOld.Count > 0)
                AddOldValuesToken(result, removedOld, PREFIX_REMOVED_FIELDS);
            if (removedNew.Count > 0)
                AddNewValuesToken(result, removedNew, PREFIX_REMOVED_FIELDS);

            return result;
        }

        private static void DeltaField(string fieldName, JToken source, JToken target, ObjectDeltaResult result = null)
        {
            if (result == null)
                result = new ObjectDeltaResult();
            if (source == null)
            {
                if (target != null)
                {
                    AddToken(result, fieldName, source, target);
                }
            }
            else if (target == null)
            {
                AddToken(result, fieldName, source, target);
            }
            else switch (source.Type)
            {
                case JTokenType.Object:
                    var v = target as JObject;
                    var r = Delta(source as JObject, v);
                    if (!r.AreEqual)
                        AddToken(result, fieldName, r);
                    break;
                case JTokenType.Array:
                    var aS = source as JArray;
                    var aT = target as JArray;

                    if ((aS.Count == 0 || aT.Count == 0) && (aS.Count != aT.Count))
                    {
                        AddToken(result, fieldName, source, target);
                    }
                    else
                    {
                        var arrayDelta = new ObjectDeltaResult();
                        var minCount = Math.Min(aS.Count, aT.Count);
                        for (var i = 0; i < Math.Max(aS.Count, aT.Count); i++)
                        {
                            if (i < minCount)
                            {
                                DeltaField(i.ToString(), aS[i], aT[i], arrayDelta);
                            }
                            else if (i >= aS.Count)
                            {
                                AddNewValuesToken(arrayDelta, aT[i], i.ToString());
                            }
                            else
                            {
                                AddOldValuesToken(arrayDelta, aS[i], i.ToString());
                            }
                        }

                        if (arrayDelta.AreEqual) return;
                        if (aS.Count != aT.Count)
                            AddToken(arrayDelta, PREFIX_ARRAY_SIZE, aS.Count, aT.Count);
                        AddToken(result, fieldName, arrayDelta);
                    }
                    break;
                default:
                    if (!JToken.DeepEquals(source, target))
                    {
                        AddToken(result, fieldName, source, target);
                    }
                    break;
            }
        }

        private static JsonSerializer GetJsonSerializer()
        {
            // ensure the serializer will not ignore null values
            var settings = JsonConvert.DefaultSettings != null ? JsonConvert.DefaultSettings() : new JsonSerializerSettings();
            settings.NullValueHandling = NullValueHandling.Include;
            settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            settings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
            settings.Formatting = Formatting.None;
            settings.MissingMemberHandling = MissingMemberHandling.Ignore;
            settings.ObjectCreationHandling = ObjectCreationHandling.Replace;

            // create our custom serializer
            var writer = JsonSerializer.Create(settings);
            return writer;
        }

        private static JToken Patch(JToken sourceJson, JToken deltaJson)
        {
            JToken token;
            // deal with null values
            if (sourceJson == null || deltaJson == null || !sourceJson.HasValues)
            {
                return deltaJson;
            }
            if (deltaJson.Type != JTokenType.Object)
            {
                return deltaJson;
            }
            // deal with objects
            var deltaObj = (JObject)deltaJson;
            if (sourceJson.Type == JTokenType.Array)
            {
                var sz = 0;
                var foundArraySize = deltaObj.TryGetValue(PREFIX_ARRAY_SIZE, out token);
                if (foundArraySize)
                {
                    deltaObj.Remove(PREFIX_ARRAY_SIZE);
                    sz = token.Value<int>();
                }
                var array = sourceJson as JArray;
                // resize array
                if (foundArraySize && array.Count != sz)
                {
                    var snapshot = array.DeepClone() as JArray;
                    array.Clear();
                    for (var i = 0; i < sz; i++)
                    {
                        array.Add(i < snapshot.Count ? snapshot[i] : null);
                    }
                }
                // patch it
                foreach (var f in deltaObj)
                {
                    int ix;
                    if (int.TryParse(f.Key, out ix))
                    {
                        array[ix] = Patch(array[ix], f.Value);
                    }
                }
            }
            else
            {
                var sourceObj = sourceJson as JObject ?? new JObject();
                // remove fields
                if (deltaObj.TryGetValue(PREFIX_REMOVED_FIELDS, out token))
                {
                    deltaObj.Remove(PREFIX_REMOVED_FIELDS);
                    foreach (var f in token as JArray)
                        sourceObj.Remove(f.ToString());
                }

                // patch it
                foreach (var f in deltaObj)
                {
                    sourceObj[f.Key] = Patch(sourceObj[f.Key], f.Value);
                }
            }
            return sourceJson;
        }

        private static void AddNewValuesToken(ObjectDeltaResult item, JToken newToken, string fieldName)
        {
            if (item.NewValues == null)
                item.NewValues = new JObject();
            item.NewValues[fieldName] = newToken;
        }

        private static void AddOldValuesToken(ObjectDeltaResult item, JToken oldToken, string fieldName)
        {
            if (item.OldValues == null)
                item.OldValues = new JObject();
            item.OldValues[fieldName] = oldToken;
        }

        private static void AddToken(ObjectDeltaResult item, string fieldName, JToken oldToken, JToken newToken)
        {
            AddOldValuesToken(item, oldToken, fieldName);

            AddNewValuesToken(item, newToken, fieldName);
        }

        private static void AddToken(ObjectDeltaResult item, string fieldName, ObjectDeltaResult delta)
        {
            AddToken(item, fieldName, delta.OldValues, delta.NewValues);
        }
    }
}