using System;
using System.Reflection;
using System.Linq;
using Tomlet.Models;
using System.Collections.Generic;


namespace TomlDotNet
{

    /// https://github.com/SamboyCoding/Tomlet
    /// https://github.com/toml-lang/toml/blob/master/toml.md
    /// https://github.com/paiden/Nett
    //
    /// <summary>
    /// Method facilitation conversion from TOML data to CLR objects.
    /// </summary>
    public static class Deserialize
    {
        static Deserialize()
        {
            // add 'identity' conversions
            BuiltInConversions.Add((typeof(string), typeof(string)), s => s);
            BuiltInConversions.Add((typeof(long), typeof(long)), l => l);
            BuiltInConversions.Add((typeof(bool), typeof(bool)), b => b);
            BuiltInConversions.Add((typeof(double), typeof(double)), d => d);
            BuiltInConversions.Add((typeof(DateTime), typeof(DateTime)), dt => dt);
            BuiltInConversions.Add((typeof(DateTimeOffset), typeof(DateTimeOffset)), dto => dto);

            // these are used for filling elements of hereteogeneous arrays
            BuiltInConversions.Add((typeof(string), typeof(object)), o => o);
            BuiltInConversions.Add((typeof(long), typeof(object)), o => o);
            BuiltInConversions.Add((typeof(bool), typeof(object)), o => o);
            BuiltInConversions.Add((typeof(double), typeof(object)), o => o);
            BuiltInConversions.Add((typeof(DateTime), typeof(object)), o => o);
            BuiltInConversions.Add((typeof(DateTimeOffset), typeof(object)), o => o);

        }

