/* XNA4 Specification Mismatch Test Program for FNA
 * Written by Nick Gravelyn
 * http://www.brushfiregames.com/
 *
 * Updates by Ethan "flibitijibibo" Lee
 * http://www.flibitijibibo.com/
 *
 * Released under public domain.
 * No warranty implied; use at your own risk.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace XNA4SpecTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Results results = new Results();

            // Gather up all the XNA types from all redist assemblies. We're not looking at content pipeline stuff.
            var xnaTypes = GetAllTypes(
                typeof(Microsoft.Xna.Framework.Vector2).Assembly, // Microsoft.Xna.Framework.dll
                // lol: typeof(Microsoft.Xna.Framework.GamerServices.AvatarDescription).Assembly, // Microsoft.Xna.Framework.Avatar.dll
                typeof(Microsoft.Xna.Framework.Game).Assembly, // Microsoft.Xna.Framework.Game.dll
                // MonoGame.Net: typeof(Microsoft.Xna.Framework.GamerServices.GamerServicesDispatcher).Assembly, // Microsoft.Xna.Framework.GamerServices.dll
                typeof(Microsoft.Xna.Framework.Graphics.GraphicsDevice).Assembly, // Microsoft.Xna.Framework.Graphics.dll
                // TODO: typeof(Microsoft.Xna.Framework.Input.Touch.TouchCollection).Assembly, // Microsoft.Xna.Framework.Input.Touch.dll
                // MonoGame.Net: typeof(Microsoft.Xna.Framework.Net.PacketReader).Assembly, // Microsoft.Xna.Framework.Net.dll
                typeof(Microsoft.Xna.Framework.Storage.StorageContainer).Assembly, // Microsoft.Xna.Framework.Storage.dll
                typeof(Microsoft.Xna.Framework.Media.Video).Assembly, // Microsoft.Xna.Framework.Video.dll
                typeof(Microsoft.Xna.Framework.Audio.WaveBank).Assembly // Microsoft.Xna.Framework.Xact.dll
            );

            // Gather up the FNA types.
            // Assuming here that you've got MonoGame.Framework.dll, SDL2-CS.dll, and TheoraPlay-CS.dll in the output directory.
            var fnaTypes = GetAllTypes(Assembly.LoadFrom("MonoGame.Framework.dll"));

            // Figure out if there are any types in XNA that aren't in FNA
            results.TypesNotInFNA.AddRange(from p in xnaTypes
                                           where !fnaTypes.ContainsKey(p.Key)
                                           orderby p.Key
                                           select p.Value.FullName);

            // Figure out if there are any types in FNA that aren't in XNA
            results.TypesExtraInFNA.AddRange(from p in fnaTypes
                                             where !xnaTypes.ContainsKey(p.Key)
                                             orderby p.Key
                                             select p.Value.FullName);

            // Get the types that are in both APIs for further comparison
            var matchedTypes = from p in xnaTypes
                               where fnaTypes.ContainsKey(p.Key)
                               orderby p.Key
                               select new { XNAType = p.Value, FNAType = fnaTypes[p.Key] };

            // Perform per-type comparisons to look for method/field/property equivalence.
            foreach (var pair in matchedTypes)
            {
                TypeResults typeResults = new TypeResults { TypeName = pair.XNAType.FullName };
                CompareTypes(pair.XNAType, pair.FNAType, typeResults);

                if (!typeResults.IsEmpty())
                {
                    results.TypeComparisons.Add(typeResults);
                }
            }

            using (var writer = new StreamWriter("SpecMismatches.txt"))
            {
                if (results.TypesNotInFNA.Count > 0)
                {
                    writer.WriteLine("Types Not In FNA:");
                    foreach (var t in results.TypesNotInFNA)
                    {
                        writer.WriteLine("\t{0}", t);
                    }
                    writer.WriteLine();
                }

                if (results.TypesExtraInFNA.Count > 0)
                {
                    writer.WriteLine("Types Extra In FNA:");
                    foreach (var t in results.TypesExtraInFNA)
                    {
                        writer.WriteLine("\t{0}", t);
                    }
                    writer.WriteLine();
                }

                if (results.TypeComparisons.Count > 0)
                {
                    writer.WriteLine("Type Comparisons:");
                    foreach (var t in results.TypeComparisons)
                    {
                        writer.WriteLine("\t{0}", t.TypeName);
                        if (t.FieldsNotInFNA.Count > 0)
                        {
                            writer.WriteLine("\t\tFields Not In FNA:");
                            foreach (var f in t.FieldsNotInFNA)
                            {
                                writer.WriteLine("\t\t\t{0}", f);
                            }
                        }
                        if (t.FieldsExtraInFNA.Count > 0)
                        {
                            writer.WriteLine("\t\tFields Extra In FNA:");
                            foreach (var f in t.FieldsExtraInFNA)
                            {
                                writer.WriteLine("\t\t\t{0}", f);
                            }
                        }
                        if (t.PropertiesNotInFNA.Count > 0)
                        {
                            writer.WriteLine("\t\tProperties Not In FNA:");
                            foreach (var p in t.PropertiesNotInFNA)
                            {
                                writer.WriteLine("\t\t\t{0}", p);
                            }
                        }
                        if (t.PropertiesExtraInFNA.Count > 0)
                        {
                            writer.WriteLine("\t\tProperties Extra In FNA:");
                            foreach (var p in t.PropertiesExtraInFNA)
                            {
                                writer.WriteLine("\t\t\t{0}", p);
                            }
                        }
                        if (t.EventsNotInFNA.Count > 0)
                        {
                            writer.WriteLine("\t\tEvents Not In FNA:");
                            foreach (var e in t.EventsNotInFNA)
                            {
                                writer.WriteLine("\t\t\t{0}", e);
                            }
                        }
                        if (t.EventsExtraInFNA.Count > 0)
                        {
                            writer.WriteLine("\t\tEvents Extra In FNA:");
                            foreach (var e in t.EventsExtraInFNA)
                            {
                                writer.WriteLine("\t\t\t{0}", e);
                            }
                        }
                        if (t.MethodsNotInFNA.Count > 0)
                        {
                            writer.WriteLine("\t\tMethods Not In FNA:");
                            foreach (var m in t.MethodsNotInFNA)
                            {
                                writer.WriteLine("\t\t\t{0}", m);
                            }
                        }
                        if (t.MethodsExtraInFNA.Count > 0)
                        {
                            writer.WriteLine("\t\tMethods Extra In FNA:");
                            foreach (var m in t.MethodsExtraInFNA)
                            {
                                writer.WriteLine("\t\t\t{0}", m);
                            }
                        }
                        writer.WriteLine();
                    }
                }
            }
        }

        // flibit added this to get around mismatches based on assembly name.
        private static string CleanString(string fullName)
        {
            if (fullName == null)
            {
                return null;
            }
            string[] assemblyIndicators = new string[]
            {
                ", Microsoft.Xna.Framework",
                ", MonoGame.Framework"
            };
            foreach (string indicator in assemblyIndicators)
            {
                if (fullName.Contains(indicator))
                {
                    return fullName.Substring(
                        0,
                        fullName.LastIndexOf(indicator)
                    );
                }
            }
            return fullName;
        }

        private static void CompareTypes(Type xnaType, Type fnaType, TypeResults results)
        {
            var xnaFields = xnaType.GetFields();
            var fnaFields = fnaType.GetFields();
            results.FieldsNotInFNA.AddRange(from f in xnaFields
                                            where AreFieldsDifferent(f, fnaFields.FirstOrDefault(f2 => f2.Name == f.Name))
                                            orderby f.Name
                                            select f.GetSignature());
            results.FieldsExtraInFNA.AddRange(from f in fnaFields
                                              where AreFieldsDifferent(f, xnaFields.FirstOrDefault(f2 => f2.Name == f.Name))
                                              orderby f.Name
                                              select f.GetSignature());

            var xnaProperties = xnaType.GetProperties();
            var fnaProperties = fnaType.GetProperties();
            results.PropertiesNotInFNA.AddRange(from p in xnaProperties
                                                where ArePropertiesDifferent(p, fnaProperties.FirstOrDefault(p2 => p2.Name == p.Name))
                                                orderby p.Name
                                                select p.GetSignature());
            results.PropertiesExtraInFNA.AddRange(from p in fnaProperties
                                                  where ArePropertiesDifferent(p, xnaProperties.FirstOrDefault(p2 => p2.Name == p.Name))
                                                  orderby p.Name
                                                  select p.GetSignature());

            var xnaEvents = xnaType.GetEvents();
            var fnaEvents = fnaType.GetEvents();
            results.EventsNotInFNA.AddRange(from e in xnaEvents
                                            where AreEventsDifferent(e, fnaEvents.FirstOrDefault(e2 => e2.Name == e.Name))
                                            orderby e.Name
                                            select e.GetSignature());
            results.EventsExtraInFNA.AddRange(from e in fnaEvents
                                              where AreEventsDifferent(e, xnaEvents.FirstOrDefault(e2 => e2.Name == e.Name))
                                              orderby e.Name
                                              select e.GetSignature());

            var xnaMethods = xnaType.GetMethods().Where(m => !m.IsSpecialName);
            var fnaMethods = fnaType.GetMethods().Where(m => !m.IsSpecialName);
            results.MethodsNotInFNA.AddRange(from m in xnaMethods
                                             where AreMethodsDifferent(m, fnaMethods.FirstOrDefault(m2 => !AreMethodsDifferent(m, m2)))
                                             orderby m.Name
                                             select m.GetSignature());
            results.MethodsExtraInFNA.AddRange(from m in fnaMethods
                                               where AreMethodsDifferent(m, xnaMethods.FirstOrDefault(m2 => !AreMethodsDifferent(m, m2)))
                                               orderby m.Name
                                               select m.GetSignature());
        }

        private static bool AreFieldsDifferent(FieldInfo field1, FieldInfo field2)
        {
            if ((field1 == null && field2 != null) || (field1 != null && field2 == null))
            {
                return true;
            }

            if (field1.Name != field2.Name)
            {
                return true;
            }

            // Compare type names because XNA types won't match FNA types but their names should
            if (CleanString(field1.FieldType.FullName) != CleanString(field2.FieldType.FullName))
            {
                return true;
            }

            if (field1.IsStatic != field2.IsStatic)
            {
                return true;
            }

            return false;
        }

        private static bool ArePropertiesDifferent(PropertyInfo prop1, PropertyInfo prop2)
        {
            if ((prop1 == null && prop2 != null) || (prop1 != null && prop2 == null))
            {
                return true;
            }

            if (prop1.Name != prop2.Name)
            {
                return true;
            }

            // FIXME: WTF...? -flibit
            if (prop1.Name == "Item" && prop2.Name == "Item")
            {
                return false;
            }

            // Compare type names because XNA types won't match FNA types but their names should
            if (CleanString(prop1.PropertyType.FullName) != CleanString(prop2.PropertyType.FullName))
            {
                return true;
            }

            if (AreMethodsDifferent(prop1.GetGetMethod(), prop2.GetGetMethod()))
            {
                return true;
            }

            if (AreMethodsDifferent(prop1.GetSetMethod(), prop2.GetSetMethod()))
            {
                return true;
            }

            return false;
        }

        private static bool AreEventsDifferent(EventInfo evt1, EventInfo evt2)
        {
            if ((evt1 == null && evt2 != null) || (evt1 != null && evt2 == null))
            {
                return true;
            }

            if (evt1.Name != evt2.Name)
            {
                return true;
            }

            // Compare type names because XNA types won't match FNA types but their names should
            if (CleanString(evt1.EventHandlerType.FullName) != CleanString(evt2.EventHandlerType.FullName))
            {
                return true;
            }

            if (evt1.GetAddMethod().IsStatic != evt2.GetAddMethod().IsStatic)
            {
                return true;
            }

            return false;
        }

        private static bool AreMethodsDifferent(MethodInfo method1, MethodInfo method2)
        {
            if (method1 == null && method2 == null)
            {
                return false;
            }

            if ((method1 == null && method2 != null) || (method1 != null && method2 == null))
            {
                return true;
            }

            if (method1.Name != method2.Name)
            {
                return true;
            }

            // Compare type names because XNA types won't match FNA types but their names should
            if (CleanString(method1.ReturnType.FullName) != CleanString(method2.ReturnType.FullName))
            {
                return true;
            }

            var params1 = method1.GetParameters();
            var params2 = method2.GetParameters();

            if (params1.Count() != params2.Count())
            {
                return true;
            }

            for (int i = 0; i < params1.Count(); i++)
            {
                var p1 = params1.ElementAt(i);
                var p2 = params2.ElementAt(i);

                if (CleanString(p1.ParameterType.FullName) != CleanString(p2.ParameterType.FullName))
                {
                    return true;
                }

                if (p1.IsRetval != p2.IsRetval)
                {
                    return true;
                }

                if (p1.IsOut != p2.IsOut)
                {
                    return true;
                }

                if (p1.IsOptional != p2.IsOptional)
                {
                    return true;
                }

                // TODO: Should we be pedantic and look at parameter names?
            }

            return false;
        }

        private static Dictionary<string, Type> GetAllTypes(params Assembly[] assemblies)
        {
            var types = new Dictionary<string, Type>();

            foreach (var a in assemblies)
            {
                foreach (var t in a.GetExportedTypes())
                {
                    types[t.FullName] = t;
                }
            }

            return types;
        }
    }

    public static class Extensions
    {
        public static string GetSignature(this FieldInfo field)
        {
            var sig = string.Format("{0} {1}", field.FieldType, field.Name);
            if (field.IsStatic)
            {
                sig = "static " + sig;
            }
            return sig;
        }

        public static string GetSignature(this PropertyInfo property)
        {
            var sig = string.Format("{0} {1} {{", property.PropertyType, property.Name);

            var getter = property.GetGetMethod();
            var setter = property.GetSetMethod();

            if ((getter != null && getter.IsStatic) ||
                (setter != null && setter.IsStatic))
            {
                sig = "static " + sig;
            }

            if (getter != null)
            {
                sig += " get;";
            }
            if (setter != null)
            {
                sig += " set;";
            }

            sig += " }";

            return sig;
        }

        public static string GetSignature(this EventInfo evt)
        {
            var sig = string.Format("{0} {1}", evt.EventHandlerType, evt.Name);

            if (evt.GetAddMethod().IsStatic)
            {
                sig = "static " + sig;
            }

            return sig;
        }

        public static string GetSignature(this MethodInfo method)
        {
            var sig = string.Format("{0} {1}(", method.ReturnType, method.Name);

            if (method.IsStatic)
            {
                sig = "static " + sig;
            }

            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Count(); i++)
            {
                var p = parameters.ElementAt(i);

                var pStr = string.Format("{0} {1}", p.ParameterType, p.Name);

                if (p.IsOut)
                {
                    pStr = "out " + pStr;
                }
                else if (p.IsRetval)
                {
                    pStr = "ref " + pStr;
                }

                sig += pStr;

                if (i < parameters.Count() - 1)
                {
                    sig += ", ";
                }
            }

            sig += ")";

            return sig;
        }
    }

    public class Results
    {
        public List<string> TypesNotInFNA = new List<string>();
        public List<string> TypesExtraInFNA = new List<string>();

        public List<TypeResults> TypeComparisons = new List<TypeResults>();
    }

    public class TypeResults
    {
        public string TypeName;

        public List<string> FieldsNotInFNA = new List<string>();
        public List<string> FieldsExtraInFNA = new List<string>();

        public List<string> PropertiesNotInFNA = new List<string>();
        public List<string> PropertiesExtraInFNA = new List<string>();

        public List<string> EventsNotInFNA = new List<string>();
        public List<string> EventsExtraInFNA = new List<string>();

        public List<string> MethodsNotInFNA = new List<string>();
        public List<string> MethodsExtraInFNA = new List<string>();

        public bool IsEmpty()
        {
            return
                FieldsNotInFNA.Count == 0 &&
                FieldsExtraInFNA.Count == 0 &&
                PropertiesNotInFNA.Count == 0 &&
                PropertiesExtraInFNA.Count == 0 &&
                EventsNotInFNA.Count == 0 &&
                EventsExtraInFNA.Count == 0 &&
                MethodsNotInFNA.Count == 0 &&
                MethodsExtraInFNA.Count == 0;
        }
    }
}
