using System.Globalization;
using System.Text;
using Tw.Core;

namespace Tw.Generators;

/// <summary>Serializes a compiled <see cref="StylePlan"/> back into C# construction code.</summary>
internal static class PlanEmitter
{
    public static string Emit(StylePlan plan)
    {
        var sb = new StringBuilder();
        sb.Append("global::Tw.Core.StylePlan.Precompiled(\n");
        sb.Append("            light: ").Append(Declarations(plan.Light)).Append(",\n");
        sb.Append("            dark: ").Append(Declarations(plan.Dark)).Append(",\n");

        sb.Append("            states: new global::Tw.Core.StatePlan[] { ");
        foreach (var state in plan.States)
        {
            sb.Append($"new global::Tw.Core.StatePlan(global::Tw.Core.TwInteractiveState.{state.State}, ");
            sb.Append(Declarations(state.Light)).Append(", ").Append(Declarations(state.Dark)).Append("), ");
        }
        sb.Append("},\n");

        sb.Append("            breakpoints: new global::Tw.Core.BreakpointPlan[] { ");
        foreach (var bp in plan.Breakpoints)
        {
            sb.Append($"new global::Tw.Core.BreakpointPlan({F(bp.MinWidth)}, ");
            sb.Append(Declarations(bp.Light)).Append(", ").Append(Declarations(bp.Dark)).Append("), ");
        }
        sb.Append("})");
        return sb.ToString();
    }

    private static string Declarations(TwDeclaration[] set)
    {
        var sb = new StringBuilder("new global::Tw.Core.TwDeclaration[] { ");
        foreach (var d in set)
            sb.Append($"new global::Tw.Core.TwDeclaration(global::Tw.Core.TwPropertyId.{d.Property}, {Value(d.Value)}), ");
        sb.Append('}');
        return sb.ToString();
    }

    private static string Value(TwValue v) => v.Kind switch
    {
        TwValueKind.Scalar => $"global::Tw.Core.TwValue.Scalar({F(v.X)})",
        TwValueKind.Color => $"global::Tw.Core.TwValue.Color(0x{v.Rgba:X8}u)",
        TwValueKind.Edges => $"global::Tw.Core.TwValue.Edges({F(v.X)}, {F(v.Y)}, {F(v.Z)}, {F(v.W)})",
        TwValueKind.Corners => $"global::Tw.Core.TwValue.Corners({F(v.X)}, {F(v.Y)}, {F(v.Z)}, {F(v.W)})",
        TwValueKind.Shadow => $"global::Tw.Core.TwValue.Shadow(0x{v.Rgba:X8}u, {F(v.X)}, {F(v.Y)}, {F(v.Z)})",
        _ => $"global::Tw.Core.TwValue.Enum({(byte)v.X})",
    };

    private static string F(float value)
    {
        if (float.IsNaN(value)) return "float.NaN";
        if (float.IsPositiveInfinity(value)) return "float.PositiveInfinity";
        if (float.IsNegativeInfinity(value)) return "float.NegativeInfinity";
        if (value >= float.MaxValue) return "float.MaxValue";
        // G9 (not "R"): round-trips exactly on all compiler hosts.
        return value.ToString("G9", CultureInfo.InvariantCulture) + "f";
    }
}
