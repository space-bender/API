﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Kavics.ApiExplorer
{
    public class Api
    {
        private ApiType[] _types;
        private Filter _filter;

        private string BinPath { get; set; }

        public Api(string binPath, Filter filter = null)
        {
            BinPath = binPath.Trim('\\');
            this._filter = filter ?? new Filter();
        }

        //private static Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();
        public ApiType[] GetTypes()
        {
            if (_types == null)
            {
                var assemblyPaths = Directory.GetFiles(BinPath, "*.dll").Union(Directory.GetFiles(BinPath, "*.exe")).ToArray();
                if (assemblyPaths.Length == 0)
                    throw new Exception("Source directory does not contain any dll or exe file.");

                //foreach (var path in assemblyPaths)
                //{
                //    var assembly = Assembly.LoadFrom(path);
                //    foreach(var type in assembly.GetTypes())
                //    {
                //        if (type.Name.StartsWith("<") || type.FullName.StartsWith("<"))
                //            continue;
                //        if(_typeCache.ContainsKey(type.FullName))
                //        {
                //            int q = 1;
                //        }
                //        _typeCache.Add(type.FullName, type);
                //    }
                //}
                foreach (var path in assemblyPaths)
                    Assembly.LoadFrom(path);

                var namespaceRegex = _filter.NamespaceFilter;

                var asms = AppDomain.CurrentDomain.GetAssemblies();
                var relevantAsms = asms.Where(a => Path.GetDirectoryName(a.Location) == BinPath).ToArray();
                var types = relevantAsms
                    .SelectMany(a => _filter.WithInternals ? a.GetTypes() : a.GetExportedTypes(), (a, t) => t);

                // skip auto implementations e.g. "<>c__DisplayClass29_0"
                types = types.Where(t => !t.Name.StartsWith("<"));

                if (namespaceRegex != null)
                    types = types.Where(x => namespaceRegex.IsMatch(x.Namespace ?? ""));

                var apiTypes = types
                    .Select(t => new ApiType(t, _filter.WithInternalMembers));

                if (_filter.ContentHandlerFilter)
                    apiTypes = apiTypes.Where(t => t.IsContentHandler);

                apiTypes = apiTypes
                    .OrderBy(a => a.Assembly)
                    .ThenBy(a => a.Namespace)
                    .ThenBy(a => a.Name);

                _types = apiTypes.ToArray();
            }
            return _types;
        }

        public static string GetTypeName(Type type)
        {
            var origType = type;
            var typeInfo = type.GetTypeInfo();
            if (!type.IsGenericType)
            {
                var name = type.Name;
                if (!name.Contains('`'))
                    return GetSimpleName(type.Name);
                if(type.FullName == null)
                    return origType.Name;
                type = Type.GetType(type.FullName.Replace("&", ""));
                if (type == null)
                    return origType.Name;
            }
            var baseName = GetSimpleName(type.Name.Split('`')[0]);
            var genericPart = (type.GenericTypeArguments.Length > 0)
                ? string.Join(", ", type.GenericTypeArguments.Select(GetTypeName).ToArray())
                : string.Join(", ", typeInfo.GenericTypeParameters.Select(GetTypeName).ToArray());
            return $"{baseName}<{genericPart}>";
        }

        public static string GetSimpleName(string name)
        {
            switch (name)
            {
                default: return name;
                case "String": return "string";
                case "SByte": return "sbyte";
                case "Byte": return "byte";
                case "Boolean": return "bool";
                case "Char": return "char";
                case "Int16": return "short";
                case "UInt16": return "ushort";
                case "Int32": return "int";
                case "UInt32": return "uint";
                case "Int64": return "long";
                case "UInt64": return "ulong";
                case "Single": return "float";
                case "Double": return "double";
                case "Decimal": return "decimal";
                case "Void": return "void";
            }
        }

    }
}