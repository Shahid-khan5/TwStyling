using System.Globalization;
using System.Text;
using TwStyling;

namespace TwStyling.Generators;

/// <summary>Serializes a compiled <see cref="StylePlan"/> back into C# construction code.</summary>
internal static class PlanEmitter
{
    public static string Emit(StylePlan plan)
    {
        var sb = new StringBuilder();
        sb.Append("global::TwStyling.StylePlan.Precompiled(\n");
        sb.Append("            light: ").Append(Declarations(plan.Light)).Append(",\n");
        sb.Append("            dark: ").Append(Declarations(plan.Dark)).Append(",\n");

        sb.Append("            states: new global::TwStyling.StatePlan[] { ");
        foreach (var state in plan.States)
        {
            sb.Append($"new global::TwStyling.StatePlan(global::TwStyling.TwInteractiveState.{state.State}, ");
            sb.Append(Declarations(state.Light)).Append(", ").Append(Declarations(state.Dark)).Append("), ");
        }
        sb.Append("},\n");

        sb.Append("            breakpoints: new global::TwStyling.BreakpointPlan[] { ");
        foreach (var bp in plan.Breakpoints)
        {
            sb.Append($"new global::TwStyling.BreakpointPlan({F(bp.MinWidth)}, ");
            sb.Append(Declarations(bp.Light)).Append(", ").Append(Declarations(bp.Dark)).Append("), ");
        }
        sb.Append("})");
        return sb.ToString();
    }

    private static string Declarations(TwDeclaration[] set)
    {
        var sb = new StringBuilder("new global::TwStyling.TwDeclaration[] { ");
        foreach (var d in set)
            sb.Append($"new global::TwStyling.TwDeclaration(global::TwStyling.TwPropertyId.{d.Property}, {Value(d.Value)}), ");
        sb.Append('}');
        return sb.ToString();
    }

    private static string Value(TwValue v) => v.Kind switch
    {
        TwValueKind.Scalar => $"global::TwStyling.TwValue.Scalar({F(v.X)})",
        TwValueKind.Color => $"global::TwStyling.TwValue.Color(0x{v.Rgba:X8}u)",
        TwValueKind.Edges => $"global::TwStyling.TwValue.Edges({F(v.X)}, {F(v.Y)}, {F(v.Z)}, {F(v.W)})",
        TwValueKind.Corners => $"global::TwStyling.TwValue.Corners({F(v.X)}, {F(v.Y)}, {F(v.Z)}, {F(v.W)})",
        TwValueKind.Shadow => $"global::TwStyling.TwValue.Shadow(0x{v.Rgba:X8}u, {F(v.X)}, {F(v.Y)}, {F(v.Z)})",
        _ => $"global::TwStyling.TwValue.Enum({(byte)v.X})",
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
