using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

namespace VirtualVoid.Networking
{
   public static class AssemblyUtil
    {
        public static List<MethodInfo> GetAllMethodsWithAttribute(Type attribType)
        {
            List<MethodInfo> allMethods = new List<MethodInfo>();

            foreach (Assembly assembly in GetAssemblies())
            {
                MethodInfo[] methods = assembly.GetTypes()
                        .SelectMany(t => t.GetMethods())
                        .Where(m => m.GetCustomAttributes(attribType, false).Length > 0)
                        .ToArray();

                allMethods.AddRange(methods);
            }

            return allMethods;
        }

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
