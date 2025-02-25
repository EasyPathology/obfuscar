/*
    Copyright (c) 2015 Ki

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Obfuscar;

namespace ICSharpCode.BamlDecompiler.Baml;

internal partial class KnownThings
{
    private readonly BamlAssemblyResolver bamlAssemblyResolver;
    private readonly Dictionary<int, AssemblyDefinition> assemblies = new();
    private readonly Dictionary<KnownMembers, KnownMember> members = new();
    private readonly Dictionary<KnownTypes, TypeDefinition> types = new();
    private readonly Dictionary<int, string> strings = new();
    private readonly Dictionary<int, (string, string, string)> resources = new();

    private class BamlAssemblyResolver(IEnumerable<string> assemblySearchPaths) : IAssemblyResolver
    {
        private readonly List<string> assemblySearchPaths = assemblySearchPaths.ToList();
        private Dictionary<string, AssemblyDefinition> cache = new();

        public void Dispose() { }

        public AssemblyDefinition Resolve(AssemblyNameReference name) => Resolve(name, new ReaderParameters
        {
            AssemblyResolver = this
        });

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            if (cache.TryGetValue(name.Name, out var assembly)) return assembly;

            AssemblyDefinition assemblyCandidate = null;
            foreach (var path in assemblySearchPaths)
            {
                try
                {
                    var assemblyPath = System.IO.Path.Combine(path, name.Name + ".dll");
                    if (!System.IO.File.Exists(assemblyPath)) continue;
                    assembly = AssemblyDefinition.ReadAssembly(assemblyPath, parameters);
                    if (assemblyCandidate == null ||
                        assemblyCandidate.Name.Version < assembly.Name.Version)
                    {
                        assemblyCandidate = assembly;
                    }
                }
                catch
                {
                    // ignore
                }
            }

            if (assemblyCandidate != null) cache[name.Name] = assemblyCandidate;
            return assemblyCandidate;
        }
    }

    public KnownThings(IEnumerable<string> assemblySearchPaths)
    {
        bamlAssemblyResolver = new BamlAssemblyResolver(assemblySearchPaths);
        InitAssemblies();
        InitTypes();
        InitMembers();
        InitStrings();
        InitResources();
    }

    public Func<KnownTypes, TypeDefinition> Types => id => types[id];
    public Func<KnownMembers, KnownMember> Members => id => members[id];
    public Func<short, string> Strings => id => strings[id];
    public Func<short, (string, string, string)> Resources => id => resources[id];

    private AssemblyDefinition ResolveAssembly(string name) =>
        bamlAssemblyResolver.Resolve(AssemblyNameReference.Parse(name));

    private static TypeDefinition InitType(AssemblyDefinition assembly, string ns, string name)
    {
        if (assembly.MainModule.GetType(ns, name) is { } typeDefinition) return typeDefinition;
        if (assembly.MainModule.TryGetTypeReference($"{ns}.{name}", out var typeReference)) return typeReference.Resolve();
        // then resolve type forwards
        if (assembly.MainModule.ExportedTypes.FirstOrDefault(t => t.Name == name) is { } exportedType) return exportedType.Resolve();
        throw new Exception($"Type {ns}.{name} not found in {assembly.FullName}");
    }

    private KnownMember InitMember(KnownTypes parent, string name, TypeDefinition type) =>
        new(parent, types[parent], name, type);
}

internal class KnownMember
{
    public KnownMember(KnownTypes parent, TypeDefinition declType, string name, TypeDefinition type)
    {
        Parent = parent;
        Property = declType.Properties.SingleOrDefault(p => p.Name == name);
        DeclaringType = declType;
        Name = name;
        Type = type;
    }

    public KnownTypes Parent { get; }
    public TypeDefinition DeclaringType { get; }
    public PropertyDefinition Property { get; }
    public string Name { get; }
    public TypeDefinition Type { get; }
}
