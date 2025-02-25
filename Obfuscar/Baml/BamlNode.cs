using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;

namespace ICSharpCode.BamlDecompiler.Baml;

internal class BamlNode
{
    public BamlRecordType Type => Record.Type;
    public virtual BamlRecord Record { get; protected init; }
    public BamlBlockNode Parent { get; init; }
    public BamlNode ConstructorParameter { get; protected set; }
    public MemberReference BamlType { get; protected set; }
    public object BamlValue { get; protected set; }
    public Dictionary<string, BamlNode> Properties { get; } = new();

    public static BamlBlockNode Parse(BamlContext ctx, BamlDocument document)
    {
        Debug.Assert(document.Count > 0 && document[0].Type == BamlRecordType.DocumentStart);

        BamlBlockNode current = null;
        var stack = new Stack<BamlBlockNode>();

        foreach (var record in document)
        {
            if (IsHeader(record))
            {
                var previous = current;

                current = new BamlBlockNode
                {
                    Header = record,
                    Parent = previous
                };

                if (previous != null)
                {
                    previous.children.Add(current);
                    stack.Push(previous);
                }
            }
            else if (IsFooter(record))
            {
                if (current == null) throw new Exception("Unexpected footer.");

                while (!IsMatch(current.Header, record))
                {
                    // End record can be omitted (sometimes).
                    if (stack.Count > 0)
                        current = stack.Pop();
                }

                current.Footer = record;
                current.Build(ctx);

                if (stack.Count > 0) current = stack.Pop();
            }
            else
            {
                if (current == null) throw new Exception("Unexpected record.");

                current.children.Add(
                    new BamlNode
                    {
                        Record = record,
                        Parent = current
                    });
            }
        }

        Debug.Assert(stack.Count == 0);
        return current;

        static bool IsHeader(BamlRecord rec) => rec.Type is BamlRecordType.ConstructorParametersStart or BamlRecordType.DocumentStart or
            BamlRecordType.ElementStart or
            BamlRecordType.KeyElementStart or BamlRecordType.NamedElementStart or BamlRecordType.PropertyArrayStart or
            BamlRecordType.PropertyComplexStart or BamlRecordType.PropertyDictionaryStart or BamlRecordType.PropertyListStart or
            BamlRecordType.StaticResourceStart;

        static bool IsFooter(BamlRecord rec) => rec.Type is BamlRecordType.ConstructorParametersEnd or BamlRecordType.DocumentEnd or
            BamlRecordType.ElementEnd or
            BamlRecordType.KeyElementEnd or BamlRecordType.PropertyArrayEnd or BamlRecordType.PropertyComplexEnd or
            BamlRecordType.PropertyDictionaryEnd or BamlRecordType.PropertyListEnd or BamlRecordType.StaticResourceEnd;

        static bool IsMatch(BamlRecord header, BamlRecord footer) => header.Type switch
        {
            BamlRecordType.ConstructorParametersStart => footer.Type == BamlRecordType.ConstructorParametersEnd,
            BamlRecordType.DocumentStart => footer.Type == BamlRecordType.DocumentEnd,
            BamlRecordType.KeyElementStart => footer.Type == BamlRecordType.KeyElementEnd,
            BamlRecordType.PropertyArrayStart => footer.Type == BamlRecordType.PropertyArrayEnd,
            BamlRecordType.PropertyComplexStart => footer.Type == BamlRecordType.PropertyComplexEnd,
            BamlRecordType.PropertyDictionaryStart => footer.Type == BamlRecordType.PropertyDictionaryEnd,
            BamlRecordType.PropertyListStart => footer.Type == BamlRecordType.PropertyListEnd,
            BamlRecordType.StaticResourceStart => footer.Type == BamlRecordType.StaticResourceEnd,
            BamlRecordType.ElementStart or BamlRecordType.NamedElementStart => footer.Type == BamlRecordType.ElementEnd,
            _ => false
        };
    }

