using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

namespace VirtualVoid.Networking
{
    static class AssemblyUtil
    {
        //public static List<Assembly> GetAssemblies()
        //{
        //    var returnAssemblies = new List<Assembly>();
        //    var loadedAssemblies = new HashSet<string>();
        //    var assembliesToCheck = new Queue<Assembly>();
        //
        //    assembliesToCheck.Enqueue(Assembly.GetExecutingAssembly());
        //
        //    while (assembliesToCheck.Any())
        //    {
        //        var assemblyToCheck = assembliesToCheck.Dequeue();
        //
        //        foreach (var reference in assemblyToCheck.GetReferencedAssemblies()
        //            .Where(x => !x.Name.StartsWith("Microsoft.") && !x.Name.StartsWith("System.") && !x.Name.StartsWith("UnityEngine.")))
        //        {
        //
        //            if (!loadedAssemblies.Contains(reference.FullName))
        //            {
        //                var assembly = Assembly.Load(reference);
        //                assembliesToCheck.Enqueue(assembly);
        //                loadedAssemblies.Add(reference.FullName);
        //                returnAssemblies.Add(assembly);
        //            }
        //        }
        //    }
        //
        //    return returnAssemblies;
        //}

        public static List<Assembly> GetAssemblies()
        {
            var assemblies = new List<Assembly>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.StartsWith("Mono.Cecil"))
                    continue;

                if (assembly.FullName.StartsWith("UnityScript"))
                    continue;

                if (assembly.FullName.StartsWith("Boo.Lan"))
                    continue;

                if (assembly.FullName.StartsWith("System"))
                    continue;

                if (assembly.FullName.StartsWith("I18N"))
                    continue;

                if (assembly.FullName.StartsWith("UnityEngine"))
                    continue;

                if (assembly.FullName.StartsWith("UnityEditor"))
                    continue;

                if (assembly.FullName.StartsWith("mscorlib"))
                    continue;

                assemblies.Add(assembly);
            }

            return assemblies;
        }
    }
}
