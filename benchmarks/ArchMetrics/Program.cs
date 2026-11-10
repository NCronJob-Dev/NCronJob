using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Usage: ArchMetrics <srcRoot> <binDirWithDependencies> [--json out.json]
var srcRoot = Path.GetFullPath(args[0]);
var binDir = Path.GetFullPath(args[1]);
var jsonOut = args.Length > 3 && args[2] == "--json" ? args[3] : null;
var sep = Path.DirectorySeparatorChar;

var files = Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
    .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
    .ToList();

var trees = files.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), new CSharpParseOptions(LanguageVersion.Preview), f)).ToList();

// Same imports the SDK generates for <ImplicitUsings>enable</ImplicitUsings> in a Microsoft.NET.Sdk project.
trees.Add(CSharpSyntaxTree.ParseText("""
    global using System;
    global using System.Collections.Generic;
    global using System.IO;
    global using System.Linq;
    global using System.Net.Http;
    global using System.Threading;
    global using System.Threading.Tasks;
    """, new CSharpParseOptions(LanguageVersion.Preview), "ImplicitUsings.g.cs"));
var refDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
var refs = Directory.EnumerateFiles(refDir, "*.dll")
    .Concat(Directory.EnumerateFiles(binDir, "*.dll").Where(f => !Path.GetFileName(f).Equals("NCronJob.dll", StringComparison.OrdinalIgnoreCase)))
    .GroupBy(Path.GetFileName).Select(g => g.First())
    .Where(f => { try { System.Reflection.AssemblyName.GetAssemblyName(f); return true; } catch { return false; } })
    .Select(f => (MetadataReference)MetadataReference.CreateFromFile(f))
    .ToList();

