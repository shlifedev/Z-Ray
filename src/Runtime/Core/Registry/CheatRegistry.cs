using System;
using System.Collections.Generic;
using System.Reflection;

namespace Lumera.ZRay
{
    public sealed class CheatRegistry
    {
        private static readonly StringComparer ID_COMPARER = StringComparer.OrdinalIgnoreCase;

        private readonly Dictionary<string, CheatDefinition> _byId = new Dictionary<string, CheatDefinition>(ID_COMPARER);
        private readonly List<CheatDefinition> _sorted = new List<CheatDefinition>();
        private readonly CheatScanner _scanner;
        private readonly Action<string> _reportProblem;
        private bool _bSortedDirty;

        public ValueTypeRegistry ValueTypes { get; }
        public int Version { get; private set; }

        public IReadOnlyList<CheatDefinition> Definitions
        {
            get
            {
                if (_bSortedDirty)
                {
                    _sorted.Sort(CompareForMenu);
                    _bSortedDirty = false;
                }

                return _sorted;
            }
        }

        public CheatRegistry(ValueTypeRegistry? valueTypesOrNull = null, Action<string>? reportProblemOrNull = null)
        {
            ValueTypes = valueTypesOrNull ?? new ValueTypeRegistry();
            _reportProblem = reportProblemOrNull ?? (_ => { });
            _scanner = new CheatScanner(ValueTypes, _reportProblem);
        }

        public CheatDefinition? FindOrNull(string id)
        {
            return _byId.TryGetValue(id, out CheatDefinition definition) ? definition : null;
        }

        public void RegisterStatic(IEnumerable<Assembly> assemblies)
        {
            Assembly core = typeof(CheatRegistry).Assembly;
            string coreName = core.GetName().Name;
            List<CheatDefinition> definitions = new List<CheatDefinition>();
            foreach (Assembly assembly in assemblies)
            {
                if (assembly.IsDynamic || (assembly != core && !References(assembly, coreName)))
                {
                    continue;
                }

                foreach (Type type in GetTypes(assembly))
                {
                    _scanner.Scan(type, null, definitions);
                }
            }

            AddAll(definitions);
        }

        public void RegisterStatic(Type type)
        {
            List<CheatDefinition> definitions = new List<CheatDefinition>();
            _scanner.Scan(type, null, definitions);
            AddAll(definitions);
        }

        public IDisposable Register(object target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            List<CheatDefinition> definitions = new List<CheatDefinition>();
            _scanner.Scan(target.GetType(), target, definitions);
            return new Registration(this, AddAll(definitions));
        }

        private List<CheatDefinition> AddAll(List<CheatDefinition> definitions)
        {
            List<CheatDefinition> added = new List<CheatDefinition>(definitions.Count);
            for (int index = 0; index < definitions.Count; index++)
            {
                CheatDefinition definition = definitions[index];
                if (_byId.ContainsKey(definition.Id))
                {
                    _reportProblem($"치트 id '{definition.Id}'가 이미 등록되어 있어 나중 것을 무시한다.");
                    continue;
                }

                _byId.Add(definition.Id, definition);
                _sorted.Add(definition);
                added.Add(definition);
            }

            if (added.Count > 0)
            {
                _bSortedDirty = true;
                Version++;
            }

            return added;
        }

        private void RemoveAll(List<CheatDefinition> definitions)
        {
            bool bRemoved = false;
            for (int index = 0; index < definitions.Count; index++)
            {
                CheatDefinition definition = definitions[index];
                if (_byId.TryGetValue(definition.Id, out CheatDefinition current) && current == definition)
                {
                    _byId.Remove(definition.Id);
                    _sorted.Remove(definition);
                    bRemoved = true;
                }
            }

            if (bRemoved)
            {
                Version++;
            }
        }

        private static bool References(Assembly assembly, string coreName)
        {
            AssemblyName[] references = assembly.GetReferencedAssemblies();
            for (int index = 0; index < references.Length; index++)
            {
                if (references[index].Name == coreName)
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<Type> GetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                List<Type> loaded = new List<Type>();
                foreach (Type? type in exception.Types)
                {
                    if (type != null)
                    {
                        loaded.Add(type);
                    }
                }

                return loaded;
            }
        }

        private static int CompareForMenu(CheatDefinition left, CheatDefinition right)
        {
            int category = string.CompareOrdinal(left.Category, right.Category);
            if (category != 0)
            {
                return category;
            }

            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : ID_COMPARER.Compare(left.Id, right.Id);
        }

        private sealed class Registration : IDisposable
        {
            private CheatRegistry? _registryOrNull;
            private readonly List<CheatDefinition> _definitions;

            public Registration(CheatRegistry registry, List<CheatDefinition> definitions)
            {
                _registryOrNull = registry;
                _definitions = definitions;
            }

            public void Dispose()
            {
                _registryOrNull?.RemoveAll(_definitions);
                _registryOrNull = null;
            }
        }
    }
}
