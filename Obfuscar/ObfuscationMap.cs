#region Copyright (c) 2007 Ryan Williams <drcforbin@gmail.com>

/// <copyright>
/// Copyright (c) 2007 Ryan Williams <drcforbin@gmail.com>
/// 
/// Permission is hereby granted, free of charge, to any person obtaining a copy
/// of this software and associated documentation files (the "Software"), to deal
/// in the Software without restriction, including without limitation the rights
/// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the Software is
/// furnished to do so, subject to the following conditions:
/// 
/// The above copyright notice and this permission notice shall be included in
/// all copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
/// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
/// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
/// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
/// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
/// THE SOFTWARE.
/// </copyright>

#endregion

using System;
using System.Collections.Generic;
using System.Linq;

namespace Obfuscar;

internal enum ObfuscationStatus
{
    Unknown,
    WillRename,
    Renamed,
    Skipped
}

internal class ObfuscatedThing(string name)
{
    public string Name { get; } = name;

    public void Update(ObfuscationStatus status, string statusText)
    {
        Status = status;
        StatusText = statusText;
    }

    public ObfuscationStatus Status { get; set; } = ObfuscationStatus.Unknown;

    public string StatusText { get; set; }

    public virtual string ObfuscatedFullName => StatusText;

    public override string ToString()
    {
        return Name + " " + Status + " " + (StatusText ?? "");
    }
}

internal class ObfuscatedClass(string name) : ObfuscatedThing(name)
{
    public Dictionary<MethodKey, ObfuscatedThing> Methods { get; } = new();
    public Dictionary<FieldKey, ObfuscatedThing> Fields { get; } = new();
    public Dictionary<PropertyKey, ObfuscatedThing> Properties { get; } = new();
    public Dictionary<EventKey, ObfuscatedThing> Events { get; } = new();

    public string ObfuscatedNamespace =>
        Status != ObfuscationStatus.Renamed ?
            null :
            StatusText[(StatusText.IndexOf(']') + 1)..StatusText.IndexOf("::", StringComparison.Ordinal)];

    public string ObfuscatedName =>
        Status != ObfuscationStatus.Renamed ?
            null :
            StatusText[(StatusText.IndexOf("::", StringComparison.Ordinal) + 2)..];

    public override string ObfuscatedFullName =>
        Status != ObfuscationStatus.Renamed ?
            null :
            ObfuscatedNamespace + '.' + ObfuscatedName;

    public ObfuscatedThing GetObfuscatedProperty(string name) =>
        Status != ObfuscationStatus.Renamed ?
            null :
            Properties.FirstOrDefault(p => p.Key.Name == name).Value is
                { Status: ObfuscationStatus.Renamed } obfuscatedProperty ?
                obfuscatedProperty :
                null;
}

internal class ObfuscationMap
{
    public Dictionary<TypeKey, ObfuscatedClass> ClassMap { get; } = new();

    public List<ObfuscatedThing> Resources { get; } = [];

    public ObfuscatedClass GetClass(TypeKey key)
    {
        ObfuscatedClass c;

        if (!ClassMap.TryGetValue(key, out c))
        {
            c = new ObfuscatedClass(key.ToString());
            ClassMap[key] = c;
        }

        return c;
    }

    public ObfuscatedThing GetField(FieldKey key)
    {
        ObfuscatedClass c = GetClass(key.TypeKey);

        ObfuscatedThing t;
        if (!c.Fields.TryGetValue(key, out t))
        {
            t = new ObfuscatedThing(key.ToString());
            c.Fields[key] = t;
        }

        return t;
    }

    public ObfuscatedThing GetMethod(MethodKey key)
    {
        ObfuscatedClass c = GetClass(key.TypeKey);

        ObfuscatedThing t;
        if (!c.Methods.TryGetValue(key, out t))
        {
            t = new ObfuscatedThing(key.ToString());
            c.Methods[key] = t;
        }

        return t;
    }

    public ObfuscatedThing GetProperty(PropertyKey key)
    {
        ObfuscatedClass c = GetClass(key.TypeKey);

        ObfuscatedThing t;
        if (!c.Properties.TryGetValue(key, out t))
        {
            t = new ObfuscatedThing(key.ToString());
            c.Properties[key] = t;
        }

        return t;
    }

    public ObfuscatedThing GetEvent(EventKey key)
    {
        ObfuscatedClass c = GetClass(key.TypeKey);

        ObfuscatedThing t;
        if (!c.Events.TryGetValue(key, out t))
        {
            t = new ObfuscatedThing(key.ToString());
            c.Events[key] = t;
        }

        return t;
    }

    public void UpdateType(TypeKey key, ObfuscationStatus status, string text)
    {
        ObfuscatedClass c = GetClass(key);

        c.Update(status, text);
    }

    public void UpdateField(FieldKey key, ObfuscationStatus status, string text)
    {
        ObfuscatedThing f = GetField(key);

        f.Update(status, text);
    }

    public void UpdateMethod(MethodKey key, ObfuscationStatus status, string text)
    {
        ObfuscatedThing m = GetMethod(key);

        m.Update(status, text);
    }

    public void UpdateProperty(PropertyKey key, ObfuscationStatus status, string text)
    {
        ObfuscatedThing m = GetProperty(key);

        m.Update(status, text);
    }

    public void UpdateEvent(EventKey key, ObfuscationStatus status, string text)
    {
        ObfuscatedThing m = GetEvent(key);

        m.Update(status, text);
    }

    public void AddResource(string name, ObfuscationStatus status, string text)
    {
        ObfuscatedThing r = new ObfuscatedThing(name);

        r.Update(status, text);

        Resources.Add(r);
    }

    public IEnumerable<Tuple<TypeKey, string>> FindClasses(string name)
    {
        foreach (var kvp in ClassMap)
        {
            if (kvp.Value.Status == ObfuscationStatus.Renamed)
            {
                if (kvp.Value.StatusText.EndsWith(name, StringComparison.Ordinal))
                {
                    yield return new Tuple<TypeKey, string>(kvp.Key, kvp.Value.StatusText);
                }
            }
        }
    }
}
