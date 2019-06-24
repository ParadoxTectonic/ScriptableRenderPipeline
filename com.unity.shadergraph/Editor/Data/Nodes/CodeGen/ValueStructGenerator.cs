using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UnityEditor.ShaderGraph
{
    public static class ValueStructGenerator
    {
        private static string ValueTypeName(int componentCount)
            => componentCount == 1 ? "Float" : $"Float{componentCount}";

        private static string HlslTypeName(int componentCount)
            => componentCount == 1 ? "float" : $"float{componentCount}";

        private static char SwizzleComponentName(int v)
            => v == 3 ? 'w' : (char)('x' + v);

        private static string SwizzleName(char[] swizzleNames, int componentCount)
            => String.Concat(Enumerable.Take(swizzleNames, componentCount).Reverse());

        private static void GenerateSwizzle(StringBuilder sb, int maxComponents)
        {
            var swizzleName = new char[4];
            for (int swizzleComponents = 1; swizzleComponents <= 4; ++swizzleComponents)
            {
                var typeName = ValueTypeName(swizzleComponents);
                for (int i = 0; i < (int)Math.Pow(maxComponents, swizzleComponents); ++i)
                {
                    swizzleName[0] = SwizzleComponentName(i % maxComponents);
                    swizzleName[1] = SwizzleComponentName((i / maxComponents) % maxComponents);
                    swizzleName[2] = SwizzleComponentName((i / maxComponents / maxComponents) % maxComponents);
                    swizzleName[3] = SwizzleComponentName((i / maxComponents / maxComponents / maxComponents) % maxComponents);
                    var swizzle = SwizzleName(swizzleName, swizzleComponents);
                    sb.Append($"\t\tpublic {typeName} {swizzle}\n");
                    sb.Append($"\t\t\t=> new {typeName}() {{ Code = $\"({{Code}}).{swizzle}\" }};\n");
                    sb.Append("\n");
                }
            }
        }

        private static void GenerateConstructor(StringBuilder sb, List<int> stack)
        {
            int components = stack.Sum();
            var typeName = ValueTypeName(components);
            sb.Append($"\t\tpublic static {typeName} {typeName}(");
            for (int v = 0; v < stack.Count; ++v)
                sb.Append($"{ValueTypeName(stack[v])} v{v}{(v != stack.Count - 1 ? ", " : "")}");
            sb.Append(")\n");
            sb.Append($"\t\t\t=> new {typeName}() {{ Code = $\"{HlslTypeName(components)}(");
            for (int v = 0; v < stack.Count; ++v)
                sb.Append($"{{v{v}.Code}}{(v != stack.Count - 1 ? ", " : "")}");
            sb.Append(")\" };\n\n");
        }

        private static void GenerateConstructosRecurse(StringBuilder sb, int components, List<int> stack)
        {
            if (components == 0)
            {
                if (stack.Count > 1)
                    GenerateConstructor(sb, stack);
            }
            else
            {
                for (int i = components; i >= 1; --i)
                {
                    stack.Add(i);
                    GenerateConstructosRecurse(sb, components - i, stack);
                    stack.RemoveAt(stack.Count - 1);
                }
            }
        }

        private static void GenerateConstructors(StringBuilder sb)
        {
            var stack = new List<int>(4);
            for (int components = 2; components <= 4; ++components)
                GenerateConstructosRecurse(sb, components, stack);
        }

        private static void Intrinsic1(StringBuilder sb, string func, string returnType = "")
        {
            for (int components = 1; components <= 4; ++components)
            {
                var typeName = ValueTypeName(components);
                sb.Append($"\t\tpublic static {(returnType == "" ? typeName : returnType)} {func}({typeName} x)\n");
                sb.Append($"\t\t\t=> new {(returnType == "" ? typeName : returnType)}() {{ Code = $\"{func}({{x.Code}})\" }};\n\n");
            }
        }

        private static void Intrinsic2(StringBuilder sb, string func, string returnType = "")
        {
            for (int components = 1; components <= 4; ++components)
            {
                var typeName = ValueTypeName(components);
                sb.Append($"\t\tpublic static {(returnType == "" ? typeName : returnType)} {func}({typeName} x, {typeName} y)\n");
                sb.Append($"\t\t\t=> new {(returnType == "" ? typeName : returnType)}() {{ Code = $\"{func}({{x.Code}}, {{y.Code}})\" }};\n\n");
            }
        }

        private static void Intrinsic3(StringBuilder sb, string func, string returnType = "")
        {
            for (int components = 1; components <= 4; ++components)
            {
                var typeName = ValueTypeName(components);
                sb.Append($"\t\tpublic static {(returnType == "" ? typeName : returnType)} {func}({typeName} x, {typeName} y, {typeName} z)\n");
                sb.Append($"\t\t\t=> new {(returnType == "" ? typeName : returnType)}() {{ Code = $\"{func}({{x.Code}}, {{y.Code}}, {{z.Code}})\" }};\n\n");
            }
        }

        [MenuItem("Tools/SG/GenerateCs")]
        public static void Generate()
        {
            string path = "Packages/com.unity.shadergraph/Editor/Data/Nodes/CodeGen/Values.gen.cs";

            using (var stream = new StreamWriter(path))
            {
                var sb = new StringBuilder();
                sb.Append("// Auto-generated by Tools/SG/GenerateCs menu. DO NOT hand edit.\n");
                sb.Append("namespace UnityEditor.ShaderGraph.Hlsl\n");
                sb.Append("{\n");

                for (int i = 1; i <= 4; ++i)
                {
                    var typeName = ValueTypeName(i);
                    sb.Append($"\tpublic struct {typeName}\n");
                    sb.Append("\t{\n");
                    sb.Append($"\t\tpublic string Code;\n\n");

                    sb.Append("\t\t// C# doesn't allow overloading operator=...\n");
                    sb.Append($"\t\tpublic void AssignFrom({typeName} other)\n");
                    sb.Append("\t\t{\n");
                    sb.Append("\t\t\tCode = other.Code;\n");
                    sb.Append("\t\t}\n\n");

                    if (i == 1)
                    {
                        for (int j = 2; j <= 4; ++j)
                        {
                            var otherTypeName = ValueTypeName(j);
                            sb.Append($"\t\tpublic static implicit operator {otherTypeName}(Float x)\n");
                            sb.Append($"\t\t\t=> new {otherTypeName}() {{ Code = $\"({{x.Code}}).{String.Concat(Enumerable.Repeat("x", j))}\" }};\n");
                            sb.Append("\n");
                        }
                    }

                    if (i == 4)
                    {
                        sb.Append("\t\tpublic string Trim(int components)\n");
                        sb.Append("\t\t{\n");
                        sb.Append("\t\t\tif (components == 1)\n");
                        sb.Append("\t\t\t\treturn \".x\";\n");
                        sb.Append("\t\t\telse if (components == 2)\n");
                        sb.Append("\t\t\t\treturn \".xy\";\n");
                        sb.Append("\t\t\telse if (components == 3)\n");
                        sb.Append("\t\t\t\treturn \".xyz\";\n");
                        sb.Append("\t\t\telse\n");
                        sb.Append("\t\t\t\treturn \".xyzw\";\n");
                        sb.Append("\t\t}\n\n");
                    }

                    sb.Append($"\t\tpublic static {typeName} operator-({typeName} v)\n");
                    sb.Append($"\t\t\t=> new {typeName}() {{ Code = $\"-({{v.Code}})\" }};\n\n");

                    sb.Append($"\t\tpublic static implicit operator {typeName} (float v)\n");
                    sb.Append($"\t\t\t=> new {typeName}() {{ Code = $\"({{v}}).{String.Concat(Enumerable.Repeat("x", i))}\" }};\n\n");

                    sb.Append($"\t\tpublic static implicit operator {typeName} (int v)\n");
                    sb.Append($"\t\t\t=> new {typeName}() {{ Code = $\"({{v}}.0f).{String.Concat(Enumerable.Repeat("x", i))}\" }};\n\n");

                    sb.Append($"\t\tpublic static implicit operator {typeName} (double v)\n");
                    sb.Append($"\t\t\t=> new {typeName}() {{ Code = $\"({{(float)v}}).{String.Concat(Enumerable.Repeat("x", i))}\" }};\n\n");

                    sb.Append($"\t\tpublic static {typeName} operator+({typeName} x, {typeName} y)\n");
                    sb.Append($"\t\t\t=> new {typeName}() {{ Code = $\"({{x.Code}}) + ({{y.Code}})\" }};\n");
                    sb.Append("\n");
                    sb.Append($"\t\tpublic static {typeName} operator-({typeName} x, {typeName} y)\n");
                    sb.Append($"\t\t\t=> new {typeName}() {{ Code = $\"({{x.Code}}) - ({{y.Code}})\" }};\n");
                    sb.Append("\n");
                    sb.Append($"\t\tpublic static {typeName} operator*({typeName} x, {typeName} y)\n");
                    sb.Append($"\t\t\t=> new {typeName}() {{ Code = $\"({{x.Code}}) * ({{y.Code}})\" }};\n");
                    sb.Append("\n");
                    sb.Append($"\t\tpublic static {typeName} operator/({typeName} x, {typeName} y)\n");
                    sb.Append($"\t\t\t=> new {typeName}() {{ Code = $\"({{x.Code}}) / ({{y.Code}})\" }};\n");
                    sb.Append("\n");

                    GenerateSwizzle(sb, i);

                    sb.Append("\t}\n");
                    sb.Append("\n");
                }

                // intrinsics
                sb.Append("\tpublic static class Intrinsics\n");
                sb.Append("\t{\n");

                // Constructors
                GenerateConstructors(sb);

                Intrinsic1(sb, "abs");
                Intrinsic1(sb, "acos");
                Intrinsic1(sb, "asin");
                Intrinsic1(sb, "atan");
                Intrinsic2(sb, "atan2");
                Intrinsic1(sb, "ceil");
                Intrinsic3(sb, "clamp");
                Intrinsic1(sb, "cos");
                Intrinsic1(sb, "cosh");

                sb.Append("\t\tpublic static Float3 cross(Float3 x, Float3 y)\n");
                sb.Append("\t\t\t=> new Float3() { Code = $\"cross({x.Code}, {y.Code})\" };\n\n");

                Intrinsic1(sb, "ddx");
                Intrinsic1(sb, "ddy");
                Intrinsic1(sb, "degrees");
                Intrinsic2(sb, "distance", "Float");
                Intrinsic2(sb, "dot", "Float");
                Intrinsic1(sb, "exp");
                Intrinsic1(sb, "exp2");
                Intrinsic1(sb, "floor");
                Intrinsic2(sb, "fmod");
                Intrinsic1(sb, "frac");
                Intrinsic1(sb, "fwidth");
                Intrinsic1(sb, "length", "Float");
                Intrinsic3(sb, "lerp");
                Intrinsic1(sb, "log");
                Intrinsic1(sb, "log10");
                Intrinsic1(sb, "log2");
                Intrinsic2(sb, "max");
                Intrinsic2(sb, "min");
                Intrinsic1(sb, "normalize");
                Intrinsic2(sb, "pow");
                Intrinsic1(sb, "radians");
                Intrinsic2(sb, "reflect");
                Intrinsic2(sb, "refract");
                Intrinsic1(sb, "rcp");
                Intrinsic1(sb, "round");
                Intrinsic1(sb, "rsqrt");
                Intrinsic1(sb, "saturate");
                Intrinsic1(sb, "sign");
                Intrinsic1(sb, "sin");
                Intrinsic1(sb, "sinh");
                Intrinsic3(sb, "smoothstep");
                Intrinsic1(sb, "sqrt");
                Intrinsic2(sb, "step");
                Intrinsic1(sb, "tan");
                Intrinsic1(sb, "tanh");
                Intrinsic1(sb, "trunc");

                sb.Append("\t}\n");

                sb.Append("}\n");

                sb.Replace("\n", System.Environment.NewLine);
                sb.Replace("\t", "    ");
                stream.WriteLine(sb.ToString());
            }

            AssetDatabase.Refresh();
        }
    }
}
