#region Copyright (c) 2007 Ryan Williams <drcforbin@gmail.com>

// <copyright>
// Copyright (c) 2007 Ryan Williams <drcforbin@gmail.com>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// </copyright>

#endregion

using System;
using Mono.Cecil;
using Obfuscar.Helpers;

namespace Obfuscar;

internal class TypeKey : IComparable<TypeKey>
{
    private readonly int hashCode;
    private readonly TypeReference typeReference;

    public TypeKey(TypeReference type)
    {
        typeReference = type;
        Scope = type.GetScopeName();

        Name = string.IsNullOrEmpty(type.Namespace) ? type.Name : type.Namespace + "." + type.Name;
        var declaringType = type;
        // Build path to nested type
        while (declaringType.DeclaringType != null)
        {
            declaringType = declaringType.DeclaringType;
            Name = $"{declaringType.Name}/{Name}";
        }
        Namespace = declaringType.Namespace;

        Fullname = !string.IsNullOrEmpty(Namespace) && Namespace != type.Namespace ? Namespace + "." + Name : Name;

        // Our name should be the same as the Cecil's name. This is important to the Match method.
        var gi = type as GenericInstanceType;
        type.DeclaringType = type.DeclaringType; // Hack: Update fullname of nested type
        if (Fullname != type.ToString() && (gi == null || Fullname != gi.ElementType.FullName))
            throw new InvalidOperationException($"Type names do not match: \"{Fullname}\" != \"{type}\"");

        hashCode = CalcHashCode();
    }

    public TypeKey(string scope, string ns, string name)
        : this(scope, ns, name, ns + "." + name)
    {
    }

    public TypeKey(string scope, string ns, string name, string fullname)
    {
        Scope = scope;
        Namespace = ns;
        Name = name;
        Fullname = fullname;

        hashCode = CalcHashCode();
    }

    public TypeDefinition TypeDefinition => typeReference as TypeDefinition;

    public string Scope { get; }

    public string Namespace { get; }

    public string Name { get; }

    public string Fullname { get; }

    public int CompareTo(TypeKey other)
    {
        // no need to check ns and name...should be in fullname
        var cmp = string.Compare(Scope, other.Scope);
        if (cmp == 0)
            cmp = string.Compare(Fullname, other.Fullname);
        return cmp;
    }

    private int CalcHashCode()
    {
        return Scope.GetHashCode() ^ Namespace.GetHashCode() ^ Name.GetHashCode() ^ Fullname.GetHashCode();
    }

    public bool Matches(TypeReference type)
    {
        // Remove generic type parameters and compare full names
        var instanceType = type as GenericInstanceType;
        if (instanceType == null)
            type.DeclaringType = type.DeclaringType; // Hack: Update full name
        var typeFullName = type.ToString();
        if (instanceType != null)
            typeFullName = instanceType.ElementType.ToString();
        return typeFullName == Fullname;
    }

    public bool Equals(TypeKey other)
    {
        return other != null &&
            hashCode == other.hashCode &&
            Scope == other.Scope &&
            Namespace == other.Namespace &&
            Name == other.Name &&
            Fullname == other.Fullname;
    }

    public override bool Equals(object obj)
    {
        return obj is TypeKey other && Equals(other);
    }

    public static bool operator ==(TypeKey a, TypeKey b)
    {
        if ((object)a == null)
            return (object)b == null;
        if ((object)b == null)
            return false;
        return a.Equals(b);
    }

    public static bool operator !=(TypeKey a, TypeKey b)
    {
        if ((object)a == null)
            return (object)b != null;
        if ((object)b == null)
            return true;
        return !a.Equals(b);
    }

    public override int GetHashCode()
    {
        return hashCode;
    }

    public override string ToString()
    {
        return $"[{Scope}]{Fullname}";
    }
}