var compilation = CSharpCompilation.Create("NCronJob", trees, refs,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

// Unresolved symbols would be skipped silently by the analysis below and understate coupling, so refuse to continue.
// CS8795/CS0762 stem from the missing [LoggerMessage] generator output.
string[] expectedErrors = ["CS8795", "CS0762"];
var bindingErrors = compilation.GetDiagnostics()
    .Where(d => d.Severity == DiagnosticSeverity.Error && !expectedErrors.Contains(d.Id))
    .ToList();
if (bindingErrors.Count > 0)
{
    foreach (var error in bindingErrors.Take(20))
        Console.Error.WriteLine(error);
    Console.Error.WriteLine($"{bindingErrors.Count} compilation errors: the metrics would be unreliable. Build the library first so its dependencies are in <binDir>.");
    return 1;
}

string ComponentOf(string path)
{
    var parts = Path.GetRelativePath(srcRoot, path).Split(sep);
    return parts.Length == 1 ? "(root)" : parts[0];
}

var typeInfos = new Dictionary<INamedTypeSymbol, TypeInfo>(SymbolEqualityComparer.Default);
foreach (var tree in trees)
{
    var model = compilation.GetSemanticModel(tree);
    foreach (var decl in tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Where(d => d.Parent is not TypeDeclarationSyntax))
    {
        var sym = ((INamedTypeSymbol)model.GetDeclaredSymbol(decl)!).OriginalDefinition;
        if (!typeInfos.TryGetValue(sym, out var ti))
            typeInfos[sym] = ti = new TypeInfo(sym, ComponentOf(tree.FilePath));
        ti.Decls.Add((decl, model));
    }
}

INamedTypeSymbol? TopLevelOf(INamedTypeSymbol? t)
{
    if (t is null) return null;
    t = t.OriginalDefinition;
    while (t.ContainingType is not null) t = t.ContainingType;
    return t;
}

INamedTypeSymbol? ProjectType(ITypeSymbol? t)
{
    var top = t is INamedTypeSymbol nt ? TopLevelOf(nt) : null;
    return top is not null && typeInfos.ContainsKey(top) ? top : null;
}

void AddTypeDeps(ITypeSymbol? t, TypeInfo ti)
{
    switch (t)
    {
        case null: return;
        case IArrayTypeSymbol at: AddTypeDeps(at.ElementType, ti); return;
        case INamedTypeSymbol nt:
            var owner = ProjectType(nt);
            if (owner is not null && !SymbolEqualityComparer.Default.Equals(owner, ti.Symbol)) ti.Deps.Add(owner);
            foreach (var a in nt.TypeArguments) AddTypeDeps(a, ti);
            break;
    }
}

foreach (var ti in typeInfos.Values)
{
    foreach (var (decl, model) in ti.Decls)
    {
        var loc = decl.GetLocation().GetLineSpan();
        ti.Loc += loc.EndLinePosition.Line - loc.StartLinePosition.Line + 1;

        if (decl is TypeDeclarationSyntax tds)
        {
            if (model.GetDeclaredSymbol(tds) is INamedTypeSymbol self)
            {
                AddTypeDeps(self.BaseType, ti);
                foreach (var i in self.Interfaces) AddTypeDeps(i, ti);
            }
        }

        foreach (var node in decl.DescendantNodes())
        {
            if (node is not (IdentifierNameSyntax or GenericNameSyntax)) continue;
            var s = model.GetSymbolInfo(node).Symbol;
            switch (s)
            {
                case INamedTypeSymbol nt: AddTypeDeps(nt, ti); break;
                case IMethodSymbol m: AddTypeDeps(m.ContainingType, ti); AddTypeDeps(m.ReturnType, ti); break;
                case IPropertySymbol p: AddTypeDeps(p.ContainingType, ti); AddTypeDeps(p.Type, ti); break;
                case IFieldSymbol f: AddTypeDeps(f.ContainingType, ti); AddTypeDeps(f.Type, ti); break;
                case ILocalSymbol l: AddTypeDeps(l.Type, ti); break;
                case IParameterSymbol pa: AddTypeDeps(pa.Type, ti); break;
            }
        }
    }
}

foreach (var ti in typeInfos.Values)
    foreach (var d in ti.Deps)
        typeInfos[d].Dependents.Add(ti.Symbol);

foreach (var ti in typeInfos.Values) ComputeCohesion(ti);

void ComputeCohesion(TypeInfo ti)
{
    if (ti.Symbol.TypeKind is not (TypeKind.Class or TypeKind.Struct)) return;

    var attrs = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
    var used = new Dictionary<ISymbol, HashSet<ISymbol>>(SymbolEqualityComparer.Default);
    var called = new Dictionary<ISymbol, HashSet<ISymbol>>(SymbolEqualityComparer.Default);

    bool IsInstanceMember(ISymbol s) => !s.IsStatic && SymbolEqualityComparer.Default.Equals(TopLevelOf(s.ContainingType), ti.Symbol);

    foreach (var (decl, model) in ti.Decls)
    {
        if (decl is not TypeDeclarationSyntax tds) continue;

        // Captured primary constructor parameters are state. A record's positional parameters are exposed as properties
        // and referenced as those, so the generated properties are the state there.
        if (tds.ParameterList is not null)
        {
            foreach (var p in tds.ParameterList.Parameters)
            {
                if (model.GetDeclaredSymbol(p) is not { } ps) continue;
                if (tds is RecordDeclarationSyntax)
                {
                    var property = ps.ContainingType.GetMembers(ps.Name).OfType<IPropertySymbol>().FirstOrDefault();
                    if (property is not null) attrs.Add(property.OriginalDefinition);
                }
                else attrs.Add(ps.OriginalDefinition);
            }
        }

        foreach (var member in tds.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax fd:
                    foreach (var v in fd.Declaration.Variables)
                        if (model.GetDeclaredSymbol(v) is IFieldSymbol { IsStatic: false, IsConst: false } f) attrs.Add(f);
                    break;
                case PropertyDeclarationSyntax pd when model.GetDeclaredSymbol(pd) is { IsStatic: false } p:
                    var isAuto = pd.ExpressionBody is null && pd.AccessorList!.Accessors.All(a => a.Body is null && a.ExpressionBody is null);
                    if (isAuto) { attrs.Add(p); break; }
                    used[p] = []; called[p] = [];
                    Collect(pd, model, p);
                    break;
                case MethodDeclarationSyntax md when model.GetDeclaredSymbol(md) is { IsStatic: false } m:
                    used[m] = []; called[m] = [];
                    Collect(md, model, m);
                    break;
            }
        }
    }

    void Collect(SyntaxNode body, SemanticModel model, ISymbol owner)
    {
        foreach (var id in body.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            var s = model.GetSymbolInfo(id).Symbol?.OriginalDefinition;
            if (s is null || SymbolEqualityComparer.Default.Equals(s, owner)) continue;
            switch (s)
            {
                case IFieldSymbol or IPropertySymbol when IsInstanceMember(s):
                    used[owner].Add(s); break;
                case IParameterSymbol { ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Constructor } c } when SymbolEqualityComparer.Default.Equals(TopLevelOf(c.ContainingType), ti.Symbol):
                    used[owner].Add(s); break;
                case IMethodSymbol { MethodKind: MethodKind.Ordinary } ms when IsInstanceMember(ms):
                    called[owner].Add(ms); break;
            }
        }
    }

    var nodes = used.Keys.ToList();
    ti.MethodCount = nodes.Count;
    if (nodes.Count == 0) { ti.Lcom4 = 0; return; }
    if (nodes.Count == 1) { ti.Lcom4 = 1; return; }

    var parent = nodes.ToDictionary(k => k, k => k, SymbolEqualityComparer.Default);
    ISymbol Find(ISymbol x)
    {
        while (!SymbolEqualityComparer.Default.Equals(parent[x], x)) { parent[x] = parent[parent[x]]; x = parent[x]; }
        return x;
    }
    void Union(ISymbol a, ISymbol b)
    {
        var ra = Find(a); var rb = Find(b);
        if (!SymbolEqualityComparer.Default.Equals(ra, rb)) parent[ra] = rb;
    }

    var usersOfAttr = new Dictionary<ISymbol, HashSet<ISymbol>>(SymbolEqualityComparer.Default);
    foreach (var a in attrs) usersOfAttr[a] = new(SymbolEqualityComparer.Default);
    foreach (var (n, u) in used)
        foreach (var a in u)
        {
            if (used.ContainsKey(a)) { Union(n, a); continue; }
            if (!usersOfAttr.TryGetValue(a, out var l)) usersOfAttr[a] = l = new(SymbolEqualityComparer.Default);
            l.Add(n);
        }
    foreach (var l in usersOfAttr.Values) { var arr = l.ToList(); for (var i = 1; i < arr.Count; i++) Union(arr[0], arr[i]); }
    foreach (var (n, c) in called) foreach (var m in c) if (used.ContainsKey(m)) Union(n, m);

    ti.Lcom4 = nodes.Select(Find).Distinct(SymbolEqualityComparer.Default).Count();

    // Henderson-Sellers LCOM*: (M - mean(methods per attribute)) / (M - 1); 0 = perfectly cohesive, >1 = lacking cohesion.
    // Attributes that no method uses count with zero users, so they lower the cohesion.
    if (usersOfAttr.Count == 0) ti.LcomHs = 1;
    else
    {
        var meanUsers = usersOfAttr.Values.Average(l => (double)l.Count);
        ti.LcomHs = Math.Round((nodes.Count - meanUsers) / (nodes.Count - 1), 3);
    }
}

