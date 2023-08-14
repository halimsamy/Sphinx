using System.Collections.Generic;
using dnlib.DotNet;

namespace Sphinx.SymbolsObfuscation
{
    internal class SymbolsObfuscationService
    {
        private static readonly object CanRenameKey = new object();
        private static readonly object ReferencesKey = new object();

        private readonly Dictionary<IMemberDef, string> _nameMap = new Dictionary<IMemberDef, string>();
        private readonly Dictionary<string, string> _nameMap1 = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _nameMap2 = new Dictionary<string, string>();

        public bool CanRename(object def)
        {
            return Context.Annotations.Get(def, CanRenameKey, true);
        }

        public void SetCanRename(object def, bool val)
        {
            Context.Annotations.Set(def, CanRenameKey, val);
        }

        public void AddReference<T>(T obj, INameReference<T> reference)
        {
            Context.Annotations.GetOrCreate(obj, ReferencesKey, key => new List<INameReference>()).Add(reference);
        }

        public IList<INameReference> GetReferences(object obj)
        {
            return Context.Annotations.GetLazy(obj, ReferencesKey, key => new List<INameReference>());
        }

        public string ObfuscateName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            string newName = null;
            name = this.ParseGenericName(name, out var count);

            if (this._nameMap1.ContainsKey(name))
                return this.MakeGenericName(this._nameMap1[name], count);

            //newName = Extensions.RandomString();
            newName = "_" + name.Replace(".", "_");

            this._nameMap1[name] = newName;
            this._nameMap2[newName] = name;

            return this.MakeGenericName(newName, count);
        }

        public string GetOriginalName(string obfuscatedName)
        {
            return this._nameMap2.GetValueOrDefault(obfuscatedName, obfuscatedName);
        }

        public string GetObfuscatedName(string originalName)
        {
            return this._nameMap1.GetValueOrDefault(originalName, originalName);
        }

        public ICollection<KeyValuePair<string, string>> GetNameMap()
        {
            return this._nameMap2;
        }

        private string ParseGenericName(string name, out int? count)
        {
            if (name.LastIndexOf('`') != -1)
            {
                var index = name.LastIndexOf('`');
                if (int.TryParse(name.Substring(index + 1), out var c))
                {
                    count = c;
                    return name.Substring(0, index);
                }
            }

            count = null;
            return name;
        }

        private string MakeGenericName(string name, int? count)
        {
            return count == null ? name : $"{name}`{count.Value}";
        }
    }
}