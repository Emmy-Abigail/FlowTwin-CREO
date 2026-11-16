using FlowTwin.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Api.Infraestructura;

/// <summary>
/// Al arrancar, espera a que la base responda en vez de caerse. En Azure SQL gratuita la base se pausa tras un rato
/// sin uso y tarda hasta un minuto en despertar; en un servidor propio, SQL Server puede tardar en levantar después de
/// un reinicio. Mientras la API está encendida, sus tareas en segundo plano mantienen la base despierta, así que el
/// único momento en que hace falta esperar es este.
/// </summary>
public static class EsperaBaseDatos
{
    /// <summary>SQL Server: «Cannot open database … requested by the login». El servidor está, la base no.</summary>
    private const int BaseInexistente = 4060;

    public static async Task HastaQueRespondaAsync(IServiceProvider servicios, IConfiguration config, CancellationToken ct = default)
    {
        var limite = TimeSpan.FromSeconds(config.GetValue("BaseDatos:EsperaMaximaSegundos", 120));
        var log = servicios.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(EsperaBaseDatos));
        var inicio = DateTime.UtcNow;
        for (var intento = 1; ; intento++)
        {
            using (var scope = servicios.CreateScope())
            {
                var cadena = scope.ServiceProvider.GetRequiredService<FlowTwinDbContext>().Database.GetConnectionString();
                try
                {
                    await using var conexion = new SqlConnection(cadena);
                    await conexion.OpenAsync(ct);
                    return;
                }
                catch (SqlException e) when (e.Number == BaseInexistente)
                {
                    // El servidor responde y la base aún no existe (instalación nueva): la crean las migraciones
                    return;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    log.LogDebug(e, "La base no responde todavía");
                }
            }
            if (DateTime.UtcNow - inicio >= limite)
                throw new InvalidOperationException(
                    $"La base de datos no respondió en {limite.TotalSeconds:N0} s. Revisa ConnectionStrings__FlowTwin y que el servidor acepte conexiones.");
            log.LogWarning("La base de datos no responde todavía (intento {Intento}); se reintenta en 5 s", intento);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}
