using System;
using System.Collections.Generic;
using System.Reflection;

namespace Lumera.ZRay
{
    // 등록된 치트 정의를 모아 둔다. 정적 상태가 없어서 호스트(또는 검사)가 인스턴스를 만들어 소유한다.
    // id는 대소문자를 무시해서 비교한다. 같은 id는 먼저 등록한 것만 남고 나중 것은 거부하며 이유를 알린다.
    public sealed class CheatRegistry
    {
        private static readonly StringComparer ID_COMPARER = StringComparer.OrdinalIgnoreCase;

        private readonly Dictionary<string, CheatDefinition> _byId = new Dictionary<string, CheatDefinition>(ID_COMPARER);
        private readonly List<CheatDefinition> _sorted = new List<CheatDefinition>();
        private readonly CheatScanner _scanner;
        private readonly Action<string> _reportProblem;
        private bool _bSortedDirty;

        public ValueTypeRegistry ValueTypes { get; }
        // 정의가 더해지거나 빠질 때마다 오른다. 화면은 이 값이 바뀌었을 때만 목록을 다시 만든다.
        public int Version { get; private set; }

        // 분류, 순서, 이름 순으로 정렬한 모든 정의.
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

        // reportProblemOrNull: 잘못된 선언과 중복 id를 알릴 곳(Unity에서는 Debug.LogWarning).
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

        // 이 라이브러리를 참조하는 어셈블리만 골라 정적 치트를 모두 등록한다. 엔진·시스템 어셈블리는 건너뛴다.
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

        // 한 타입의 정적 치트만 등록한다. 검사와, 어셈블리를 훑지 않고 명시적으로 등록하고 싶을 때 쓴다.
        public void RegisterStatic(Type type)
        {
            List<CheatDefinition> definitions = new List<CheatDefinition>();
            _scanner.Scan(type, null, definitions);
            AddAll(definitions);
        }

        // 객체의 인스턴스 치트를 등록한다. 돌려받은 핸들을 Dispose하면 이번에 등록한 것만 빠진다.
        // 판이나 씬이 바뀌면 이전 핸들을 Dispose하고 새 대상을 다시 등록한다.
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
                // 같은 id로 다른 정의가 들어와 있으면 그것은 건드리지 않는다.
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

        // 일부 타입을 불러오지 못하는 어셈블리도 나머지 타입은 훑는다.
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