    internal virtual void Build(BamlContext ctx)
    {
        switch (Record)
        {
            case PropertyWithConverterRecord propertyWithConverterRecord:
            {
                // e.g.
                // {Binding State, Converter={StaticResource Equality2VisibilityConverter}}
                BamlType = ctx.ResolveProperty(propertyWithConverterRecord.AttributeId);
                BamlValue = propertyWithConverterRecord.Value;  // todo
                break;
            }
            case PropertyWithExtensionRecord propertyWithExtensionRecord:
            {
                // e.g.
                // <Setter Property="Background" Value="{DynamicResource {x:Static ct:Avatar.BackgroundBrushKey}}" />
                BamlType = ctx.ResolveProperty(propertyWithExtensionRecord.AttributeId);
                var extTypeId = (short)propertyWithExtensionRecord.Flags & 0xfff;
                var valTypeExt = ((short)propertyWithExtensionRecord.Flags & 0x4000) == 0x4000;
                var valStaticExt = ((short)propertyWithExtensionRecord.Flags & 0x2000) == 0x2000;

                switch ((KnownTypes)extTypeId)
                {
                    case KnownTypes.TypeExtension when valTypeExt:
                    {
                        BamlValue = ctx.ResolveType(propertyWithExtensionRecord.ValueId);
                        break;
                    }
                    case KnownTypes.TemplateBindingExtension:
                    {
                        // e.g. BorderBrush="{TemplateBinding Control.BorderBrush}"
                        BamlValue = ctx.ResolveProperty(propertyWithExtensionRecord.ValueId);
                        break;
                    }
                    case KnownTypes.StaticExtension when valStaticExt && propertyWithExtensionRecord.ValueId > 0x7fff:
                    {
                        var isKey = true;
                        var bamlId = unchecked((short)-propertyWithExtensionRecord.ValueId);
                        switch (bamlId)
                        {
                            case > 232 and < 464:
                                bamlId -= 232;
                                isKey = false;
                                break;
                            case > 464 and < 467:
                                bamlId -= 231;
                                break;
                            case > 467 and < 470:
                                bamlId -= 234;
                                isKey = false;
                                break;
                        }

                        var res = ctx.KnownThings.Resources(bamlId);
                        BamlValue = isKey ? res.Item1 + "." + res.Item2 : res.Item1 + "." + res.Item3;
                        break;
                    }
                    case KnownTypes.StaticExtension when valStaticExt:
                    {
                        // e.g. Command="{x:Static p:SettingsPage.BrowseFileCommand}"
                        BamlValue = ctx.ResolveProperty(propertyWithExtensionRecord.ValueId);
                        break;
                    }
                    default:
                    {
                        BamlValue = ctx.ResolveString(propertyWithExtensionRecord.ValueId);
                        break;
                    }
                }

                // var extValue = ext.ToString(ctx, parent.Xaml);
                // var attr = new XAttribute(xamlProp.ToXName(ctx, parent.Xaml, xamlProp.IsAttachedTo(elemType)), extValue);
                break;
            }
        }
    }
}

internal class BamlBlockNode : BamlNode
{
    public BamlRecord Header { get; init; }
    public BamlRecord Footer { get; set; }
    public override BamlRecord Record
    {
        get => Header;
        protected init => Header = value;
    }

    public IEnumerable<BamlBlockNode> Children => children.OfType<BamlBlockNode>();

    internal readonly List<BamlNode> children = [];

    internal override void Build(BamlContext ctx)
    {
        switch (Record)
        {
            case ElementStartRecord elementStartRecord:
            {
                BamlType = ctx.ResolveType(elementStartRecord.TypeId);
                break;
            }
            case ContentPropertyRecord contentPropertyRecord:
            {
                // e.g.
                // ControlTemplate.VisualTree
                // ct:ClippingBorder.Child
                BamlType = ctx.ResolveProperty(contentPropertyRecord.AttributeId);
                break;
            }
            case PropertyComplexStartRecord propertyComplexStartRecord:
            {
                BamlType = ctx.ResolveProperty(propertyComplexStartRecord.AttributeId);
                break;
            }
        }

        for (var i = 0; i >= 0 && i < children.Count; i++)
        {
            var node = children[i];
            switch (node.Record)
            {
                case ConstructorParametersStartRecord:
                {
                    Debug.Assert(((BamlBlockNode)node).children.Count == 1);
                    ConstructorParameter = ((BamlBlockNode)node).children[0];
                    children.RemoveAt(i);
                    i--;
                    break;
                }
                case AttributeInfoRecord attributeInfoRecord:
                {
                    Properties[attributeInfoRecord.Name] = children[i + 1];
                    children.RemoveAt(i + 1);
                    children.RemoveAt(i);
                    i -= 2;
                    break;
                }
            }

            node.Build(ctx);
        }

        ConstructorParameter?.Build(ctx);

        foreach (var property in Properties.Values)
        {
            property.Build(ctx);
        }
    }
}
