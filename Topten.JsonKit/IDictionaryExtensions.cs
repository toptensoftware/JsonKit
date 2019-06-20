using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Topten.JsonKit
{
    public static class IDictionaryExtensions
    {
        public static bool WalkPath(this IDictionary<string, object> This, string Path, bool create, Func<IDictionary<string,object>,string, bool> leafCallback)
        {
            // Walk the path
            var parts = Path.Split('.');
            for (int i = 0; i < parts.Length-1; i++)
            {
                object val;
                if (!This.TryGetValue(parts[i], out val))
                {
                    if (!create)
                        return false;

                    val = new Dictionary<string, object>();
                    This[parts[i]] = val;
                }
                This = (IDictionary<string,object>)val;
            }

            // Process the leaf
            return leafCallback(This, parts[parts.Length-1]);
        }

        public static bool PathExists(this IDictionary<string, object> This, string Path)
        {
            return This.WalkPath(Path, false, (dict, key) => dict.ContainsKey(key));
        }

        public static object GetPath(this IDictionary<string, object> This, Type type, string Path, object def)
        {
            This.WalkPath(Path, false, (dict, key) =>
            {
                object val;
                if (dict.TryGetValue(key, out val))
                {
                    if (val == null)
                        def = val;
                    else if (type.IsAssignableFrom(val.GetType()))
                        def = val;
                    else
                        def = Json.Reparse(type, val);
                }
                return true;
            });

            return def;
        }

        // Ensure there's an object of type T at specified path
        public static T GetObjectAtPath<T>(this IDictionary<string, object> This, string Path) where T:class,new()
        {
            T retVal = null;
            This.WalkPath(Path, true, (dict, key) =>
            {
                object val;
                dict.TryGetValue(key, out val);
                retVal = val as T;
                if (retVal == null)
                {
                    retVal = val == null ? new T() : Json.Reparse<T>(val);
                    dict[key] = retVal;
                }
                return true;
            });

            return retVal;
        }

        public static T GetPath<T>(this IDictionary<string, object> This, string Path, T def = default(T))
        {
            return (T)This.GetPath(typeof(T), Path, def);
        }

        public static void SetPath(this IDictionary<string, object> This, string Path, object value)
        {
            This.WalkPath(Path, true, (dict, key) => { dict[key] = value; return true; });
        }
    }
}