        public static void AddNumericConversions()
        {
            /// these are ok 99% of the time
            Deserialize.Conversions.Add((typeof(long), typeof(ulong)), (i) => Convert.ToUInt64((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(int)), (i) => Convert.ToInt32((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(uint)), (i) => Convert.ToUInt32((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(short)), (i) => Convert.ToInt16((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(ushort)), (i) => Convert.ToUInt16((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(byte)), (i) => Convert.ToByte((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(float)), (i) => Convert.ToSingle((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(double)), (i) => Convert.ToDouble((long)i));
            Deserialize.Conversions.Add((typeof(double), typeof(float)), (d) => Convert.ToSingle((double)d));
        }

        private static Dictionary<(Type from, Type to), Func<object, object>> BuiltInConversions { get; set; } = new();

        public static Dictionary<(Type from, Type to), Func<object, object>> Conversions { get; private set; } = new();

        /// <summary>
        /// Constructs an instance of type T from the contents of the TOML file filePath
        /// </summary>
        /// <typeparam name="T">class constraint required because value types can always be default constructed (they match any toml file.)</typeparam>
        /// <param name="filePath">realtive or absolute path to a TOML file</param>
        /// <returns></returns>
        public static T FromFile<T>(string filePath) where T : class
            => FromString<T>(System.IO.File.ReadAllText(filePath));

        /// <summary>
        /// As FromFile, but the TOML file contents are passed directly as a string
        /// </summary>
        /// <typeparam name="T">See Fromfile</typeparam>
        /// <param name="tomlFileContents"></param>
        /// <returns></returns>
        public static T FromString<T>(string tomlFileContents) where T : class
        {
            var parser = new Tomlet.TomlParser();
            return FromToml<T>(parser.Parse(tomlFileContents));
        }

        public static T FromToml<T>(TomlTable tt) where T : class
            => (T)FromToml(tt, typeof(T));

        private static object FromToml(TomlTable tt, Type t)
        {
            List<Exception> exs = new();
            foreach(var c in ConstructorTryOrder(t,tt))
            {
                try
                { return FromToml(tt, t, c); }
                catch (Exception e) 
                { 
                    exs.Add( new InvalidOperationException($"Construction with {c} failed -> {e.Message}")); 
                }
            }
            throw new AggregateException("No constructors compatible with Toml data found", exs);
        }

        public static object FromToml(TomlTable tt, Type t, ConstructorInfo c)
        {
            var param_list = c.GetParameters();

            var params_ = new object?[param_list.Length];
            foreach (var (v, idx) in param_list.Select((ParameterInfo v, int idx) => (v, idx)))
            {
                params_[idx] = GetObj(tt, v);
            }
            return Convert.ChangeType(c.Invoke(params_), t);
        }

        private static object? GetObj(TomlTable tt, ParameterInfo p)
        {
            if(p.Name is null) throw new ArgumentException($"{nameof(p)}.name must not be null", nameof(p));
            if( tt.ContainsKey(p.Name))
            {
                var value = tt.GetValue(p.Name);
                return FromToml(value, p.ParameterType);
            }
            else
            {
                if (p.IsOptional) return p.DefaultValue;
                throw new InvalidOperationException($"no element in TOML found to match non-optional {p.Name}");
            }
        }

        /// <summary>
        /// Toml value to CLR type 'type'
        /// </summary>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private static object FromToml(TomlValue from, Type toType)
            => from switch
            {
                null => throw new ArgumentNullException(nameof(from)),
                TomlArray a => BuildFromArray(a,toType),
                TomlTable t => FromToml(t, toType),
                // special case for parsing string to enums
                TomlString s when toType.IsEnum => 
                    Enum.TryParse(toType, s.Value, out object? o) switch
                    {
                        true => o!,
                        false => throw new FormatException($"Unable to parse {s.Value} as Enum {toType.Name}")
                    },
                // rest should be singletons, require compatibility with toType or a valid conversion
                _ => DoConversion(ExtractValue(from), toType),
            };

        private static object DoConversion(object o, Type type)
            // try builtIn dict of conversions, then fallback to user defined
            => BuiltInConversions.ContainsKey((from: o.GetType(), to: type))
                ? BuiltInConversions[(from: o.GetType(), to: type)](o)
                : Conversions.ContainsKey((from: o.GetType(), to: type))
                ? Conversions[(from: o.GetType(), to: type)](o)
                : throw new InvalidCastException($"{o.GetType()}->{type}");

        private static object ExtractValue(TomlValue v)
        => v switch
        {
            TomlString s => s.Value,
            TomlLong i => i.Value,
            TomlBoolean b => b.Value,
            TomlDouble d => d.Value,
            TomlLocalDateTime ldt => ldt.Value,
            TomlOffsetDateTime odt => odt.Value,            
            _ => throw new InvalidOperationException("Only defined for single-value types"),
        };

        /// <summary>
        /// Build toType 
        /// </summary>
        /// <param name="fromToml"></param>
        /// <param name="toType"></param>
        /// <returns></returns>
        private static object BuildFromArray(TomlArray fromToml, Type toType)
        {
            // Method 1: Use constructor of some toType's  Name<T>(Iennumerable<T>)

            /// 1) find any constructors on ToType with an single IEnumerable<T> parameter, 
            /// 2) For each try to create an Iennumerable<T> from th TOMl array,
            /// 3) attempt to construct object using the parameter list
            /// 4) use this list as an arg to the constructor from 1) above

            var possibles = PossibleConstructors(toType);
            foreach( var (constructor, eleType) in possibles)
            {
                // try to asemble arg list, if it throws, try next
                try
                {
                    var listType = typeof(List<>);
                    // for heterog this might be 'object'. Will also throw if the class has no generic type arg
                    //var elementType = toType.GenericTypeArguments[0];
                    var constructedListType = listType.MakeGenericType(eleType);

                    IEnumerable<object> prm = from el in fromToml select FromToml(el, eleType);

                    var instance = (System.Collections.IList)Activator.CreateInstance(constructedListType)!;
                    if (instance is null) throw new InvalidOperationException($"Failed to construct List<{eleType}> to construct {toType}");
                    foreach (var p in prm)
                        instance.Add(p); // will throw if instance does not support type of the enumerator
                    return constructor.Invoke(new object[] { instance });
                }
                catch { }
            }


            foreach(var (converter, eleType) in PossibleConverters(toType))
            {
                try
                {
                    var listType = typeof(List<>);
                    // for heterog this might be 'object'. Will also throw if the class has no generic type arg
                    //var elementType = toType.GenericTypeArguments[0];
                    var constructedListType = listType.MakeGenericType(eleType);

                    IEnumerable<object> prm = from el in fromToml select FromToml(el, eleType);

                    var instance = (System.Collections.IList)Activator.CreateInstance(constructedListType)!;
                    if (instance is null) throw new InvalidOperationException($"Failed to construct List<{eleType}> to construct {toType}");
                    foreach (var p in prm)
                        instance.Add(p); // will throw if instance does not support type of the enumerator

                    return converter(instance);
                }
                catch { }
            }
            // now try a converter
            //var f = ConvertFromEnumerable(toType, elementType);
            //if (f is not null) return f(prm);

            throw new InvalidOperationException($"No constructor on type {toType} with 1 Enumerable<T> parameter, and no Conveter from IEnnumerable <T> found");
            /// converter way
            //var conv = ConvertFromEnumerable(toType);
        }

        public static Type? IsIEnumerable(Type t)
            => (t.Name == "IEnumerable`1" && t.GenericTypeArguments.Length == 1) ? t.GenericTypeArguments[0] : null;

        private static List<(ConstructorInfo constructor, Type elementType)> PossibleConstructors(Type t)
        {
            List <(ConstructorInfo constructor, Type elementType)> L = new();
            foreach(var c in t.GetConstructors())
            {
                ParameterInfo[] ps = c.GetParameters();
                if (ps.Length != 1) continue;
                var elementType = IsIEnumerable(ps[0].ParameterType);
                if (elementType is not null) L.Add((constructor: c, elementType));
            }
            return L;
        }

        /// <summary>
        /// Looks in teh conversions list for one that can take an IEnumerable<T> and return toType
        /// </summary>
        /// <param name="toType"></param>
        /// <returns></returns>
        private static List<(Func<object,object> converter, Type elementType)> PossibleConverters(Type toType)
        {
            List<(Func<object, object> converter, Type elementType)> @out = new();
            foreach (var (key, value) in Conversions)
            {
                if (!toType.IsAssignableFrom(key.to)) continue;
                var eleType = IsIEnumerable(key.from);
                if (eleType is null) continue;
                @out.Add((converter:value, elementType:eleType));
            }
            return @out;
        }

        /// <summary>
        /// Filters public, instance constructors of type t on thier number of parameters.  
        /// If a Tomlable is provided it filters other those that do not have 
        /// </summary>
        /// <param name="t"></param>
        /// <param name="tt"></param>
        /// <returns></returns>
        public static List<ConstructorInfo> ConstructorTryOrder(Type t, TomlTable? tt)
        {
            var cs = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ToList();
            // sort on # params
            cs.Sort((c1, c2) => c2.GetParameters().Length.CompareTo(c1.GetParameters().Length));
            if (tt is null) return cs;            
            // next line filter constructors with more required params than keys in the tomltable
            //     this is just a performance enhancement really, so tt is optional
            var l = (from c in cs where NumberRequiredParams(c) <= tt.Keys.Count select c).ToList();
            return l;
        }

        public static int NumberRequiredParams(ConstructorInfo c)
            => (from p in c.GetParameters() where !p.IsOptional select p).Count();
    }
}