using System.Reflection;
using NetArchTest.Rules;

namespace FlowTwin.ArchitectureTests;

/// <summary>
/// Regla de la sección "Dependencias entre proyectos": un módulo solo puede usar de otro su carpeta
/// Contracts. Si alguien referencia el dominio o los casos de uso de otro módulo, la compilación de
/// pruebas falla aquí.
/// </summary>
public class ReglasDeDependenciaTests
{
    private static readonly string[] Modulos =
        ["Identidad", "Gemelo", "Dispositivos", "Atenciones", "Monitoreo", "Reportes", "Auditoria"];

    private static Assembly Ensamblado(string modulo) => Assembly.Load($"FlowTwin.{modulo}");

    public static TheoryData<string> TodosLosModulos() => [.. Modulos];

    [Theory]
    [MemberData(nameof(TodosLosModulos))]
    public void Un_modulo_no_usa_el_dominio_ni_la_aplicacion_de_otro(string modulo)
    {
        var prohibidos = Modulos.Where(m => m != modulo)
            .SelectMany(m => new[] { $"FlowTwin.{m}.Domain", $"FlowTwin.{m}.Application", $"FlowTwin.{m}.Endpoints" })
            .ToArray();

        var resultado = Types.InAssembly(Ensamblado(modulo)).ShouldNot().HaveDependencyOnAny(prohibidos).GetResult();

        Assert.True(resultado.IsSuccessful,
            $"{modulo} usa partes internas de otro módulo: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
    }

    [Theory]
    [MemberData(nameof(TodosLosModulos))]
    public void El_dominio_no_conoce_la_base_ni_la_web(string modulo)
    {
        var resultado = Types.InAssembly(Ensamblado(modulo))
            .That().ResideInNamespace($"FlowTwin.{modulo}.Domain")
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(resultado.IsSuccessful,
            $"El dominio de {modulo} depende de EF Core o ASP.NET: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void El_SharedKernel_no_depende_de_ningun_modulo()
    {
        var resultado = Types.InAssembly(Assembly.Load("FlowTwin.SharedKernel"))
            .ShouldNot().HaveDependencyOnAny([.. Modulos.Select(m => $"FlowTwin.{m}")])
            .GetResult();

        Assert.True(resultado.IsSuccessful);
    }
}
