using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Topten.JsonKit
{
    /// <summary>
    /// Helper extensions for navigating dictionaries of string/object
    /// </summary>
    public static class IDictionaryExtensions
    {
        /// <summary>
        /// Navigates a string/object dictionary following a path
        /// </summary>
        /// <param name="This">The root dictionary</param>
        /// <param name="Path">The path to follow</param>
        /// <param name="create">Whether to create nodes as walking the path</param>
        /// <param name="leafCallback">A callback invoked for each lead node.  Return false to stop walk</param>
        /// <returns>Result of last leafCallback, or false if key not found and create parameter is false</returns>
        public static bool WalkPath(
            this IDictionary<string, object> This, 
            string Path, 
            bool create, 
            Func<IDictionary<string,object>,string, bool> leafCallback
            )
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

        /// <summary>
        /// Check if a path exists in an string/object dictionary heirarchy
        /// </summary>
        /// <param name="This">The root dictionary</param>
        /// <param name="Path">The path to follow</param>
        /// <returns>True if the path exists</returns>
        public static bool PathExists(this IDictionary<string, object> This, string Path)
        {
            return This.WalkPath(Path, false, (dict, key) => dict.ContainsKey(key));
        }

        /// <summary>
        /// Gets the object at the specified path in an string/object dictionary heirarchy
        /// </summary>
        /// <param name="This">The root dictionary</param>
        /// <param name="type">The expected returned object type</param>
        /// <param name="Path">The path to follow</param>
        /// <param name="def">The default value if the value isn't found</param>
        /// <returns></returns>
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

        /// <summary>
        /// Gets an object of specified type at a path location in a string/object dictionaryt
        /// </summary>
        /// <typeparam name="T">The returned object type</typeparam>
        /// <param name="This">The root dictionary</param>
        /// <param name="Path">The path of the object to return</param>
        /// <returns>The object instance</returns>
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

        /// <summary>
        /// Get a value at a path in a string/object dictionary heirarchy
        /// </summary>
        /// <typeparam name="T">The type of object to be returned</typeparam>
        /// <param name="This">The root dictionary</param>
        /// <param name="Path">The path of the entry to find</param>
        /// <param name="def">The default value to return if not found</param>
        /// <returns>The located value, or the default value if not found</returns>
        public static T GetPath<T>(this IDictionary<string, object> This, string Path, T def = default(T))
        {
            return (T)This.GetPath(typeof(T), Path, def);
        }

        /// <summary>
        /// Set a value in a string/object dictionary heirarchy
        /// </summary>
        /// <param name="This">The root dictionary</param>
        /// <param name="Path">The path of the value to set</param>
        /// <param name="value">The value to set</param>
        public static void SetPath(this IDictionary<string, object> This, string Path, object value)
        {
            This.WalkPath(Path, true, (dict, key) => { dict[key] = value; return true; });
        }
    }
}