foreach (var ti in typeInfos.Values)
    foreach (var (decl, _) in ti.Decls)
        ti.Complexity += decl.DescendantNodes().Count(n => n is IfStatementSyntax or WhileStatementSyntax or ForStatementSyntax or ForEachStatementSyntax or CaseSwitchLabelSyntax or CatchClauseSyntax or ConditionalExpressionSyntax or SwitchExpressionArmSyntax or CasePatternSwitchLabelSyntax or BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or LambdaExpressionSyntax)
            + decl.DescendantTokens().Count(t => t.IsKind(SyntaxKind.AmpersandAmpersandToken) || t.IsKind(SyntaxKind.BarBarToken) || t.IsKind(SyntaxKind.QuestionQuestionToken));

foreach (var ti in typeInfos.Values)
{
    var ctor = ti.Decls.SelectMany(d => d.Decl.Members().OfType<ConstructorDeclarationSyntax>()).OrderByDescending(c => c.ParameterList.Parameters.Count).FirstOrDefault();
    ti.CtorParams = ctor?.ParameterList.Parameters.Count
        ?? ti.Decls.Select(d => d.Decl).OfType<TypeDeclarationSyntax>().FirstOrDefault()?.ParameterList?.Parameters.Count ?? 0;
}

List<List<T>> Scc<T>(IEnumerable<T> nodes, Func<T, IEnumerable<T>> edges) where T : notnull
{
    var index = 0; var stack = new Stack<T>(); var idx = new Dictionary<T, int>(); var low = new Dictionary<T, int>(); var on = new HashSet<T>();
    var result = new List<List<T>>();
    void Strong(T v)
    {
        idx[v] = low[v] = index++; stack.Push(v); on.Add(v);
        foreach (var w in edges(v))
        {
            if (!idx.ContainsKey(w)) { Strong(w); low[v] = Math.Min(low[v], low[w]); }
            else if (on.Contains(w)) low[v] = Math.Min(low[v], idx[w]);
        }
        if (low[v] != idx[v]) return;
        var comp = new List<T>(); T w2;
        do { w2 = stack.Pop(); on.Remove(w2); comp.Add(w2); } while (!EqualityComparer<T>.Default.Equals(w2, v));
        result.Add(comp);
    }
    foreach (var n in nodes) if (!idx.ContainsKey(n)) Strong(n);
    return result;
}

