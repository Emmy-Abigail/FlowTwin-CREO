using System.Net.Http.Json;
using System.Text.Json;
using FlowTwin.Atenciones.Application;
using FlowTwin.Atenciones.Domain;
using FlowTwin.Dispositivos.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.IntegrationTests;

/// <summary>
/// F-07 de punta a punta: entrega de la tarjeta → lecturas en ope.LecturaBle → motor de presencia →
/// ope.EventoZona → devolución. El motor no corre solo en las pruebas (MotorActivo = false): se llama
/// a ProcesarAsync a mano, así la prueba no espera 45 s reales de histéresis.
/// </summary>
[Collection("Api")]
public class PresenciaTests(ApiFixture api)
{
    private const int SalaDeEspera = 3;

    private async Task<int> ProcesarAsync()
    {
        using var scope = api.Fabrica.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ProcesadorPresencia>().ProcesarAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Del_nodo_de_la_Sala_a_la_estadia_en_la_Sala_y_se_cierra_al_devolver()
    {
        // 1. El Operador entrega T-09; la atención "empezó" hace 5 minutos para poder poner lecturas antes de ahora
        var operador = await api.ClienteDeAsync("operador@creo.pe");
        var entrega = await (await operador.PostAsJsonAsync("/api/v1/admision/escaneos", new { codigoQr = TarjetasDePrueba.Presencia, modo = "AUTO" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var atencionId = entrega.GetProperty("atencion").GetProperty("id").GetInt64();
        await api.EnBaseAsync(db => db.Atenciones.Where(a => a.Id == atencionId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.InicioEn, DateTime.UtcNow.AddMinutes(-5))));

        // 2. N-02 (Sala de espera) ve su emisor a -58 dBm cada 2 s durante 60 s
        var inicio = DateTime.UtcNow.AddSeconds(-60);
        await api.EnBaseAsync(async db =>
        {
            var n02 = await db.Nodos.Where(n => n.Codigo == "N-02").Select(n => n.Id).SingleAsync();
            var emisor = EmisorBle.Identificador(major: 1, minor: int.Parse(TarjetasDePrueba.Presencia[2..]));
            db.LecturasBle.AddRange(Enumerable.Range(0, 31).Select(i => LecturaBle.Desde(n02, emisor, -58, 0, inicio.AddSeconds(i * 2))));
            return await db.SaveChangesAsync();
        });

        // 3. El motor confirma la entrada, fechada cuando la Sala empezó a ser la mejor (RN-10)
        await ProcesarAsync();

        var estadia = await api.EnBaseAsync(db => db.EventosZona.SingleAsync(e => e.AtencionId == atencionId));
        Assert.Equal(SalaDeEspera, estadia.ZonaId);
        Assert.Null(estadia.SalidaEn);
        Assert.InRange(estadia.EntradaEn, inicio.AddSeconds(-1), inicio.AddSeconds(1));
        Assert.NotNull(await api.EnBaseAsync(db => db.Atenciones.Where(a => a.Id == atencionId).Select(a => a.UltimaLecturaEn).SingleAsync()));

        // Procesar otra vez no duplica nada (RN-11)
        await ProcesarAsync();
        Assert.Equal(1, await api.EnBaseAsync(db => db.EventosZona.CountAsync(e => e.AtencionId == atencionId)));

        // 4. Al devolver la tarjeta, la estadía termina en el mismo instante
        await operador.PostAsJsonAsync("/api/v1/admision/escaneos", new { codigoQr = TarjetasDePrueba.Presencia, modo = "AUTO" });

        var cerrada = await api.EnBaseAsync(db => db.EventosZona.SingleAsync(e => e.AtencionId == atencionId));
        Assert.NotNull(cerrada.SalidaEn);
    }

    [Fact]
    public async Task Las_lecturas_de_un_emisor_sin_atencion_abierta_se_ignoran_RN12()
    {
        // T-12 está Disponible: nadie la lleva, sus lecturas no generan estadías
        await api.EnBaseAsync(async db =>
        {
            var n02 = await db.Nodos.Where(n => n.Codigo == "N-02").Select(n => n.Id).SingleAsync();
            var emisor = EmisorBle.Identificador(major: 1, minor: TarjetasDePrueba.SinAtencion);
            var inicio = DateTime.UtcNow.AddSeconds(-60);
            db.LecturasBle.AddRange(Enumerable.Range(0, 31).Select(i => LecturaBle.Desde(n02, emisor, -55, 0, inicio.AddSeconds(i * 2))));
            return await db.SaveChangesAsync();
        });
        var antes = await api.EnBaseAsync(db => db.EventosZona.CountAsync());

        await ProcesarAsync();

        Assert.Equal(antes, await api.EnBaseAsync(db => db.EventosZona.CountAsync()));
    }

    [Fact]
    public async Task La_base_impide_dos_estadias_abiertas_de_la_misma_atencion_RN11()
    {
        var atencionId = await api.EnBaseAsync(db => db.Atenciones.Select(a => a.Id).FirstAsync());
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => api.EnBaseAsync(async db =>
        {
            db.EventosZona.Add(new EventoZona { AtencionId = atencionId, ZonaId = 3, EntradaEn = DateTime.UtcNow.AddDays(-30) });
            db.EventosZona.Add(new EventoZona { AtencionId = atencionId, ZonaId = 4, EntradaEn = DateTime.UtcNow.AddDays(-30) });
            return await db.SaveChangesAsync();
        }));

        Assert.Contains("UX_EventoZona_AtencionAbierta", ex.InnerException?.Message);
    }
}
