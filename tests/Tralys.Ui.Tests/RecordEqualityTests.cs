using System.Reflection;

namespace Tralys.Ui.Tests;

public sealed class RecordEqualityTests
{
    [Test]
    public async Task A_generic_records_equatable_fields_stay_valid_types_with_its_first_type_parameter_replaced_by_the_field_type()
    {
        // Mono's wasm AOT loads F[T0 := F] to compare a shared record's IEquatable<F> field.
        var fields = 0;
        var broken = new List<string>();
        var records = new[] { typeof(Ui).Assembly, typeof(TreeRow).Assembly }
            .SelectMany(static a => a.GetTypes())
            .Where(static t =>
                t.IsGenericTypeDefinition
                && t.GetMethod("PrintMembers", BindingFlags.Instance | BindingFlags.NonPublic)
                    is not null
            );

        foreach (var record in records)
        {
            var first = record.GetGenericArguments()[0];
            foreach (
                var field in record.GetFields(
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.DeclaredOnly
                )
            )
            {
                var type = field.FieldType;
                if (
                    !type.IsGenericType
                    || !Mentions(type, first)
                    || !typeof(IEquatable<>).MakeGenericType(type).IsAssignableFrom(type)
                )
                    continue;
                fields++;
                try
                {
                    Replaced(type, first, type);
                }
                catch (ArgumentException)
                {
                    broken.Add($"{record.Name}.{field.Name}");
                }
            }
        }

        await Assert.That(fields).IsGreaterThan(10);
        await Assert.That(broken).IsEmpty();
    }

    static bool Mentions(Type type, Type parameter) =>
        type == parameter
        || (type.IsGenericType && type.GetGenericArguments().Any(a => Mentions(a, parameter)));

    static Type Replaced(Type type, Type parameter, Type with)
    {
        if (type == parameter)
            return with;
        if (!type.IsGenericType)
            return type;
        var arguments = type.GetGenericArguments();
        for (var i = 0; i < arguments.Length; i++)
            arguments[i] = Replaced(arguments[i], parameter, with);
        return type.GetGenericTypeDefinition().MakeGenericType(arguments);
    }
}