var typeSccs = Scc<INamedTypeSymbol>(typeInfos.Keys.ToList(), t => typeInfos[t].Deps).Where(c => c.Count > 1).ToList();

var components = typeInfos.Values.Select(t => t.Component).Distinct().OrderBy(c => c).ToList();
var compDeps = components.ToDictionary(c => c, _ => new Dictionary<string, int>());
foreach (var t in typeInfos.Values)
    foreach (var d in t.Deps)
    {
        var dc = typeInfos[d].Component;
        if (dc == t.Component) continue;
        compDeps[t.Component][dc] = compDeps[t.Component].GetValueOrDefault(dc) + 1;
    }
var compSccs = Scc(components, c => compDeps[c].Keys).Where(c => c.Count > 1).ToList();

double Cbo(TypeInfo t) => t.Deps.Count + t.Dependents.Count;
// Coupled AND incohesive: efferent coupling x number of unrelated responsibility clusters x complexity weight
double Hotspot(TypeInfo t) => t.MethodCount >= 2 ? t.Deps.Count * Math.Max(t.Lcom4, 1) * (1 + t.Complexity / 50.0) : 0;

var tis = typeInfos.Values.OrderBy(t => t.Symbol.Name).ToList();
var classes = tis.Where(t => t.Symbol.TypeKind is TypeKind.Class && t.MethodCount >= 2).ToList();
var sb = new StringBuilder();
sb.AppendLine($"types={tis.Count} files={files.Count} typeDeclLines={tis.Sum(t => t.Loc)}");
sb.AppendLine();
sb.AppendLine("## Per-type metrics (sorted by Hotspot)");
sb.AppendLine("| Type | Component | LOC | Ce | Ca | CBO | I | CC | Ctor | M | LCOM4 | LCOM-HS | Hotspot |");
sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
foreach (var t in tis.OrderByDescending(Hotspot).ThenByDescending(Cbo))
{
    var ce = t.Deps.Count; var ca = t.Dependents.Count;
    var i = ce + ca == 0 ? 0 : (double)ce / (ce + ca);
    sb.AppendLine($"| {t.Symbol.Name} | {t.Component} | {t.Loc} | {ce} | {ca} | {Cbo(t)} | {i:0.00} | {t.Complexity} | {t.CtorParams} | {t.MethodCount} | {t.Lcom4} | {t.LcomHs:0.00} | {Hotspot(t):0.0} |");
}
sb.AppendLine();
sb.AppendLine("## Aggregates");
sb.AppendLine($"- Types: {tis.Count}");
sb.AppendLine($"- Mean Ce: {tis.Average(t => t.Deps.Count):0.00}; Max Ce: {tis.Max(t => t.Deps.Count)} ({tis.OrderByDescending(t => t.Deps.Count).First().Symbol.Name})");
sb.AppendLine($"- Mean CBO: {tis.Average(Cbo):0.00}; Max CBO: {tis.Max(Cbo)} ({tis.OrderByDescending(Cbo).First().Symbol.Name})");
sb.AppendLine($"- Classes with >=2 members: {classes.Count}; mean LCOM4: {classes.Average(t => t.Lcom4):0.00}; classes with LCOM4>1: {classes.Count(t => t.Lcom4 > 1)}; mean LCOM-HS: {classes.Average(t => t.LcomHs):0.00}");
sb.AppendLine($"- Sum Hotspot (all types): {tis.Sum(Hotspot):0.0}; top-5 hotspot sum: {tis.OrderByDescending(Hotspot).Take(5).Sum(Hotspot):0.0}; max hotspot: {tis.Max(Hotspot):0.0} ({tis.OrderByDescending(Hotspot).First().Symbol.Name})");
sb.AppendLine($"- Max ctor params: {tis.Max(t => t.CtorParams)} ({tis.OrderByDescending(t => t.CtorParams).First().Symbol.Name})");
sb.AppendLine($"- Type-level dependency cycles (SCCs>1): {typeSccs.Count}; types involved: {typeSccs.Sum(c => c.Count)}  [{string.Join("; ", typeSccs.Select(c => string.Join(",", c.Select(x => x.Name).OrderBy(x => x))))}]");
sb.AppendLine($"- Total type-level dependency edges: {tis.Sum(t => t.Deps.Count)}");
sb.AppendLine($"- Cross-component type edges: {tis.Sum(t => t.Deps.Count(d => typeInfos[d].Component != t.Component))}");
sb.AppendLine($"- Component-level cycles (SCCs>1): {compSccs.Count} [{string.Join("; ", compSccs.Select(c => string.Join(",", c.OrderBy(x => x))))}]");
sb.AppendLine();
sb.AppendLine("## Components");
sb.AppendLine("| Component | Types | Intra edges | Out edges | In edges | Cohesion intra/(intra+out) | Instability Ce/(Ca+Ce) |");
sb.AppendLine("|---|---|---|---|---|---|---|");
foreach (var c in components)
{
    var members = tis.Where(t => t.Component == c).ToList();
    var intra = members.Sum(t => t.Deps.Count(d => typeInfos[d].Component == c));
    var outE = members.Sum(t => t.Deps.Count(d => typeInfos[d].Component != c));
    var inE = tis.Where(t => t.Component != c).Sum(t => t.Deps.Count(d => typeInfos[d].Component == c));
    sb.AppendLine($"| {c} | {members.Count} | {intra} | {outE} | {inE} | {(intra + outE == 0 ? 0 : (double)intra / (intra + outE)):0.00} | {(outE + inE == 0 ? 0 : (double)outE / (outE + inE)):0.00} |");
}
sb.AppendLine();
sb.AppendLine("## Component dependencies (row depends on column; type-edge count)");
foreach (var c in components)
    sb.AppendLine($"- {c} -> " + (compDeps[c].Count == 0 ? "(none)" : string.Join(", ", compDeps[c].OrderByDescending(x => x.Value).Select(x => $"{x.Key}({x.Value})"))));
