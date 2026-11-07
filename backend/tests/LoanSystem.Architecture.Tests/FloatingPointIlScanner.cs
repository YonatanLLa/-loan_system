using System.Reflection;
using System.Reflection.Emit;

namespace LoanSystem.Architecture.Tests;

/// <summary>
/// Walks method IL to find floating-point usage inside method bodies (locals, float constants,
/// conversions and calls such as Math.Pow), which signature-level checks and NetArchTest miss.
/// </summary>
internal static class FloatingPointIlScanner
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                       BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Type[] FloatingTypes = [typeof(float), typeof(double), typeof(float?), typeof(double?)];

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => o.Value);

    private static readonly HashSet<short> FloatOpCodes =
    [
        OpCodes.Ldc_R4.Value, OpCodes.Ldc_R8.Value, OpCodes.Conv_R4.Value, OpCodes.Conv_R8.Value,
        OpCodes.Conv_R_Un.Value, OpCodes.Ldind_R4.Value, OpCodes.Ldind_R8.Value,
        OpCodes.Ldelem_R4.Value, OpCodes.Ldelem_R8.Value
    ];

    public static IEnumerable<string> FindViolations(Type type)
    {
        var methods = type.GetMethods(Flags).Cast<MethodBase>().Concat(type.GetConstructors(Flags));
        foreach (var method in methods)
        {
            var body = method.GetMethodBody();
            if (body is null) continue;

            if (body.LocalVariables.Any(l => FloatingTypes.Contains(l.LocalType)))
            {
                yield return $"{type.FullName}.{method.Name} (floating-point local)";
                continue;
            }

            var reason = ScanIl(method, body.GetILAsByteArray() ?? []);
            if (reason is not null)
                yield return $"{type.FullName}.{method.Name} ({reason})";
        }
    }

    private static string? ScanIl(MethodBase method, byte[] il)
    {
        var i = 0;
        while (i < il.Length)
        {
            short value = il[i] == 0xFE && i + 1 < il.Length ? (short)(0xFE00 | il[i + 1]) : il[i];
            if (!OpCodesByValue.TryGetValue(value, out var opCode)) return null; // unknown: stop scanning safely
            i += opCode.Size;

            if (FloatOpCodes.Contains(opCode.Value))
                return $"IL {opCode.Name}";

            if (opCode.OperandType == OperandType.InlineMethod)
            {
                var token = BitConverter.ToInt32(il, i);
                var callee = TryResolve(method, token);
                if (callee is not null && UsesFloating(callee))
                    return $"calls {callee.DeclaringType?.Name}.{callee.Name}";
            }

            i += OperandSize(opCode, il, i);
        }

        return null;
    }

    private static MethodBase? TryResolve(MethodBase method, int token)
    {
        try
        {
            return method.Module.ResolveMethod(token,
                method.DeclaringType?.IsGenericType == true ? method.DeclaringType.GetGenericArguments() : null,
                method.IsGenericMethod ? method.GetGenericArguments() : null);
        }
        catch
        {
            return null;
        }
    }

    private static bool UsesFloating(MethodBase callee) =>
        callee.GetParameters().Any(p => FloatingTypes.Contains(p.ParameterType)) ||
        (callee is MethodInfo mi && FloatingTypes.Contains(mi.ReturnType));

    private static int OperandSize(OpCode opCode, byte[] il, int position) => opCode.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + BitConverter.ToInt32(il, position) * 4,
        _ => 4
    };
}
