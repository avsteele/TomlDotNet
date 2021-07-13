using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections;

using Tomlet.Models;

namespace TomlDotNet
{
    public static class Serialize
    {
        /// <summary>
        /// See RecordToTomlString. Simply writes that toml string to a file.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <param name="filename"></param>
        public static void ToToml<T>(T data, string filename) where T : notnull
        {
            var str = ToToml(data);
            System.IO.File.WriteAllText(filename, str);
        }

        /// <summary>
        /// Converts a CLR record (class) object into a TOML string.
        /// Method: It looks for a constructor on type T.  It uses the The parameter names in 
        /// the constructors to dtermine the values added to the TOML. This works for for
        /// default-made record constructors e.g.: 'public record Data(int I, string S, ...);'
        /// It works for other types on the condition that each constructor parameter name 
        /// corresponds to a properties on the object to be serialized
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string ToToml<T>(T data) where T : notnull
        {
            var table = ToToml(data as object);
            return table.SerializeNonInlineTable(null, false);
        }        

        private static TomlTable ToToml(object data)
        {
            var type = data.GetType();
            if (data is null) throw new ArgumentNullException(nameof(data));
            var tt = new Tomlet.Models.TomlTable();

            var bindingFlags = BindingFlags.Public | BindingFlags.Instance;
            foreach (var p in type.GetProperties(bindingFlags))
            {
                if (p.Name is null) throw new InvalidOperationException($"Property name {p.Name} is null?");
                var obj = p.GetValue(data);
                if (obj is null) throw new InvalidOperationException($"No property {p.Name} not found on type {type}");
                // Property exists, but if it is marked non-serialize, we ignore it 
                if (IsNonSerialized(type, p)) continue;
                var (publicSet, isInitOnly) = CanSet(p);
                if (!publicSet) continue;
                var tomlValue = ToTomlBase(obj, p.PropertyType);
                tt.PutValue(p.Name, tomlValue);
            }

            foreach(var f in type.GetFields(bindingFlags))
            {
                if (f.IsNotSerialized) continue;
                object? obj = f.GetValue(data);
                if (obj is null) throw new InvalidOperationException($"value of field {f.Name} was null");
                var tomlValue = ToTomlBase(obj, f.FieldType);
                tt.PutValue(f.Name, tomlValue);
            }

            return tt;
        }

        /// <summary>
        /// returns whther the property has a public setter and whether that public setter is init-only.
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static (bool publicSet, bool isInitOnly) CanSet(PropertyInfo p)
        {
            //if (!p.CanWrite) return (false, false);
            MethodInfo? setMethod = p.SetMethod;
            if (setMethod is null) return (false, false);
            // Get the modifiers applied to the return parameter.
            var setMethodReturnParameterModifiers = setMethod.ReturnParameter.GetRequiredCustomModifiers();
            // Init-only properties are marked with the IsExternalInit type.
            return (true, setMethodReturnParameterModifiers.Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)));
        }

        public static bool IsNonSerialized(Type t, ParameterInfo p)
            => IsNonSerializedBase(t, p);

        public static bool IsNonSerialized(Type t, PropertyInfo p)
            => IsNonSerializedBase(t, p);

        public static bool IsNonSerialized(FieldInfo f)
        {
            if ((from a in f.CustomAttributes where a.AttributeType == typeof(NonSerializedAttribute) select 1).Any()) return true;
            return false;
        }

        public static bool IsNonSerializedBase(Type t, object p)
        {
            if (p is not ParameterInfo pi && p is not PropertyInfo) throw new ArgumentException(nameof(p), "Must be PropertyInfo or ParameterInfo");
            string name = (p as ParameterInfo)?.Name ?? (p as MemberInfo)?.Name!;
            string fieldName = $"<{name}>k__BackingField";
            /// might need ot search base clases one by one since 
            /// non-pubic field are not able to be obtianed any other way
            FieldInfo? f = null;
            Type? currentType = t;
            while (f is null)
            {
                f = currentType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (f is null) currentType = currentType.BaseType;
                if (currentType is null) throw new InvalidOperationException($"Field {name} on type {t} (or its bases) not found");
            }
            // get the backing field
            return IsNonSerialized(f);
        }

        private static TomlValue ToTomlBase(object data, Type targetType) => data switch
        {
            bool b => TomlBoolean.ValueOf(b),
            double _ or float _ => new TomlDouble(Convert.ToDouble(data)),
            long _ or int _ or uint _ or short _ or ushort _ or byte _ => new TomlLong(Convert.ToInt64(data)),
            string s => new TomlString(s),
            DateTime dt => new TomlLocalDateTime(dt),
            DateTimeOffset dto => new TomlOffsetDateTime(dto),
            IEnumerable e => MakeTomlArray(e),
            _ => ToToml(data)
        };

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        /// <param name="targetType">use only in the case that we have an array of records</param>
        /// <returns></returns>
        private static TomlArray MakeTomlArray(IEnumerable e)
        {
            if (e.GetType().GenericTypeArguments.Length == 0) 
                throw new InvalidOperationException("Unable to convert IEnumerable to tomlarray, missing generic type");

            var listType = typeof(List<>);
            var elementType = e.GetType().GenericTypeArguments[0]; // for heterog this might be 'object'
            //var constructedListType = listType.MakeGenericType(elementType);

            List<TomlValue> tomlValues = (from object el in e select ToTomlBase(el, elementType)).ToList();
            var a = new TomlArray();
            
            var p = typeof(TomlArray).GetField("ArrayValues");
            p?.SetValue(a, tomlValues);  // jenky, but I can't find any other way to corectly construct a TomlArray

            if(tomlValues.Count>0 && tomlValues[0] is TomlTable)
            {
                var pIsTable = typeof(TomlArray).GetField("IsTableArray", BindingFlags.NonPublic| BindingFlags.Instance);
                pIsTable?.SetValue(a, true);
            }

            return a;
        }

        /// <summary>
        /// Returns the object returned by src.propName's 'getter'
        /// </summary>
        /// <param name="src"></param>
        /// <param name="propName"></param>
        /// <returns></returns>
        public static object? GetPropValue(object src, string propName)
        {
            return src.GetType().GetProperty(propName)?.GetValue(src, null);
        }

    }
}
