using System;
using System.Collections.Generic;
using System.Reflection;

namespace SheepyCreations.EventBus
{
    /// <summary>
    /// Finds every concrete, fully-instantiable type across all loaded assemblies that
    /// implements a given interface. Used to auto-discover IEvent types without requiring
    /// manual registration, and works regardless of whether types live in Assembly-CSharp
    /// or in a custom Assembly Definition.
    /// </summary>
    public static class AssemblyTypeFinder
    {
        public static List<Type> GetTypes(Type interfaceType)
        {
            if (interfaceType == null)
            {
                throw new ArgumentNullException(nameof(interfaceType));
            }

            List<Type> types = new List<Type>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] assemblyTypes;
                try
                {
                    assemblyTypes = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    // Some assemblies fail to fully load every type (missing references,
                    // editor-only dependencies). Use whatever types did load instead of
                    // skipping the whole assembly.
                    assemblyTypes = Array.FindAll(exception.Types, type => type != null);
                }

                foreach (Type type in assemblyTypes)
                {
                    if (type == interfaceType || !interfaceType.IsAssignableFrom(type))
                    {
                        continue;
                    }

                    // Open generics (e.g. EntryClickedEvent<>) can't be used to build an
                    // EventBus<T>; they get their bus lazily once closed (EntryClickedEvent<Foo>).
                    if (type.IsGenericTypeDefinition || type.ContainsGenericParameters)
                    {
                        continue;
                    }

                    if (type.IsAbstract || type.IsInterface)
                    {
                        continue;
                    }

                    types.Add(type);
                }
            }

            return types;
        }
    }
}