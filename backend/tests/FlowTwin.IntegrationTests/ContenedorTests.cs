using FlowTwin.Atenciones.Application;
using FlowTwin.Dispositivos.Application;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Gemelo.Application;
using FlowTwin.Gemelo.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.IntegrationTests;

/// <summary>
/// Las pruebas de arquitectura revisan dependencias entre proyectos, pero no ven un ciclo entre
/// clases al construirlas (A necesita B y B necesita A a través de contratos). Ese ciclo no da un
/// error claro: la petición se queda colgada. Esta prueba lo convierte en una falla con mensaje.
/// </summary>
[Collection("Api")]
public class ContenedorTests(ApiFixture api)
{
    [Theory]
    [InlineData(typeof(GemeloServicio))]
    [InlineData(typeof(ISedesConsultas))]
    [InlineData(typeof(IZonasConsultas))]
    [InlineData(typeof(IUbicacionNodos))]
    [InlineData(typeof(NodosServicio))]
    [InlineData(typeof(TarjetasServicio))]
    [InlineData(typeof(IAutenticacionNodos))]
    [InlineData(typeof(IngestaNodosServicio))]
    [InlineData(typeof(IngestaServicio))]
    [InlineData(typeof(PurgaLecturasServicio))]
    [InlineData(typeof(IPresenciaDispositivos))]
    [InlineData(typeof(ProcesadorPresencia))]
    [InlineData(typeof(FlowTwin.Monitoreo.Application.EstadoPisoServicio))]
    [InlineData(typeof(FlowTwin.Atenciones.Contracts.IAtencionesEnCurso))]
    public async Task Cada_servicio_se_construye_sin_ciclos(Type servicio)
    {
        using var scope = api.Fabrica.Services.CreateScope();

        var construir = Task.Run(() => scope.ServiceProvider.GetRequiredService(servicio));
        var termino = await Task.WhenAny(construir, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.True(termino == construir, $"{servicio.Name} no se terminó de construir: revisa si hay un ciclo de dependencias.");
        Assert.NotNull(await construir);
    }
}
