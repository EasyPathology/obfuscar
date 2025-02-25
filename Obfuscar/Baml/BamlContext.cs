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

using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Obfuscar;

namespace ICSharpCode.BamlDecompiler.Baml;

internal class BamlContext
{
    public KnownThings KnownThings { get; }

    private readonly List<AssemblyInfo> assemblyList;

    private readonly Dictionary<ushort, AssemblyDefinition> assemblyMap = new();
    private readonly Dictionary<ushort, TypeReference> typeMap = new();
    private readonly Dictionary<ushort, PropertyDefinition> propertyMap = new();

    private readonly Dictionary<ushort, AssemblyInfoRecord> assemblyIdMap = new();
    private readonly Dictionary<ushort, AttributeInfoRecord> attributeIdMap = new();
    private readonly Dictionary<ushort, StringInfoRecord> stringIdMap = new();
    private readonly Dictionary<ushort, TypeInfoRecord> typeIdMap = new();

    private BamlContext(Project project)
    {
        assemblyList = project.AssemblyList;
        KnownThings = new KnownThings(project.AllAssemblySearchPaths);
    }

    public static BamlContext ConstructContext(Project project, BamlDocument document)
    {
        var ctx = new BamlContext(project);

        foreach (var record in document)
        {
            switch (record)
            {
                case AssemblyInfoRecord assemblyInfo:
                {
                    if (assemblyInfo.AssemblyId == ctx.assemblyIdMap.Count)
                        ctx.assemblyIdMap.Add(assemblyInfo.AssemblyId, assemblyInfo);
                    break;
                }
                case AttributeInfoRecord attrInfo:
                {
                    if (attrInfo.AttributeId == ctx.attributeIdMap.Count)
                        ctx.attributeIdMap.Add(attrInfo.AttributeId, attrInfo);
                    break;
                }
                case StringInfoRecord strInfo:
                {
                    if (strInfo.StringId == ctx.stringIdMap.Count)
                        ctx.stringIdMap.Add(strInfo.StringId, strInfo);
                    break;
                }
                case TypeInfoRecord typeInfo:
                {
                    if (typeInfo.TypeId == ctx.typeIdMap.Count)
                        ctx.typeIdMap.Add(typeInfo.TypeId, typeInfo);
                    break;
                }
            }
        }

        return ctx;
    }

    public AssemblyDefinition ResolveAssembly(ushort id)
    {
        id &= 0xfff;
        if (assemblyMap.TryGetValue(id, out var assembly)) return assembly;
        if (assemblyIdMap.TryGetValue(id, out var assemblyRec))
        {
            var assemblyName = AssemblyNameReference.Parse(assemblyRec.AssemblyFullName);
            if (assemblyList.FirstOrDefault(a => a.Name == assemblyName.Name) is { } assemblyInfo) assembly = assemblyInfo.Definition;
            else assembly = assemblyList[^1].Definition.MainModule.AssemblyResolver.Resolve(assemblyName);
        }

        assemblyMap[id] = assembly;
        return assembly;
    }

    public TypeReference ResolveType(ushort id)
    {
        if (typeMap.TryGetValue(id, out var type))
            return type;

        if (id > 0x7fff)
        {
            type = KnownThings.Types((KnownTypes)(short)-unchecked((short)id));
        }
        else
        {
            var typeRec = typeIdMap[id];
            var assembly = ResolveAssembly(typeRec.AssemblyId);
            type = assembly.MainModule.GetType(typeRec.TypeFullName) ??  // cache may not be up to date so try to resolve it
                assembly.MainModule.GetAllTypes().First(t => t.FullName == typeRec.TypeFullName);
        }

        return typeMap[id] = type;
    }

    public PropertyDefinition ResolveProperty(ushort id)
    {
        if (propertyMap.TryGetValue(id, out var property))
            return property;

        if (id > 0x7fff)
        {
            var knownProp = KnownThings.Members((KnownMembers)unchecked((short)-(short)id));
            // type = ResolveType(unchecked((ushort)(short)-(short)knownProp.Parent));
            // name = knownProp.Name;
            property = knownProp.Property;
        }
        else
        {
            var attrRec = attributeIdMap[id];
            var type = ResolveType(attrRec.OwnerTypeId).Resolve();
            property = type.Properties.FirstOrDefault(p => p.Name == attrRec.Name);
            if (property == null &&
                type.Fields.FirstOrDefault(f => f.Name == $"{attrRec.Name}Property") != null &&
                type.Methods.FirstOrDefault(m => m.Name == $"Get{attrRec.Name}") is { } getter &&
                type.Methods.FirstOrDefault(m => m.Name == $"Set{attrRec.Name}") is { } setter)
            {
                property = new PropertyDefinition(attrRec.Name, PropertyAttributes.SpecialName, getter.ReturnType);
            }
        }

        return propertyMap[id] = property;
    }

    public string ResolveString(ushort id)
    {
        if (id > 0x7fff)
            return KnownThings.Strings(unchecked((short)-id));
        if (stringIdMap.TryGetValue(id, out var value))
            return value.Value;
        return null;
    }
}
