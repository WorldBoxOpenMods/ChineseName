using System;
using System.Collections.Generic;

namespace Chinese_Name;

public sealed class NameGenerationContext
{
    public string Source;
    public NameGeneratorAsset VanillaAsset;
    public ChineseNameGeneratorAsset ChineseAsset;
    public Actor Actor;
    public Kingdom Kingdom;
    public Item Item;
    public EquipmentAsset EquipmentAsset;
    public ItemModAsset ItemModAsset;

    public NameGenerationContext Fork()
    {
        return new NameGenerationContext
        {
            Source = Source,
            VanillaAsset = VanillaAsset,
            ChineseAsset = ChineseAsset,
            Actor = Actor,
            Kingdom = Kingdom,
            Item = Item,
            EquipmentAsset = EquipmentAsset,
            ItemModAsset = ItemModAsset
        };
    }
}

public static class NameGenerationContextScope
{
    [ThreadStatic]
    private static Stack<NameGenerationContext> _stack;

    public static NameGenerationContext Current => _stack is { Count: > 0 } ? _stack.Peek() : null;

    public static IDisposable Push(NameGenerationContext context)
    {
        _stack ??= new Stack<NameGenerationContext>(4);
        context ??= new NameGenerationContext();
        _stack.Push(context);
        return new Scope(context);
    }

    private static void Pop(NameGenerationContext context)
    {
        if (_stack is not { Count: > 0 })
        {
            return;
        }

        if (ReferenceEquals(_stack.Peek(), context))
        {
            _stack.Pop();
        }
    }

    private sealed class Scope : IDisposable
    {
        private NameGenerationContext _context;

        public Scope(NameGenerationContext context)
        {
            _context = context;
        }

        public void Dispose()
        {
            if (_context == null)
            {
                return;
            }

            Pop(_context);
            _context = null;
        }
    }
}