sb.AppendLine();
sb.AppendLine("## Dependencies per type");
foreach (var t in tis.Where(t => t.Deps.Count > 0))
    sb.AppendLine($"- {t.Symbol.Name} -> {string.Join(", ", t.Deps.Select(d => d.Name).OrderBy(x => x))}");

Console.WriteLine(sb.ToString());

if (jsonOut is not null)
{
    File.WriteAllText(jsonOut, System.Text.Json.JsonSerializer.Serialize(new
    {
        types = tis.Count,
        edges = tis.Sum(t => t.Deps.Count),
        meanCe = tis.Average(t => t.Deps.Count),
        meanCbo = tis.Average(Cbo),
        meanLcom4 = classes.Average(t => t.Lcom4),
        classesLcom4Gt1 = classes.Count(t => t.Lcom4 > 1),
        typeCycles = typeSccs.Count,
        componentCycles = compSccs.Count,
        sumHotspot = tis.Sum(Hotspot),
    }));
}

return 0;

static class Ext
{
    public static IEnumerable<MemberDeclarationSyntax> Members(this BaseTypeDeclarationSyntax d) =>
        d is TypeDeclarationSyntax t ? t.Members : [];
}

class TypeInfo(INamedTypeSymbol symbol, string component)
{
    public INamedTypeSymbol Symbol { get; } = symbol;
    public string Component { get; } = component;
    public List<(BaseTypeDeclarationSyntax Decl, SemanticModel Model)> Decls { get; } = [];
    public HashSet<INamedTypeSymbol> Deps { get; } = new(SymbolEqualityComparer.Default);
    public HashSet<INamedTypeSymbol> Dependents { get; } = new(SymbolEqualityComparer.Default);
    public int Loc, Complexity, CtorParams, Lcom4, MethodCount;
    public double LcomHs;
}
