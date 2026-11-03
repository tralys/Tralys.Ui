namespace Tralys.Ui;

// A callback names its invoker by an id rather than a reference, which with its stamp keeps it 32 bytes.
internal static class Invokers<T>
    where T : class
{
    static readonly Lock Gate = new();
    static T?[] s_items = new T?[8];
    static int s_count = 1;

    public static int Add(T invoker)
    {
        lock (Gate)
        {
            var items = s_items;
            if (s_count == items.Length)
            {
                var grown = new T?[items.Length * 2];
                Array.Copy(items, grown, items.Length);
                grown[s_count] = invoker;
                Volatile.Write(ref s_items, grown);
            }
            else
                items[s_count] = invoker;
            return s_count++;
        }
    }

    [FramePath]
    public static T At(int id) => Volatile.Read(ref s_items)[id]!;
}
